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
DELETE FROM councils.council_minute
WHERE id::text LIKE 'd4444444-4444-4444-4444-%'
   OR council_assignment_id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM councils.council_score
WHERE id::text LIKE 'd4444444-4444-4444-4444-%'
   OR council_assignment_id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM councils.council_assignment WHERE id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM councils.council_member WHERE id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM councils.council WHERE id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM councils.reviewer_evaluation
WHERE id::text LIKE 'd4444444-4444-4444-4444-%'
   OR assignment_id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM councils.reviewer_assignment WHERE id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM councils.advisor_evaluation
WHERE id::text LIKE 'd4444444-4444-4444-4444-%'
   OR group_id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM thesis.submission_file WHERE id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM thesis.final_report WHERE id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM thesis.student_group_member WHERE id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM thesis.student_group WHERE id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM topics.topic WHERE id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM identity.user_role WHERE user_id::text LIKE 'd4444444-4444-4444-4444-%';
DELETE FROM identity."user" WHERE id::text LIKE 'd4444444-4444-4444-4444-%';
"@
    $resetSql | & $psql -v ON_ERROR_STOP=1 -f -
    if ($LASTEXITCODE -ne 0) { throw "Failed to reset council approval demo data (exit $LASTEXITCODE)." }
    Write-Host 'Reset completed: removed council approval demo records (d4444444-4444-4444-4444-*).' -ForegroundColor Yellow
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
    v_gv_a_id    UUID := '22222222-2222-2222-2222-222222222222'; -- GV0001
    v_gv_c_id    UUID := '44444444-4444-4444-4444-444444444444'; -- GV0002
    v_gv_khang   UUID := '77777777-7777-7777-7777-777777777701'; -- GV_TEST_01 TS. Phạm Minh Khang
    v_gv_d_id    UUID := 'a1111111-1111-1111-1111-000000000093'; -- GV0003 TS. Trần Minh Đức
    v_gv_e_id    UUID := 'a1111111-1111-1111-1111-000000000094'; -- GV0004 PGS.TS. Đặng Hoàng Minh
    v_faculty_id UUID := 'b2222222-2222-2222-2222-000000000099'; -- FACULTY01
    v_pdt_id     UUID := 'd4444444-4444-4444-4444-000000000098'; -- PDT01 AcademicAffairs

    v_role_student INT;
    v_role_aa      INT;
    v_role_lecturer INT;
    v_role_council INT;

    -- 8 students across 4 groups (H, I, J, K)
    v_sh1 UUID := 'd4444444-4444-4444-4444-000000000101';
    v_sh2 UUID := 'd4444444-4444-4444-4444-000000000102';
    v_si1 UUID := 'd4444444-4444-4444-4444-000000000103';
    v_si2 UUID := 'd4444444-4444-4444-4444-000000000104';
    v_sj1 UUID := 'd4444444-4444-4444-4444-000000000105';
    v_sj2 UUID := 'd4444444-4444-4444-4444-000000000106';
    v_sk1 UUID := 'd4444444-4444-4444-4444-000000000107';
    v_sk2 UUID := 'd4444444-4444-4444-4444-000000000108';

    -- Topics (H, I advisor = TS. Phạm Minh Khang; J, K advisor = TS. Trần Minh Đức)
    v_th UUID := 'd4444444-4444-4444-4444-000000000201';
    v_ti UUID := 'd4444444-4444-4444-4444-000000000202';
    v_tj UUID := 'd4444444-4444-4444-4444-000000000203';
    v_tk UUID := 'd4444444-4444-4444-4444-000000000204';

    -- Groups
    v_gh UUID := 'd4444444-4444-4444-4444-000000000301';
    v_gi UUID := 'd4444444-4444-4444-4444-000000000302';
    v_gj UUID := 'd4444444-4444-4444-4444-000000000303';
    v_gk UUID := 'd4444444-4444-4444-4444-000000000304';

    -- Final Reports & Submission Files
    v_frh UUID := 'd4444444-4444-4444-4444-000000000401';
    v_fri UUID := 'd4444444-4444-4444-4444-000000000402';
    v_frj UUID := 'd4444444-4444-4444-4444-000000000403';
    v_frk UUID := 'd4444444-4444-4444-4444-000000000404';

    v_sfh UUID := 'd4444444-4444-4444-4444-000000000411';
    v_sfi UUID := 'd4444444-4444-4444-4444-000000000412';
    v_sfj UUID := 'd4444444-4444-4444-4444-000000000413';
    v_sfk UUID := 'd4444444-4444-4444-4444-000000000414';

    v_shah TEXT := repeat('1', 64);
    v_shai TEXT := repeat('2', 64);
    v_shaj TEXT := repeat('3', 64);
    v_shak TEXT := repeat('4', 64);

    -- Advisor & Reviewer evaluations
    v_adv_h UUID := 'd4444444-4444-4444-4444-000000000501';
    v_adv_i UUID := 'd4444444-4444-4444-4444-000000000502';
    v_adv_j UUID := 'd4444444-4444-4444-4444-000000000503';
    v_adv_k UUID := 'd4444444-4444-4444-4444-000000000504';

    v_ra_h UUID := 'd4444444-4444-4444-4444-000000000511';
    v_ra_i UUID := 'd4444444-4444-4444-4444-000000000512';
    v_ra_j UUID := 'd4444444-4444-4444-4444-000000000513';
    v_ra_k UUID := 'd4444444-4444-4444-4444-000000000514';

    v_re_h UUID := 'd4444444-4444-4444-4444-000000000521';
    v_re_i UUID := 'd4444444-4444-4444-4444-000000000522';
    v_re_j UUID := 'd4444444-4444-4444-4444-000000000523';
    v_re_k UUID := 'd4444444-4444-4444-4444-000000000524';

    v_eval_ts TIMESTAMPTZ := NOW() - INTERVAL '4 days';

    -- 5 Councils covering all approval scenarios
    v_c05 UUID := 'd4444444-4444-4444-4444-000000000705'; -- Ready Draft (Khoa đã nộp, 2 nhóm)
    v_c06 UUID := 'd4444444-4444-4444-4444-000000000706'; -- Ready Draft (GVPB kiêm UV)
    v_c07 UUID := 'd4444444-4444-4444-4444-000000000707'; -- Draft (Chờ Khoa nộp lên PĐT)
    v_c08 UUID := 'd4444444-4444-4444-4444-000000000708'; -- Blocked Draft (Trùng phòng & thiếu Thư ký)
    v_c09 UUID := 'd4444444-4444-4444-4444-000000000709'; -- Active (Đã phê duyệt, có số QĐ & PDF URL)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM academic.kltn_batch WHERE id = v_batch_open) THEN
        RAISE EXCEPTION 'Expected Open batch % not found. Run app migration/seed first.', v_batch_open;
    END IF;

    SELECT id INTO v_role_student  FROM identity.role WHERE role_name = 'Student' LIMIT 1;
    SELECT id INTO v_role_aa       FROM identity.role WHERE role_name = 'AcademicAffairs' LIMIT 1;
    SELECT id INTO v_role_lecturer FROM identity.role WHERE role_name = 'Lecturer' LIMIT 1;
    SELECT id INTO v_role_council  FROM identity.role WHERE role_name = 'CouncilMember' LIMIT 1;

    -- Ensure lecturers exist and passwords match demo password
    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    (v_gv_d_id, 'GV0003', 'TS. Trần Minh Đức',      'gv.tranminhduc@ugtm.edu.vn',   'GV.TRANMINHDUC@UGTM.EDU.VN',   v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_gv_e_id, 'GV0004', 'PGS.TS. Đặng Hoàng Minh', 'gv.danghoangminh@ugtm.edu.vn', 'GV.DANGHOANGMINH@UGTM.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_pdt_id,  'PDT01',  'ThS. Lê Minh Tâm',        'pdt@ugtm.edu.vn',              'PDT@UGTM.EDU.VN',              v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO UPDATE
      SET password_hash = EXCLUDED.password_hash,
          status = 'Active',
          access_failed_count = 0;

    INSERT INTO identity.user_role (user_id, role_id, assigned_at)
    SELECT u_id, r_id, NOW()
    FROM (VALUES
        (v_gv_d_id,  v_role_lecturer),
        (v_gv_d_id,  v_role_council),
        (v_gv_e_id,  v_role_lecturer),
        (v_gv_e_id,  v_role_council),
        (v_pdt_id,   v_role_aa)
    ) AS v(u_id, r_id)
    WHERE NOT EXISTS (
        SELECT 1 FROM identity.user_role ur WHERE ur.user_id = v.u_id AND ur.role_id = v.r_id
    );

    -- Students for Groups H, I, J, K
    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    (v_sh1, '22520701', 'Nguyễn Tấn Tài',   '22520701@gm.uit.edu.vn', '22520701@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_sh2, '22520702', 'Lê Thị Thanh Tâm', '22520702@gm.uit.edu.vn', '22520702@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_si1, '22520703', 'Phạm Quốc Khánh',  '22520703@gm.uit.edu.vn', '22520703@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_si2, '22520704', 'Võ Ngọc Diệp',     '22520704@gm.uit.edu.vn', '22520704@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_sj1, '22520705', 'Trần Hoàng Long',  '22520705@gm.uit.edu.vn', '22520705@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_sj2, '22520706', 'Đặng Thị Mai Anh', '22520706@gm.uit.edu.vn', '22520706@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_sk1, '22520707', 'Huỳnh Nhật Hào',   '22520707@gm.uit.edu.vn', '22520707@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_sk2, '22520708', 'Bùi Thảo Nhi',     '22520708@gm.uit.edu.vn', '22520708@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO identity.user_role (user_id, role_id, assigned_at)
    SELECT s_id, v_role_student, NOW()
    FROM (VALUES (v_sh1),(v_sh2),(v_si1),(v_si2),(v_sj1),(v_sj2),(v_sk1),(v_sk2)) AS t(s_id)
    WHERE NOT EXISTS (
        SELECT 1 FROM identity.user_role ur WHERE ur.user_id = t.s_id AND ur.role_id = v_role_student
    );

    -- Topics
    INSERT INTO topics.topic (
        id, batch_id, advisor_id, title_vi, title_en, description, requirements, status, student_number, created_at
    ) VALUES
    (v_th, v_batch_open, v_gv_khang, 'Nền tảng phát hiện gian lận giao dịch thời gian thực trên Graph Streaming', 'Real-Time Transaction Fraud Detection on Graph Streaming', 'Xây dựng pipeline phát hiện bất thường đồ thị giao dịch với độ trễ thấp.', 'Kafka, Neo4j, PyTorch Geometric', 'Approved', 2, NOW() - INTERVAL '45 days'),
    (v_ti, v_batch_open, v_gv_khang, 'Trợ lý tra cứu quy chế học vụ đa phương thức cho sinh viên đại học', 'Multimodal Academic Regulation Assistant for University Students', 'Kết hợp RAG lai và OCR bảng biểu văn bản hành chính.', 'LangGraph, Qdrant, PaddleOCR', 'Approved', 2, NOW() - INTERVAL '45 days'),
    (v_tj, v_batch_open, v_gv_d_id,  'Hệ thống giám sát tuân thủ bảo mật Kubernetes theo chuẩn CIS Benchmark', 'Kubernetes Security Compliance Monitoring per CIS Benchmark', 'Tự động quét cấu hình cluster và đề xuất bản vá theo chính sách.', 'Go, eBPF, OPA Gatekeeper', 'Approved', 2, NOW() - INTERVAL '45 days'),
    (v_tk, v_batch_open, v_gv_d_id,  'Tối ưu lập lịch tài nguyên GPU đa người dùng trên cụm suy luận LLM', 'Multi-Tenant GPU Resource Scheduling for LLM Inference Clusters', 'Cân bằng tải động theo độ dài ngữ cảnh và SLA phản hồi.', 'Triton, vLLM, Prometheus', 'Approved', 2, NOW() - INTERVAL '45 days')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.student_group (
        id, topic_id, status, created_at, allow_syllabus_resubmission
    ) VALUES
    (v_gh, v_th, 'ReadyForReviewerAssignment', NOW() - INTERVAL '40 days', false),
    (v_gi, v_ti, 'ReadyForReviewerAssignment', NOW() - INTERVAL '40 days', false),
    (v_gj, v_tj, 'ReadyForReviewerAssignment', NOW() - INTERVAL '40 days', false),
    (v_gk, v_tk, 'ReadyForReviewerAssignment', NOW() - INTERVAL '40 days', false)
    ON CONFLICT (id) DO UPDATE SET status = EXCLUDED.status;

    INSERT INTO thesis.student_group_member (
        id, group_id, student_id, batch_id, joined_at, confirmation_status, confirmed_at_utc
    ) VALUES
    ('d4444444-4444-4444-4444-000000000311', v_gh, v_sh1, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('d4444444-4444-4444-4444-000000000312', v_gh, v_sh2, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('d4444444-4444-4444-4444-000000000313', v_gi, v_si1, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('d4444444-4444-4444-4444-000000000314', v_gi, v_si2, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('d4444444-4444-4444-4444-000000000315', v_gj, v_sj1, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('d4444444-4444-4444-4444-000000000316', v_gj, v_sj2, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('d4444444-4444-4444-4444-000000000317', v_gk, v_sk1, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days'),
    ('d4444444-4444-4444-4444-000000000318', v_gk, v_sk2, v_batch_open, NOW() - INTERVAL '39 days', 'Confirmed', NOW() - INTERVAL '39 days')
    ON CONFLICT (id) DO NOTHING;

    -- Final Reports & Submission Files
    INSERT INTO thesis.final_report (id, group_id, version_type, current_version_no, status, submitted_at) VALUES
    (v_frh, v_gh, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '7 days'),
    (v_fri, v_gi, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '7 days'),
    (v_frj, v_gj, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '7 days'),
    (v_frk, v_gk, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '7 days')
    ON CONFLICT (id) DO UPDATE SET status = EXCLUDED.status;

    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
    (v_sfh, 'FinalReport', v_frh, 1, 'demo/approval/gh-final.pdf', 'KLTN-Graph-Fraud.pdf',     'application/pdf', 3750000, '.pdf', v_shah, v_sh1, NOW() - INTERVAL '7 days'),
    (v_sfi, 'FinalReport', v_fri, 1, 'demo/approval/gi-final.pdf', 'KLTN-Academic-RAG.pdf',    'application/pdf', 3480000, '.pdf', v_shai, v_si1, NOW() - INTERVAL '7 days'),
    (v_sfj, 'FinalReport', v_frj, 1, 'demo/approval/gj-final.pdf', 'KLTN-K8s-Compliance.pdf',  'application/pdf', 4120000, '.pdf', v_shaj, v_sj1, NOW() - INTERVAL '7 days'),
    (v_sfk, 'FinalReport', v_frk, 1, 'demo/approval/gk-final.pdf', 'KLTN-GPU-Scheduler.pdf',   'application/pdf', 3990000, '.pdf', v_shak, v_sk1, NOW() - INTERVAL '7 days')
    ON CONFLICT (id) DO NOTHING;

    -- Advisor Evaluations
    INSERT INTO councils.advisor_evaluation (
        id, group_id, score, comment, evaluated_at, allows_reviewer_assignment,
        decision_at, decision_by, evaluated_by, final_report_id, is_legacy,
        report_version_no, submission_checksum_sha256, submission_file_id
    ) VALUES
    (v_adv_h, v_gh, 8.75, 'Hệ thống xử lý đồ thị luồng tốt, đáp ứng SLA thời gian thực.', NOW() - INTERVAL '6 days', true, NOW() - INTERVAL '6 days', v_gv_khang, v_gv_khang, v_frh, false, 1, v_shah, v_sfh),
    (v_adv_i, v_gi, 8.50, 'Tích hợp OCR bảng biểu quy chế rõ ràng, đánh giá truy hồi tốt.', NOW() - INTERVAL '6 days', true, NOW() - INTERVAL '6 days', v_gv_khang, v_gv_khang, v_fri, false, 1, v_shai, v_sfi),
    (v_adv_j, v_gj, 8.25, 'Bộ kiểm tra CIS Benchmark chạy tự động ổn định trên cụm test.',  NOW() - INTERVAL '6 days', true, NOW() - INTERVAL '6 days', v_gv_d_id,  v_gv_d_id,  v_frj, false, 1, v_shaj, v_sfj),
    (v_adv_k, v_gk, 9.00, 'Thuật toán lập lịch giảm tail-latency đáng kể khi tải cao.',    NOW() - INTERVAL '6 days', true, NOW() - INTERVAL '6 days', v_gv_d_id,  v_gv_d_id,  v_frk, false, 1, v_shak, v_sfk)
    ON CONFLICT (id) DO NOTHING;

    -- Reviewer Assignments (Note: Group J assigns PGS.TS. Đặng Hoàng Minh, who is Member in HĐ-UIT-06 -> GVPB kiêm UV)
    INSERT INTO councils.reviewer_assignment (
        id, group_id, reviewer_id, assigned_by, assigned_at, assignment_order, is_active,
        advisor_evaluation_id, advisor_id_at_assignment, final_report_id, is_legacy,
        report_version_no, submission_checksum_sha256, submission_file_id
    ) VALUES
    (v_ra_h, v_gh, v_gv_a_id, v_admin_id, NOW() - INTERVAL '5 days', 1, true, v_adv_h, v_gv_khang, v_frh, false, 1, v_shah, v_sfh),
    (v_ra_i, v_gi, v_gv_a_id, v_admin_id, NOW() - INTERVAL '5 days', 1, true, v_adv_i, v_gv_khang, v_fri, false, 1, v_shai, v_sfi),
    (v_ra_j, v_gj, v_gv_e_id, v_admin_id, NOW() - INTERVAL '5 days', 1, true, v_adv_j, v_gv_d_id,  v_frj, false, 1, v_shaj, v_sfj),
    (v_ra_k, v_gk, v_gv_e_id, v_admin_id, NOW() - INTERVAL '5 days', 1, true, v_adv_k, v_gv_d_id,  v_frk, false, 1, v_shak, v_sfk)
    ON CONFLICT (id) DO NOTHING;

    -- Reviewer Evaluations (all Approved & valid)
    INSERT INTO councils.reviewer_evaluation (
        id, assignment_id, score, comment, defense_approval, is_visible_to_student,
        evaluated_at, created_at_utc, created_by, evaluated_by, is_legacy,
        status, submitted_at_utc, submitted_by
    ) VALUES
    (v_re_h, v_ra_h, 8.50, 'Mô hình phát hiện gian lận rõ ràng, đồng ý cho bảo vệ trước Hội đồng.', 'Approved', true, v_eval_ts, v_eval_ts, v_gv_a_id, v_gv_a_id, false, 'Submitted', v_eval_ts, v_gv_a_id),
    (v_re_i, v_ra_i, 8.25, 'Kết quả thử nghiệm đạt yêu cầu, cần làm rõ thêm phần đánh giá độ trễ.', 'Approved', true, v_eval_ts, v_eval_ts, v_gv_a_id, v_gv_a_id, false, 'Submitted', v_eval_ts, v_gv_a_id),
    (v_re_j, v_ra_j, 8.50, 'Kiến trúc giám sát Kubernetes thực tế và hữu ích, đồng ý bảo vệ.',     'Approved', true, v_eval_ts, v_eval_ts, v_gv_e_id, v_gv_e_id, false, 'Submitted', v_eval_ts, v_gv_e_id),
    (v_re_k, v_ra_k, 8.75, 'Thực nghiệm lập lịch GPU bài bản, số liệu so sánh thuyết phục.',       'Approved', true, v_eval_ts, v_eval_ts, v_gv_e_id, v_gv_e_id, false, 'Submitted', v_eval_ts, v_gv_e_id)
    ON CONFLICT (id) DO NOTHING;

    -- 1) HĐ-UIT-05: Ready Draft (Submitted by Faculty, 3 members, 2 groups, 0 conflicts)
    INSERT INTO councils.council (
        id, batch_id, council_code, defense_date, time_slot, room, status,
        is_submitted_to_academic_affairs, submitted_at, submitted_by, submission_notes
    ) VALUES (
        v_c05, v_batch_open, 'HĐ-UIT-05', CURRENT_DATE + 10, '08:00 - 11:30 (Sáng)', 'B1.201', 'Draft',
        true, NOW() - INTERVAL '1 day', v_faculty_id,
        'Văn phòng Khoa CNTT đã đối chiếu lịch phòng B1.201, xác nhận không xung đột GVHD và kính chuyển Phòng Đào tạo phê duyệt.'
    ) ON CONFLICT (id) DO UPDATE
      SET status = 'Draft',
          defense_date = EXCLUDED.defense_date,
          time_slot = EXCLUDED.time_slot,
          room = EXCLUDED.room,
          is_submitted_to_academic_affairs = true,
          submitted_at = EXCLUDED.submitted_at,
          submission_notes = EXCLUDED.submission_notes,
          decision_number = NULL,
          signed_date = NULL,
          decision_file_url = NULL,
          approved_by = NULL,
          approved_at = NULL;

    -- 2) HĐ-UIT-06: Ready Draft (Submitted, GVPB kiêm UV model)
    INSERT INTO councils.council (
        id, batch_id, council_code, defense_date, time_slot, room, status,
        is_submitted_to_academic_affairs, submitted_at, submitted_by, submission_notes
    ) VALUES (
        v_c06, v_batch_open, 'HĐ-UIT-06', CURRENT_DATE + 10, '13:30 - 17:00 (Chiều)', 'B1.202', 'Draft',
        true, NOW() - INTERVAL '18 hours', v_faculty_id,
        'Áp dụng mô hình GVPB (PGS.TS. Đặng Hoàng Minh) kiêm Ủy viên Hội đồng theo quy chế.'
    ) ON CONFLICT (id) DO UPDATE
      SET status = 'Draft',
          defense_date = EXCLUDED.defense_date,
          time_slot = EXCLUDED.time_slot,
          room = EXCLUDED.room,
          is_submitted_to_academic_affairs = true,
          submitted_at = EXCLUDED.submitted_at,
          submission_notes = EXCLUDED.submission_notes,
          decision_number = NULL,
          signed_date = NULL,
          decision_file_url = NULL,
          approved_by = NULL,
          approved_at = NULL;

    -- 3) HĐ-UIT-07: Draft NOT yet submitted by Faculty
    INSERT INTO councils.council (
        id, batch_id, council_code, defense_date, time_slot, room, status,
        is_submitted_to_academic_affairs, submitted_at, submitted_by, submission_notes
    ) VALUES (
        v_c07, v_batch_open, 'HĐ-UIT-07', CURRENT_DATE + 11, '08:00 - 11:30 (Sáng)', 'B1.203', 'Draft',
        false, NULL, NULL, NULL
    ) ON CONFLICT (id) DO UPDATE
      SET status = 'Draft',
          defense_date = EXCLUDED.defense_date,
          time_slot = EXCLUDED.time_slot,
          room = EXCLUDED.room,
          is_submitted_to_academic_affairs = false;

    -- 4) HĐ-UIT-09: Already Approved (Active) with decision number, signed date & PDF URL
    INSERT INTO councils.council (
        id, batch_id, council_code, defense_date, time_slot, room, status,
        decision_number, signed_date, decision_file_url, approved_by, approved_at,
        is_submitted_to_academic_affairs, submitted_at, submitted_by, submission_notes
    ) VALUES (
        v_c09, v_batch_open, 'HĐ-UIT-09', CURRENT_DATE + 12, '08:00 - 11:30 (Sáng)', 'B1.204', 'Active',
        '168/QĐ-ĐHCNTT', CURRENT_DATE - 2, 'https://storage.ugtm.uit.edu.vn/decisions/168-QD-DHCNTT-HD-UIT-09.pdf',
        v_pdt_id, NOW() - INTERVAL '2 days',
        true, NOW() - INTERVAL '3 days', v_faculty_id,
        'Hội đồng chuyên đề Hệ thống phân tán & AI đã hoàn tất kiểm tra điều kiện.'
    ) ON CONFLICT (id) DO UPDATE
      SET status = 'Active',
          defense_date = EXCLUDED.defense_date,
          time_slot = EXCLUDED.time_slot,
          room = EXCLUDED.room,
          decision_number = EXCLUDED.decision_number,
          signed_date = EXCLUDED.signed_date,
          decision_file_url = EXCLUDED.decision_file_url;

    -- 5) HĐ-UIT-08: Blocked Draft (Missing Secretary + Room/Member conflict with Active HĐ-UIT-09)
    INSERT INTO councils.council (
        id, batch_id, council_code, defense_date, time_slot, room, status,
        is_submitted_to_academic_affairs, submitted_at, submitted_by, submission_notes
    ) VALUES (
        v_c08, v_batch_open, 'HĐ-UIT-08', CURRENT_DATE + 12, '08:00 - 11:30 (Sáng)', 'B1.204', 'Draft',
        true, NOW() - INTERVAL '5 hours', v_faculty_id,
        'Dự thảo thử nghiệm phát hiện trùng phòng B1.204 và thiếu Thư ký.'
    ) ON CONFLICT (id) DO UPDATE
      SET status = 'Draft',
          defense_date = EXCLUDED.defense_date,
          time_slot = EXCLUDED.time_slot,
          room = EXCLUDED.room,
          is_submitted_to_academic_affairs = true;

    -- Members (No advisor conflict on 05, 06, 07, 09)
    INSERT INTO councils.council_member (id, council_id, user_id, role_in_council) VALUES
    -- HĐ-UIT-05 (Groups H, I have advisor TS. Phạm Minh Khang -> NOT in HĐ-UIT-05)
    ('d4444444-4444-4444-4444-000000000711', v_c05, v_gv_d_id, 'Chairman'),
    ('d4444444-4444-4444-4444-000000000712', v_c05, v_gv_e_id, 'Secretary'),
    ('d4444444-4444-4444-4444-000000000713', v_c05, v_gv_c_id, 'Member'),
    -- HĐ-UIT-06 (Group J has advisor TS. Trần Minh Đức, reviewer PGS.TS. Đặng Hoàng Minh)
    ('d4444444-4444-4444-4444-000000000714', v_c06, v_gv_khang, 'Chairman'),
    ('d4444444-4444-4444-4444-000000000715', v_c06, v_gv_a_id,  'Secretary'),
    ('d4444444-4444-4444-4444-000000000716', v_c06, v_gv_e_id,  'Member'),
    -- HĐ-UIT-07 (Group K has advisor TS. Trần Minh Đức)
    ('d4444444-4444-4444-4444-000000000717', v_c07, v_gv_khang, 'Chairman'),
    ('d4444444-4444-4444-4444-000000000718', v_c07, v_gv_a_id,  'Secretary'),
    ('d4444444-4444-4444-4444-000000000719', v_c07, v_gv_c_id,  'Member'),
    -- HĐ-UIT-09 (Active, uses same date/slot/room as HĐ-UIT-08)
    ('d4444444-4444-4444-4444-000000000720', v_c09, v_gv_khang, 'Chairman'),
    ('d4444444-4444-4444-4444-000000000721', v_c09, v_gv_a_id,  'Secretary'),
    ('d4444444-4444-4444-4444-000000000722', v_c09, v_gv_c_id,  'Member'),
    -- HĐ-UIT-08 (Missing Secretary, Chairman TS. Phạm Minh Khang conflicts with HĐ-UIT-09)
    ('d4444444-4444-4444-4444-000000000723', v_c08, v_gv_khang, 'Chairman')
    ON CONFLICT (id) DO NOTHING;

    -- Assignments (each group assigned to at most 1 council)
    INSERT INTO councils.council_assignment (id, council_id, group_id, order_number) VALUES
    ('d4444444-4444-4444-4444-000000000731', v_c05, v_gh, 1),
    ('d4444444-4444-4444-4444-000000000732', v_c05, v_gi, 2),
    ('d4444444-4444-4444-4444-000000000733', v_c06, v_gj, 1),
    ('d4444444-4444-4444-4444-000000000734', v_c07, v_gk, 1)
    ON CONFLICT (id) DO NOTHING;
END $$;
'@

$seedSql.Replace('__DEMO_PWD_HASH__', $demoPasswordHash) | & $psql -v ON_ERROR_STOP=1 -f -
if ($LASTEXITCODE -ne 0) { throw "Failed to seed council approval demo data (exit $LASTEXITCODE)." }

Write-Host 'Seeded Council Approval demo data (HĐ-UIT-05..09) cleanly!' -ForegroundColor Green
Write-Host "Dev accounts password set to: $demoPasswordPlain"
Write-Host ' - AcademicAffairs (Approve): pdt@ugtm.edu.vn         (PDT01)'
Write-Host ' - Admin (Full Access):       admin@ugtm.edu.vn       (ADMIN001)'
Write-Host ' - Faculty (View):            truongkhoa@ugtm.edu.vn  (FACULTY01)'
