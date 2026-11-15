[CmdletBinding()]
param(
    [switch]$Reset
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$envFile = Join-Path $repoRoot '.env'
if (-not (Test-Path -LiteralPath $envFile)) {
    throw "Missing .env file at $envFile"
}

$connStr = $null
foreach ($line in Get-Content -LiteralPath $envFile) {
    if ($line -match '^DATABASE_CONNECTION_STRING=(.+)$') {
        $connStr = $Matches[1].Trim().Trim('"').Trim("'")
        break
    }
}
if ([string]::IsNullOrWhiteSpace($connStr)) {
    throw 'DATABASE_CONNECTION_STRING is not configured in .env.'
}

$pgParams = @{}
foreach ($segment in $connStr.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)) {
    $kv = $segment.Split('=', 2)
    if ($kv.Count -eq 2) {
        $pgParams[$kv[0].Trim().ToLowerInvariant()] = $kv[1].Trim()
    }
}

$env:PGHOST = $pgParams['host']
$env:PGPORT = $(if ($pgParams.ContainsKey('port')) { $pgParams['port'] } else { '5432' })
$env:PGDATABASE = $pgParams['database']
$env:PGUSER = $pgParams['username']
$env:PGPASSWORD = $pgParams['password']
$env:PGCLIENTENCODING = 'UTF8'

function Get-PsqlPath {
    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = Get-ChildItem -Path 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending
    if ($candidates) { return $candidates[0].FullName }

    throw 'psql.exe not found on PATH or under C:\Program Files\PostgreSQL\*\bin.'
}

$psql = Get-PsqlPath

if ($Reset) {
    $resetSql = @"
DELETE FROM councils.score_summary_component WHERE summary_id IN (
    SELECT id FROM councils.score_summary WHERE group_id::text LIKE 'b2222222-2222-2222-2222-%'
);
DELETE FROM councils.score_summary_revision WHERE group_id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM councils.score_summary WHERE group_id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM councils.council_minute
WHERE id::text LIKE 'b2222222-2222-2222-2222-%'
   OR council_assignment_id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM councils.council_score
WHERE id::text LIKE 'b2222222-2222-2222-2222-%'
   OR council_assignment_id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM councils.council_assignment WHERE id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM councils.council_member WHERE id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM councils.council WHERE id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM councils.reviewer_evaluation
WHERE id::text LIKE 'b2222222-2222-2222-2222-%'
   OR assignment_id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM councils.reviewer_assignment WHERE id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM councils.advisor_evaluation
WHERE id::text LIKE 'b2222222-2222-2222-2222-%'
   OR group_id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM thesis.submission_file WHERE id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM thesis.final_report WHERE id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM thesis.student_group_member WHERE id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM thesis.student_group WHERE id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM topics.topic WHERE id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM identity.user_role WHERE user_id::text LIKE 'b2222222-2222-2222-2222-%';
DELETE FROM identity."user" WHERE id::text LIKE 'b2222222-2222-2222-2222-%';
"@
    $resetSql | & $psql -v ON_ERROR_STOP=1 -f -
    if ($LASTEXITCODE -ne 0) { throw "Failed to reset council minute demo data (exit $LASTEXITCODE)." }
    Write-Host 'Reset completed: removed council minute demo records (b2222222-2222-2222-2222-*).' -ForegroundColor Yellow
    return
}

function Get-AspNetIdentityV3Hash([string]$Password) {
    $salt = New-Object byte[] 16
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($salt)
    $iterations = 100000
    $subkey = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2(
        [System.Text.Encoding]::UTF8.GetBytes($Password),
        $salt,
        $iterations,
        [System.Security.Cryptography.HashAlgorithmName]::SHA512,
        32
    )
    $output = New-Object byte[] (13 + $salt.Length + $subkey.Length)
    $output[0] = 0x01
    $iterBytes = [BitConverter]::GetBytes([uint32]$iterations)
    $prfBytes = [BitConverter]::GetBytes([uint32]2)
    $saltLenBytes = [BitConverter]::GetBytes([uint32]$salt.Length)
    if ([BitConverter]::IsLittleEndian) {
        [Array]::Reverse($iterBytes)
        [Array]::Reverse($prfBytes)
        [Array]::Reverse($saltLenBytes)
    }
    [Buffer]::BlockCopy($prfBytes, 0, $output, 1, 4)
    [Buffer]::BlockCopy($iterBytes, 0, $output, 5, 4)
    [Buffer]::BlockCopy($saltLenBytes, 0, $output, 9, 4)
    [Buffer]::BlockCopy($salt, 0, $output, 13, $salt.Length)
    [Buffer]::BlockCopy($subkey, 0, $output, 13 + $salt.Length, $subkey.Length)
    return [Convert]::ToBase64String($output)
}

$demoPasswordPlain = 'UgtmDemo@2026'
$demoPasswordHash = Get-AspNetIdentityV3Hash -Password $demoPasswordPlain

$seedSql = @'
DO $$
DECLARE
    v_pwd_hash TEXT := '__DEMO_PWD_HASH__';
    v_batch_open UUID := 'ad3bc9b1-0163-4a99-a711-ecb8bdc0400b';

    v_admin_id   UUID := '11111111-1111-1111-1111-111111111111';
    v_gv_a_id    UUID := '22222222-2222-2222-2222-222222222222'; -- Nguyễn Văn A (Secretary)
    v_gv_c_id    UUID := '44444444-4444-4444-4444-444444444444'; -- Lê Quang C (Member + dual-role Reviewer in Nhóm E)
    v_gv_khang   UUID := '77777777-7777-7777-7777-777777777701'; -- TS. Phạm Minh Khang (Chairman)
    v_gv_d_id    UUID := 'a1111111-1111-1111-1111-000000000093'; -- TS. Trần Minh Đức
    v_gv_e_id    UUID := 'a1111111-1111-1111-1111-000000000094'; -- PGS.TS. Đặng Hoàng Minh
    v_faculty_id UUID := 'b2222222-2222-2222-2222-000000000099'; -- PGS.TS. Nguyễn Minh Triết (Faculty)

    v_role_student INT;
    v_role_faculty INT;
    v_role_lecturer INT;
    v_role_council INT;

    -- 8 students across 4 groups (D, E, F, G)
    v_sd1 UUID := 'b2222222-2222-2222-2222-000000000101';
    v_sd2 UUID := 'b2222222-2222-2222-2222-000000000102';
    v_se1 UUID := 'b2222222-2222-2222-2222-000000000103';
    v_se2 UUID := 'b2222222-2222-2222-2222-000000000104';
    v_sf1 UUID := 'b2222222-2222-2222-2222-000000000105';
    v_sf2 UUID := 'b2222222-2222-2222-2222-000000000106';
    v_sg1 UUID := 'b2222222-2222-2222-2222-000000000107';
    v_sg2 UUID := 'b2222222-2222-2222-2222-000000000108';

    -- Topics
    v_td UUID := 'b2222222-2222-2222-2222-000000000201';
    v_te UUID := 'b2222222-2222-2222-2222-000000000202';
    v_tf UUID := 'b2222222-2222-2222-2222-000000000203';
    v_tg UUID := 'b2222222-2222-2222-2222-000000000204';

    -- Groups
    v_gd UUID := 'b2222222-2222-2222-2222-000000000301';
    v_ge UUID := 'b2222222-2222-2222-2222-000000000302';
    v_gf UUID := 'b2222222-2222-2222-2222-000000000303';
    v_gg UUID := 'b2222222-2222-2222-2222-000000000304';

    -- Final Reports & Submission Files
    v_frd UUID := 'b2222222-2222-2222-2222-000000000401';
    v_fre UUID := 'b2222222-2222-2222-2222-000000000402';
    v_frf UUID := 'b2222222-2222-2222-2222-000000000403';
    v_frg UUID := 'b2222222-2222-2222-2222-000000000404';

    v_sfd UUID := 'b2222222-2222-2222-2222-000000000411';
    v_sfe UUID := 'b2222222-2222-2222-2222-000000000412';
    v_sff UUID := 'b2222222-2222-2222-2222-000000000413';
    v_sfg UUID := 'b2222222-2222-2222-2222-000000000414';

    v_shad TEXT := repeat('d', 64);
    v_shae TEXT := repeat('e', 64);
    v_shaf TEXT := repeat('f', 64);
    v_shag TEXT := repeat('a', 64);

    -- Advisor & Reviewer evaluations
    v_adv_d UUID := 'b2222222-2222-2222-2222-000000000501';
    v_adv_e UUID := 'b2222222-2222-2222-2222-000000000502';
    v_adv_f UUID := 'b2222222-2222-2222-2222-000000000503';
    v_adv_g UUID := 'b2222222-2222-2222-2222-000000000504';

    v_ra_d UUID := 'b2222222-2222-2222-2222-000000000511';
    v_ra_e UUID := 'b2222222-2222-2222-2222-000000000512';
    v_ra_f UUID := 'b2222222-2222-2222-2222-000000000513';
    v_ra_g UUID := 'b2222222-2222-2222-2222-000000000514';

    v_re_d UUID := 'b2222222-2222-2222-2222-000000000521';
    v_re_e UUID := 'b2222222-2222-2222-2222-000000000522';
    v_re_f UUID := 'b2222222-2222-2222-2222-000000000523';
    v_re_g UUID := 'b2222222-2222-2222-2222-000000000524';

    -- Active Council HĐ-UIT-02
    v_council_02 UUID := 'b2222222-2222-2222-2222-000000000702';
    v_cm2_chair  UUID := 'b2222222-2222-2222-2222-000000000711';
    v_cm2_sec    UUID := 'b2222222-2222-2222-2222-000000000712';
    v_cm2_mem    UUID := 'b2222222-2222-2222-2222-000000000713';

    -- Council Assignments (D = Open discrepancy, E = AllScoresEntered + DualRole, F = Locked, G = UnlockedByOverride)
    v_ca_d UUID := 'b2222222-2222-2222-2222-000000000721';
    v_ca_e UUID := 'b2222222-2222-2222-2222-000000000722';
    v_ca_f UUID := 'b2222222-2222-2222-2222-000000000723';
    v_ca_g UUID := 'b2222222-2222-2222-2222-000000000724';
BEGIN
    IF NOT EXISTS (SELECT 1 FROM academic.kltn_batch WHERE id = v_batch_open) THEN
        RAISE EXCEPTION 'Expected Open batch % not found. Run app migration/seed first.', v_batch_open;
    END IF;

    SELECT id INTO v_role_student FROM identity.role WHERE role_name = 'Student' LIMIT 1;
    SELECT id INTO v_role_faculty FROM identity.role WHERE role_name = 'Faculty' LIMIT 1;
    SELECT id INTO v_role_lecturer FROM identity.role WHERE role_name = 'Lecturer' LIMIT 1;
    SELECT id INTO v_role_council FROM identity.role WHERE role_name = 'CouncilMember' LIMIT 1;

    -- Ensure lecturers/admin passwords match demo password
    UPDATE identity."user"
    SET password_hash = v_pwd_hash,
        access_failed_count = 0,
        status = 'Active',
        updated_at = NOW(),
        password_changed_at = NOW()
    WHERE id IN (v_admin_id, v_gv_a_id, v_gv_c_id, v_gv_khang);

    -- Ensure GV0003 & GV0004 exist (in case SeedGradingDemo.ps1 wasn't run)
    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    (v_gv_d_id,    'GV0003',    'TS. Trần Minh Đức',          'gv.tranminhduc@ugtm.edu.vn',   'GV.TRANMINHDUC@UGTM.EDU.VN',   v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_gv_e_id,    'GV0004',    'PGS.TS. Đặng Hoàng Minh',     'gv.danghoangminh@ugtm.edu.vn', 'GV.DANGHOANGMINH@UGTM.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_faculty_id, 'FACULTY01', 'PGS.TS. Nguyễn Minh Triết',   'truongkhoa@ugtm.edu.vn',       'TRUONGKHOA@UGTM.EDU.VN',       v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO UPDATE
      SET password_hash = EXCLUDED.password_hash,
          status = 'Active',
          access_failed_count = 0;

    INSERT INTO identity.user_role (user_id, role_id, assigned_at)
    SELECT u_id, r_id, NOW()
    FROM (VALUES
        (v_gv_d_id,    v_role_lecturer),
        (v_gv_e_id,    v_role_lecturer),
        (v_gv_khang,   v_role_lecturer),
        (v_gv_khang,   v_role_council),
        (v_gv_a_id,    v_role_lecturer),
        (v_gv_a_id,    v_role_council),
        (v_gv_c_id,    v_role_lecturer),
        (v_gv_c_id,    v_role_council),
        (v_faculty_id, v_role_faculty)
    ) AS v(u_id, r_id)
    WHERE NOT EXISTS (
        SELECT 1 FROM identity.user_role ur WHERE ur.user_id = v.u_id AND ur.role_id = v.r_id
    );

    -- Students for Groups D, E, F, G
    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    (v_sd1, '22520401', 'Bùi Quang Huy',   '22520401@gm.uit.edu.vn', '22520401@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_sd2, '22520402', 'Đoàn Ngọc Hân',   '22520402@gm.uit.edu.vn', '22520402@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_se1, '22520403', 'Trịnh Hoàng Nam', '22520403@gm.uit.edu.vn', '22520403@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_se2, '22520404', 'Ngô Phương Uyên', '22520404@gm.uit.edu.vn', '22520404@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_sf1, '22520405', 'Phan Minh Quân',  '22520405@gm.uit.edu.vn', '22520405@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_sf2, '22520406', 'Đinh Khánh Linh', '22520406@gm.uit.edu.vn', '22520406@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_sg1, '22520407', 'Vũ Hải Đăng',     '22520407@gm.uit.edu.vn', '22520407@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_sg2, '22520408', 'Lương Bảo Trâm',  '22520408@gm.uit.edu.vn', '22520408@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO identity.user_role (user_id, role_id, assigned_at)
    SELECT s_id, v_role_student, NOW()
    FROM (VALUES (v_sd1),(v_sd2),(v_se1),(v_se2),(v_sf1),(v_sf2),(v_sg1),(v_sg2)) AS t(s_id)
    WHERE NOT EXISTS (
        SELECT 1 FROM identity.user_role ur WHERE ur.user_id = t.s_id AND ur.role_id = v_role_student
    );

    -- Topics (advisor is GV0003 for all 4 so HĐ-UIT-02 members have zero advisor conflict)
    INSERT INTO topics.topic (
        id, batch_id, advisor_id, title_vi, title_en, description, requirements, status, student_number, created_at
    ) VALUES
    (v_td, v_batch_open, v_gv_d_id, 'Hệ thống phát hiện bất thường log phân tán sử dụng eBPF và Stream Processing', 'Distributed Log Anomaly Detection with eBPF and Stream Processing', 'Thu thập kernel telemetry qua eBPF và phát hiện bất thường theo thời gian thực.', 'eBPF, Kafka, Flink', 'Approved', 2, NOW() - INTERVAL '45 days'),
    (v_te, v_batch_open, v_gv_d_id, 'Ứng dụng Federated Learning bảo mật dữ liệu y tế trên thiết bị biên', 'Privacy-Preserving Medical Federated Learning on Edge Devices', 'Huấn luyện mô hình chẩn đoán phân tán không chia sẻ dữ liệu bệnh án thô.', 'PySyft, Differential Privacy', 'Approved', 2, NOW() - INTERVAL '45 days'),
    (v_tf, v_batch_open, v_gv_d_id, 'Tối ưu hóa suy luận mô hình ngôn ngữ nhỏ bằng Quantization AWQ trên GPU phổ thông', 'Optimizing Small Language Model Inference via AWQ Quantization on Consumer GPUs', 'Nén trọng số 4-bit AWQ giữ độ chính xác benchmark tiếng Việt.', 'vLLM, AWQ, CUDA', 'Approved', 2, NOW() - INTERVAL '45 days'),
    (v_tg, v_batch_open, v_gv_d_id, 'Xây dựng nền tảng phân tích mã độc Android kết hợp đồ thị lời gọi API', 'Android Malware Analysis Platform with API Call Graph Representation', 'Phát hiện mã độc di động thông qua Graph Neural Network trên CFG/API graph.', 'GNN, Androguard, Neo4j', 'Approved', 2, NOW() - INTERVAL '45 days')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.student_group (
        id, topic_id, status, created_at, allow_syllabus_resubmission
    ) VALUES
    (v_gd, v_td, 'ReadyForReviewerAssignment', NOW() - INTERVAL '40 days', false),
    (v_ge, v_te, 'ReadyForReviewerAssignment', NOW() - INTERVAL '40 days', false),
    (v_gf, v_tf, 'ReadyForReviewerAssignment', NOW() - INTERVAL '40 days', false),
    (v_gg, v_tg, 'ReadyForReviewerAssignment', NOW() - INTERVAL '40 days', false)
    ON CONFLICT (id) DO UPDATE SET status = EXCLUDED.status;

    INSERT INTO thesis.student_group_member (
        id, group_id, student_id, batch_id, joined_at, confirmation_status, confirmed_at_utc
    ) VALUES
    ('b2222222-2222-2222-2222-000000000311', v_gd, v_sd1, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('b2222222-2222-2222-2222-000000000312', v_gd, v_sd2, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('b2222222-2222-2222-2222-000000000313', v_ge, v_se1, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('b2222222-2222-2222-2222-000000000314', v_ge, v_se2, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('b2222222-2222-2222-2222-000000000315', v_gf, v_sf1, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('b2222222-2222-2222-2222-000000000316', v_gf, v_sf2, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('b2222222-2222-2222-2222-000000000317', v_gg, v_sg1, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('b2222222-2222-2222-2222-000000000318', v_gg, v_sg2, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days')
    ON CONFLICT (id) DO NOTHING;

    -- Final Reports & Submission Files
    INSERT INTO thesis.final_report (id, group_id, version_type, current_version_no, status, submitted_at) VALUES
    (v_frd, v_gd, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '7 days'),
    (v_fre, v_ge, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '7 days'),
    (v_frf, v_gf, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '7 days'),
    (v_frg, v_gg, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '7 days')
    ON CONFLICT (id) DO UPDATE SET status = EXCLUDED.status;

    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
    (v_sfd, 'FinalReport', v_frd, 1, 'demo/minutes/gd-final.pdf', 'KLTN-eBPF-LogAnomaly.pdf',     'application/pdf', 3850000, '.pdf', v_shad, v_sd1, NOW() - INTERVAL '7 days'),
    (v_sfe, 'FinalReport', v_fre, 1, 'demo/minutes/ge-final.pdf', 'KLTN-Federated-Medical.pdf',   'application/pdf', 3620000, '.pdf', v_shae, v_se1, NOW() - INTERVAL '7 days'),
    (v_sff, 'FinalReport', v_frf, 1, 'demo/minutes/gf-final.pdf', 'KLTN-AWQ-SLM-Inference.pdf',   'application/pdf', 4290000, '.pdf', v_shaf, v_sf1, NOW() - INTERVAL '7 days'),
    (v_sfg, 'FinalReport', v_frg, 1, 'demo/minutes/gg-final.pdf', 'KLTN-Android-MalwareGNN.pdf',  'application/pdf', 3940000, '.pdf', v_shag, v_sg1, NOW() - INTERVAL '7 days')
    ON CONFLICT (id) DO NOTHING;

    -- Advisor Evaluations (valid non-legacy records)
    INSERT INTO councils.advisor_evaluation (
        id, group_id, score, comment, evaluated_at, allows_reviewer_assignment,
        decision_at, decision_by, evaluated_by, final_report_id, is_legacy,
        report_version_no, submission_checksum_sha256, submission_file_id
    ) VALUES
    (v_adv_d, v_gd, 8.50, 'Nhóm triển khai eBPF ringbuf ổn định, benchmark độ trễ dưới 12ms.', NOW() - INTERVAL '6 days', true, NOW() - INTERVAL '6 days', v_gv_d_id, v_gv_d_id, v_frd, false, 1, v_shad, v_sfd),
    (v_adv_e, v_ge, 8.50, 'Mô hình FedAvg hội tụ tốt trên dữ liệu non-IID, đảm bảo ngân sách riêng tư.', NOW() - INTERVAL '6 days', true, NOW() - INTERVAL '6 days', v_gv_d_id, v_gv_d_id, v_fre, false, 1, v_shae, v_sfe),
    (v_adv_f, v_gf, 9.00, 'Tối ưu kernel CUDA đạt thông lượng cao gấp 2.4 lần FP16 baseline.', NOW() - INTERVAL '6 days', true, NOW() - INTERVAL '6 days', v_gv_d_id, v_gv_d_id, v_frf, false, 1, v_shaf, v_sff),
    (v_adv_g, v_gg, 8.00, 'Pipeline trích xuất đặc trưng đồ thị rõ ràng, thực nghiệm đa bộ dữ liệu.', NOW() - INTERVAL '6 days', true, NOW() - INTERVAL '6 days', v_gv_d_id, v_gv_d_id, v_frg, false, 1, v_shag, v_sfg)
    ON CONFLICT (id) DO NOTHING;

    -- Reviewer Assignments (Note: Group E assigns Lê Quang C — who is ALSO Council Member of HĐ-UIT-02!)
    INSERT INTO councils.reviewer_assignment (
        id, group_id, reviewer_id, assigned_by, assigned_at, assignment_order, is_active,
        advisor_evaluation_id, advisor_id_at_assignment, final_report_id, is_legacy,
        report_version_no, submission_checksum_sha256, submission_file_id
    ) VALUES
    (v_ra_d, v_gd, v_gv_e_id, v_admin_id, NOW() - INTERVAL '5 days', 1, true, v_adv_d, v_gv_d_id, v_frd, false, 1, v_shad, v_sfd),
    (v_ra_e, v_ge, v_gv_c_id, v_admin_id, NOW() - INTERVAL '5 days', 1, true, v_adv_e, v_gv_d_id, v_fre, false, 1, v_shae, v_sfe),
    (v_ra_f, v_gf, v_gv_e_id, v_admin_id, NOW() - INTERVAL '5 days', 1, true, v_adv_f, v_gv_d_id, v_frf, false, 1, v_shaf, v_sff),
    (v_ra_g, v_gg, v_gv_e_id, v_admin_id, NOW() - INTERVAL '5 days', 1, true, v_adv_g, v_gv_d_id, v_frg, false, 1, v_shag, v_sfg)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.reviewer_evaluation (
        id, assignment_id, score, comment, defense_approval, is_visible_to_student,
        evaluated_at, created_at_utc, created_by, evaluated_by, is_legacy,
        status, submitted_at_utc, submitted_by
    ) VALUES
    (v_re_d, v_ra_d, 8.25, 'Kiến trúc collector gọn nhẹ, cần bổ sung kiểm thử chịu tải khi burst log.', 'Approved', true, NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days', v_gv_e_id, v_gv_e_id, false, 'Submitted', NOW() - INTERVAL '4 days', v_gv_e_id),
    (v_re_e, v_ra_e, 8.75, 'Phân tích trade-off giữa epsilon và độ chính xác thuyết phục, đồng ý bảo vệ.', 'Approved', true, NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days', v_gv_c_id, v_gv_c_id, false, 'Submitted', NOW() - INTERVAL '4 days', v_gv_c_id),
    (v_re_f, v_ra_f, 8.75, 'Số liệu benchmark perplexity và tokens/s đầy đủ, đề nghị hội đồng khen thưởng.', 'Approved', true, NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days', v_gv_e_id, v_gv_e_id, false, 'Submitted', NOW() - INTERVAL '4 days', v_gv_e_id),
    (v_re_g, v_ra_g, 7.75, 'Kết quả phát hiện đạt F1 94.2%, cần làm rõ cơ chế xử lý obfuscation.', 'Approved', true, NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days', v_gv_e_id, v_gv_e_id, false, 'Submitted', NOW() - INTERVAL '4 days', v_gv_e_id)
    ON CONFLICT (id) DO NOTHING;

    -- Active Council HĐ-UIT-02
    INSERT INTO councils.council (
        id, batch_id, council_code, defense_date, time_slot, room, status, decision_number, signed_date, approved_by, approved_at, is_submitted_to_academic_affairs
    ) VALUES (
        v_council_02, v_batch_open, 'HĐ-UIT-02', CURRENT_DATE + 1, '13:30 - 17:00 (Chiều)', 'E3.2', 'Active', '145/QĐ-ĐHCNTT', CURRENT_DATE, v_admin_id, NOW() - INTERVAL '3 days', false
    ) ON CONFLICT (id) DO UPDATE
      SET status = 'Active',
          defense_date = EXCLUDED.defense_date,
          room = EXCLUDED.room,
          time_slot = EXCLUDED.time_slot;

    INSERT INTO councils.council_member (id, council_id, user_id, role_in_council) VALUES
    (v_cm2_chair, v_council_02, v_gv_khang, 'Chairman'),
    (v_cm2_sec,   v_council_02, v_gv_a_id,  'Secretary'),
    (v_cm2_mem,   v_council_02, v_gv_c_id,  'Member')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.council_assignment (id, council_id, group_id, order_number) VALUES
    (v_ca_d, v_council_02, v_gd, 1),
    (v_ca_e, v_council_02, v_ge, 2),
    (v_ca_f, v_council_02, v_gf, 3),
    (v_ca_g, v_council_02, v_gg, 4)
    ON CONFLICT (id) DO NOTHING;

    -- Council Scores
    -- Group D (Discrepancy >= 2.0: 8.75 vs 6.25 -> diff 2.50!)
    INSERT INTO councils.council_score (id, council_assignment_id, council_member_id, score, feedback, evaluated_at, is_ignored_in_calculation) VALUES
    ('b2222222-2222-2222-2222-000000000801', v_ca_d, v_cm2_chair, 8.75, 'Hệ thống telemetry xử lý thời gian thực tốt, trả lời phản biện rõ.', NOW() - INTERVAL '18 hours', false),
    ('b2222222-2222-2222-2222-000000000802', v_ca_d, v_cm2_sec,   8.50, 'Kiến trúc Stream Processing hợp lý, demo trực quan.', NOW() - INTERVAL '18 hours', false),
    ('b2222222-2222-2222-2222-000000000803', v_ca_d, v_cm2_mem,   6.25, 'Phần đánh giá false-positive trên tập dữ liệu thực tế còn mỏng.', NOW() - INTERVAL '17 hours', false),

    -- Group E (Dual-role Reviewer Lê Quang C -> Chỉ tham dự in CouncilMember!)
    ('b2222222-2222-2222-2222-000000000804', v_ca_e, v_cm2_chair, 8.50, 'Giải pháp bảo vệ riêng tư dữ liệu y tế chặt chẽ.', NOW() - INTERVAL '16 hours', false),
    ('b2222222-2222-2222-2222-000000000805', v_ca_e, v_cm2_sec,   8.75, 'Thuyết trình mạch lạc, kết quả thực nghiệm đáng tin cậy.', NOW() - INTERVAL '16 hours', false),
    ('b2222222-2222-2222-2222-000000000806', v_ca_e, v_cm2_mem,   8.75, 'Đã chấm ở vai trò GVPB, tham dự ghi nhận thảo luận Hội đồng.', NOW() - INTERVAL '16 hours', false),

    -- Group F (Locked)
    ('b2222222-2222-2222-2222-000000000807', v_ca_f, v_cm2_chair, 9.00, 'Xuất sắc cả về lý thuyết lượng tử hóa và tối ưu CUDA.', NOW() - INTERVAL '1 day', false),
    ('b2222222-2222-2222-2222-000000000808', v_ca_f, v_cm2_sec,   8.75, 'Báo cáo chuẩn mực, tái lập được kết quả benchmark.', NOW() - INTERVAL '1 day', false),
    ('b2222222-2222-2222-2222-000000000809', v_ca_f, v_cm2_mem,   8.50, 'Sản phẩm chạy mượt trên RTX 3060 như cam kết.', NOW() - INTERVAL '1 day', false),

    -- Group G (UnlockedByOverride)
    ('b2222222-2222-2222-2222-000000000810', v_ca_g, v_cm2_chair, 8.25, 'Biểu diễn đồ thị lời gọi API hiệu quả.', NOW() - INTERVAL '2 days', false),
    ('b2222222-2222-2222-2222-000000000811', v_ca_g, v_cm2_sec,   8.00, 'Đã rà soát lại phần trả lời câu hỏi theo biên bản phúc khảo.', NOW() - INTERVAL '2 days', false),
    ('b2222222-2222-2222-2222-000000000812', v_ca_g, v_cm2_mem,   8.00, 'Cần bổ sung phụ lục về giới hạn giải pháp khi mã bị đóng gói.', NOW() - INTERVAL '2 days', false)
    ON CONFLICT (id) DO UPDATE
      SET score = EXCLUDED.score,
          feedback = EXCLUDED.feedback,
          is_ignored_in_calculation = false,
          exclusion_reason = NULL;

    -- Council Minutes (Open, AllScoresEntered, Locked, UnlockedByOverride)
    INSERT INTO councils.council_minute (
        id, council_assignment_id, general_feedback, lock_status,
        locked_at, locked_by, override_reason, unlocked_at, unlocked_by
    ) VALUES
    (
        'b2222222-2222-2222-2222-000000000901',
        v_ca_d,
        '- Bổ sung thêm bảng so sánh tỷ lệ báo động giả (false-positive) trên tập log thật trước khi nộp lưu chiểu.' || E'\n' || '- Chuẩn hóa lại phụ lục cấu hình Prometheus & Kafka.',
        'Open',
        NULL, NULL, NULL, NULL, NULL
    ),
    (
        'b2222222-2222-2222-2222-000000000902',
        v_ca_e,
        'Hội đồng thống nhất đánh giá cao đóng góp nghiên cứu. Sinh viên hoàn thiện chỉnh sửa trích dẫn IEEE theo góp ý.',
        'AllScoresEntered',
        NULL, NULL, NULL, NULL, NULL
    ),
    (
        'b2222222-2222-2222-2222-000000000903',
        v_ca_f,
        'Nhóm bảo vệ xuất sắc. Cần bổ sung hướng dẫn tái lập môi trường Docker trong kho lưu trữ mã nguồn trước khi lưu chiểu.',
        'Locked',
        NOW() - INTERVAL '6 hours', v_gv_a_id, NULL, NULL, NULL
    ),
    (
        'b2222222-2222-2222-2222-000000000904',
        v_ca_g,
        'Nhóm cập nhật thêm phần thảo luận về giới hạn của mô hình khi ứng dụng kỹ thuật làm rối mã nguồn (obfuscation).',
        'UnlockedByOverride',
        NULL, NULL,
        'Mở khóa theo đơn đề nghị rà soát điểm số 09/ĐN-KTPM để Thư ký và Hội đồng cập nhật lại nhận xét tiêu chí 3.3.',
        NOW() - INTERVAL '2 hours', v_faculty_id
    )
    ON CONFLICT (id) DO UPDATE
      SET general_feedback = EXCLUDED.general_feedback,
          lock_status = EXCLUDED.lock_status,
          locked_at = EXCLUDED.locked_at,
          locked_by = EXCLUDED.locked_by,
          override_reason = EXCLUDED.override_reason,
          unlocked_at = EXCLUDED.unlocked_at,
          unlocked_by = EXCLUDED.unlocked_by;
END $$;
'@

$seedSql.Replace('__DEMO_PWD_HASH__', $demoPasswordHash) | & $psql -v ON_ERROR_STOP=1 -f -
if ($LASTEXITCODE -ne 0) { throw "Failed to seed council minute demo data (exit $LASTEXITCODE)." }

Write-Host 'Seeded Council Minutes demo data (HĐ-UIT-02: 4 scenarios) cleanly!' -ForegroundColor Green
Write-Host "Dev accounts password set to: $demoPasswordPlain"
Write-Host ' - Faculty (Override Unlock): truongkhoa@ugtm.edu.vn   (FACULTY01)'
Write-Host ' - Secretary (Edit + Lock):   gv.nguyenvana@ugtm.edu.vn (GV0001)'
Write-Host ' - Chairman (View):           gv.test.khang@ugtm.edu.vn (GV_TEST_01)'
Write-Host ' - Member/DualRole (View):    gv.lequangc@ugtm.edu.vn   (GV0002)'
Write-Host ' - Admin (Full Access):       admin@ugtm.edu.vn         (ADMIN001)'
