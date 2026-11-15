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
    SELECT id FROM councils.score_summary WHERE group_id::text LIKE 'a1111111-1111-1111-1111-%'
);
DELETE FROM councils.score_summary_revision WHERE group_id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM councils.score_summary WHERE group_id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM councils.evaluation_detail
WHERE id::text LIKE 'a1111111-1111-1111-1111-%'
   OR evaluation_id::text LIKE 'a1111111-1111-1111-1111-%'
   OR criteria_id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM councils.council_minute
WHERE id::text LIKE 'a1111111-1111-1111-1111-%'
   OR council_assignment_id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM councils.council_score
WHERE id::text LIKE 'a1111111-1111-1111-1111-%'
   OR council_assignment_id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM councils.council_assignment WHERE id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM councils.council_member WHERE id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM councils.council WHERE id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM councils.reviewer_evaluation
WHERE id::text LIKE 'a1111111-1111-1111-1111-%'
   OR assignment_id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM councils.reviewer_assignment WHERE id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM councils.advisor_evaluation
WHERE id::text LIKE 'a1111111-1111-1111-1111-%'
   OR group_id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM councils.grading_criteria WHERE id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM thesis.submission_file WHERE id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM thesis.final_report WHERE id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM thesis.student_group_member WHERE id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM thesis.student_group WHERE id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM topics.topic WHERE id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM identity.user_role WHERE user_id::text LIKE 'a1111111-1111-1111-1111-%';
DELETE FROM identity."user" WHERE id::text LIKE 'a1111111-1111-1111-1111-%';
"@
    $resetSql | & $psql -v ON_ERROR_STOP=1 -f -
    if ($LASTEXITCODE -ne 0) { throw "Failed to reset grading demo data (exit $LASTEXITCODE)." }
    Write-Host 'Reset completed: removed grading demo records (a1111111-1111-1111-1111-*).' -ForegroundColor Yellow
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
    $prfBytes = [BitConverter]::GetBytes([uint32]2)
    $iterBytes = [BitConverter]::GetBytes([uint32]$iterations)
    $saltLenBytes = [BitConverter]::GetBytes([uint32]$salt.Length)
    if ([BitConverter]::IsLittleEndian) {
        [Array]::Reverse($prfBytes)
        [Array]::Reverse($iterBytes)
        [Array]::Reverse($saltLenBytes)
    }
    [Array]::Copy($prfBytes, 0, $output, 1, 4)
    [Array]::Copy($iterBytes, 0, $output, 5, 4)
    [Array]::Copy($saltLenBytes, 0, $output, 9, 4)
    [Array]::Copy($salt, 0, $output, 13, $salt.Length)
    [Array]::Copy($subkey, 0, $output, 13 + $salt.Length, $subkey.Length)
    return [Convert]::ToBase64String($output)
}

$demoPasswordPlain = 'UgtmDemo@2026'
$demoPasswordHash = Get-AspNetIdentityV3Hash $demoPasswordPlain

$seedSql = @'
DO $$
DECLARE
    v_pwd_hash text := '__DEMO_PWD_HASH__';
    v_batch_open uuid := 'ad3bc9b1-0163-4a99-a711-ecb8bdc0400b';
    v_batch_hk2  uuid := 'e1a10001-0000-0000-0000-000000000002';

    v_admin_id   uuid := '11111111-1111-1111-1111-111111111111';
    v_gv_a_id    uuid := '22222222-2222-2222-2222-222222222222'; -- gv.nguyenvana@ugtm.edu.vn
    v_gv_c_id    uuid := '44444444-4444-4444-4444-444444444444'; -- gv.lequangc@ugtm.edu.vn
    v_gv_khang   uuid := '77777777-7777-7777-7777-777777777701'; -- gv.test.khang@ugtm.edu.vn
    v_gv_d_id    uuid := 'a1111111-1111-1111-1111-000000000093'; -- gv.tranminhduc@ugtm.edu.vn
    v_gv_e_id    uuid := 'a1111111-1111-1111-1111-000000000094'; -- gv.danghoangminh@ugtm.edu.vn

    v_role_student integer;
    v_role_council integer;
    v_role_lecturer integer;

    -- 6 demo students across 3 per-student grading groups
    v_s1 uuid := 'a1111111-1111-1111-1111-000000000301';
    v_s2 uuid := 'a1111111-1111-1111-1111-000000000302';
    v_s3 uuid := 'a1111111-1111-1111-1111-000000000303';
    v_s4 uuid := 'a1111111-1111-1111-1111-000000000304';
    v_s5 uuid := 'a1111111-1111-1111-1111-000000000305';
    v_s6 uuid := 'a1111111-1111-1111-1111-000000000306';

    -- Topics & Groups
    v_t1 uuid := 'a1111111-1111-1111-1111-000000000101';
    v_t2 uuid := 'a1111111-1111-1111-1111-000000000102';
    v_t3 uuid := 'a1111111-1111-1111-1111-000000000103';

    v_g1 uuid := 'a1111111-1111-1111-1111-000000000201';
    v_g2 uuid := 'a1111111-1111-1111-1111-000000000202';
    v_g3 uuid := 'a1111111-1111-1111-1111-000000000203';

    v_fr1 uuid := 'a1111111-1111-1111-1111-000000000401';
    v_fr2 uuid := 'a1111111-1111-1111-1111-000000000402';
    v_fr3 uuid := 'a1111111-1111-1111-1111-000000000403';

    v_sf1 uuid := 'a1111111-1111-1111-1111-000000000451';
    v_sf2 uuid := 'a1111111-1111-1111-1111-000000000452';
    v_sf3 uuid := 'a1111111-1111-1111-1111-000000000453';

    v_sha1 text := repeat('1', 64);
    v_sha2 text := repeat('2', 64);
    v_sha3 text := repeat('3', 64);

    -- Criteria IDs in active open batch (ad3bc9b1...)
    v_c11 uuid := 'a1111111-1111-1111-1111-000000000501';
    v_c12 uuid := 'a1111111-1111-1111-1111-000000000502';
    v_c21 uuid := 'a1111111-1111-1111-1111-000000000503';
    v_c22 uuid := 'a1111111-1111-1111-1111-000000000504';
    v_c23 uuid := 'a1111111-1111-1111-1111-000000000505';
    v_c31 uuid := 'a1111111-1111-1111-1111-000000000506';
    v_c32 uuid := 'a1111111-1111-1111-1111-000000000507';
    v_c33 uuid := 'a1111111-1111-1111-1111-000000000508';
    v_c41 uuid := 'a1111111-1111-1111-1111-000000000509';
    v_c42 uuid := 'a1111111-1111-1111-1111-000000000510';

    -- Advisor & Reviewer records
    v_adv_eval_g2 uuid := 'a1111111-1111-1111-1111-000000000602';
    v_rev_assign_g2 uuid := 'a1111111-1111-1111-1111-000000000622';

    v_adv_eval_g3 uuid := 'a1111111-1111-1111-1111-000000000603';
    v_rev_assign_g3 uuid := 'a1111111-1111-1111-1111-000000000623';
    v_rev_eval_g3 uuid := 'a1111111-1111-1111-1111-000000000633';

    -- Active Council & assignments in Đợt KLTN Thử Nghiệm
    v_council_id uuid := 'a1111111-1111-1111-1111-000000000701';
    v_cm_chair   uuid := 'a1111111-1111-1111-1111-000000000711';
    v_cm_sec     uuid := 'a1111111-1111-1111-1111-000000000712';
    v_cm_mem     uuid := 'a1111111-1111-1111-1111-000000000713';

    v_ca_g3      uuid := 'a1111111-1111-1111-1111-000000000721';
    v_ca_9999    uuid := 'a1111111-1111-1111-1111-000000000722';
    v_group_9999 uuid := '99999999-9999-9999-9999-000000000201';

    v_cs_chair_g3 uuid := 'a1111111-1111-1111-1111-000000000731';
    v_cs_sec_g3   uuid := 'a1111111-1111-1111-1111-000000000732';
    v_cs_mem_g3   uuid := 'a1111111-1111-1111-1111-000000000733';

    v_cm_min_g3   uuid := 'a1111111-1111-1111-1111-000000000741';
BEGIN
    SELECT id INTO v_role_student FROM identity.role WHERE role_name = 'Student' LIMIT 1;
    SELECT id INTO v_role_lecturer FROM identity.role WHERE role_name = 'Lecturer' LIMIT 1;
    SELECT id INTO v_role_council FROM identity.role WHERE role_name = 'CouncilMember' LIMIT 1;

    -- 1. Reset password for dev admin & lecturer test accounts cleanly
    UPDATE identity."user"
    SET password_hash = v_pwd_hash,
        access_failed_count = 0,
        status = 'Active',
        updated_at = NOW(),
        password_changed_at = NOW()
    WHERE id IN (v_admin_id, v_gv_a_id, v_gv_c_id, v_gv_khang);

    -- Seed 2 dedicated lecturers (Advisor & Reviewer for Group C so they don't collide with HĐ-UIT-01 members)
    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    (v_gv_d_id, 'GV0003', 'TS. Trần Minh Đức',      'gv.tranminhduc@ugtm.edu.vn',   'GV.TRANMINHDUC@UGTM.EDU.VN',   v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_gv_e_id, 'GV0004', 'PGS.TS. Đặng Hoàng Minh', 'gv.danghoangminh@ugtm.edu.vn', 'GV.DANGHOANGMINH@UGTM.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO NOTHING;

    -- Ensure lecturers also hold Lecturer + CouncilMember roles so they can test both grading pages
    INSERT INTO identity.user_role (user_id, role_id, assigned_at)
    SELECT u_id, r_id, NOW()
    FROM (VALUES
        (v_gv_khang, v_role_lecturer),
        (v_gv_khang, v_role_council),
        (v_gv_a_id, v_role_lecturer),
        (v_gv_a_id, v_role_council),
        (v_gv_c_id, v_role_lecturer),
        (v_gv_c_id, v_role_council),
        (v_gv_d_id, v_role_lecturer),
        (v_gv_e_id, v_role_lecturer)
    ) AS v(u_id, r_id)
    WHERE NOT EXISTS (
        SELECT 1 FROM identity.user_role ur WHERE ur.user_id = v.u_id AND ur.role_id = v.r_id
    );

    -- 2. Seed 10 standard UIT rubric criteria in active open batch (if empty) + HK2 batch
    IF EXISTS (SELECT 1 FROM academic.kltn_batch WHERE id = v_batch_open)
       AND NOT EXISTS (SELECT 1 FROM councils.grading_criteria WHERE batch_id = v_batch_open) THEN
        INSERT INTO councils.grading_criteria (
            id, batch_id, criteria_code, criteria_name, max_score, order_index, elo, section_code, section_name, section_order
        ) VALUES
        (v_c11, v_batch_open, '1.1', 'Tính mới và độ phức tạp', 1.5, 1, 'LO2', '1', 'Ý nghĩa khoa học, giá trị thực tiễn', 1),
        (v_c12, v_batch_open, '1.2', 'Tính thực tiễn và đóng góp', 1.5, 2, 'LO3', '1', 'Ý nghĩa khoa học, giá trị thực tiễn', 1),
        (v_c21, v_batch_open, '2.1', 'Khả năng tìm hiểu, phân tích yêu cầu và thiết kế hệ thống', 1.0, 3, 'LO3', '2', 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM', 2),
        (v_c22, v_batch_open, '2.2', 'Khả năng hiện thực hóa, kiểm thử và vận hành hệ thống, đánh giá giải pháp', 1.0, 4, 'LO4', '2', 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM', 2),
        (v_c23, v_batch_open, '2.3', 'Học hỏi và cập nhật kiến thức, công nghệ mới, khả năng học tập suốt đời', 1.0, 5, 'LO3', '2', 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM', 2),
        (v_c31, v_batch_open, '3.1', 'Nội dung trình bày cuốn báo và sản phẩm (viết)', 1.0, 6, 'LO6, LO8', '3', 'Trình bày kết quả', 3),
        (v_c32, v_batch_open, '3.2', 'Kỹ năng trình bày (nói)', 1.0, 7, 'LO6', '3', 'Trình bày kết quả', 3),
        (v_c33, v_batch_open, '3.3', 'Trả lời các câu hỏi', 1.0, 8, 'LO8', '3', 'Trình bày kết quả', 3),
        (v_c41, v_batch_open, '4.1', 'Lập kế hoạch và quản lý thời gian, công việc của cá nhân hoặc đội nhóm', 0.5, 9, 'LO5, LO7', '4', 'Kỹ năng mềm', 4),
        (v_c42, v_batch_open, '4.2', 'Kỹ năng tìm hiểu, đọc hiểu tài liệu ngoại ngữ và khả năng tư duy, giải quyết vấn đề', 0.5, 10, 'LO4, LO6', '4', 'Kỹ năng mềm', 4)
        ON CONFLICT (id) DO NOTHING;
    ELSE
        -- Resolve existing criteria IDs if batch already had custom criteria
        SELECT id INTO v_c11 FROM councils.grading_criteria WHERE batch_id = v_batch_open ORDER BY order_index LIMIT 1;
    END IF;

    IF EXISTS (SELECT 1 FROM academic.kltn_batch WHERE id = v_batch_hk2)
       AND NOT EXISTS (SELECT 1 FROM councils.grading_criteria WHERE batch_id = v_batch_hk2) THEN
        INSERT INTO councils.grading_criteria (
            id, batch_id, criteria_code, criteria_name, max_score, order_index, elo, section_code, section_name, section_order
        ) VALUES
        ('a1111111-1111-1111-1111-000000000511', v_batch_hk2, '1.1', 'Tính mới và độ phức tạp', 1.5, 1, 'LO2', '1', 'Ý nghĩa khoa học, giá trị thực tiễn', 1),
        ('a1111111-1111-1111-1111-000000000512', v_batch_hk2, '1.2', 'Tính thực tiễn và đóng góp', 1.5, 2, 'LO3', '1', 'Ý nghĩa khoa học, giá trị thực tiễn', 1),
        ('a1111111-1111-1111-1111-000000000513', v_batch_hk2, '2.1', 'Khả năng tìm hiểu, phân tích yêu cầu và thiết kế hệ thống', 1.0, 3, 'LO3', '2', 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM', 2),
        ('a1111111-1111-1111-1111-000000000514', v_batch_hk2, '2.2', 'Khả năng hiện thực hóa, kiểm thử và vận hành hệ thống, đánh giá giải pháp', 1.0, 4, 'LO4', '2', 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM', 2),
        ('a1111111-1111-1111-1111-000000000515', v_batch_hk2, '2.3', 'Học hỏi và cập nhật kiến thức, công nghệ mới, khả năng học tập suốt đời', 1.0, 5, 'LO3', '2', 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM', 2),
        ('a1111111-1111-1111-1111-000000000516', v_batch_hk2, '3.1', 'Nội dung trình bày cuốn báo và sản phẩm (viết)', 1.0, 6, 'LO6, LO8', '3', 'Trình bày kết quả', 3),
        ('a1111111-1111-1111-1111-000000000517', v_batch_hk2, '3.2', 'Kỹ năng trình bày (nói)', 1.0, 7, 'LO6', '3', 'Trình bày kết quả', 3),
        ('a1111111-1111-1111-1111-000000000518', v_batch_hk2, '3.3', 'Trả lời các câu hỏi', 1.0, 8, 'LO8', '3', 'Trình bày kết quả', 3),
        ('a1111111-1111-1111-1111-000000000519', v_batch_hk2, '4.1', 'Lập kế hoạch và quản lý thời gian, công việc của cá nhân hoặc đội nhóm', 0.5, 9, 'LO5, LO7', '4', 'Kỹ năng mềm', 4),
        ('a1111111-1111-1111-1111-000000000520', v_batch_hk2, '4.2', 'Kỹ năng tìm hiểu, đọc hiểu tài liệu ngoại ngữ và khả năng tư duy, giải quyết vấn đề', 0.5, 10, 'LO4, LO6', '4', 'Kỹ năng mềm', 4)
        ON CONFLICT (id) DO NOTHING;
    END IF;

    -- 3. Seed 6 test students
    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    (v_s1, '22520301', 'Võ Minh Trí',   '22520301@gm.uit.edu.vn', '22520301@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s2, '22520302', 'Trần Thảo My',  '22520302@gm.uit.edu.vn', '22520302@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s3, '22520303', 'Đặng Gia Bảo',  '22520303@gm.uit.edu.vn', '22520303@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s4, '22520304', 'Huỳnh Ngọc Lan','22520304@gm.uit.edu.vn', '22520304@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s5, '22520305', 'Phạm Anh Tuấn', '22520305@gm.uit.edu.vn', '22520305@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s6, '22520306', 'Lý Thu Hà',     '22520306@gm.uit.edu.vn', '22520306@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO identity.user_role (user_id, role_id, assigned_at)
    SELECT s_id, v_role_student, NOW()
    FROM (VALUES (v_s1),(v_s2),(v_s3),(v_s4),(v_s5),(v_s6)) AS t(s_id)
    WHERE NOT EXISTS (
        SELECT 1 FROM identity.user_role ur WHERE ur.user_id = t.s_id AND ur.role_id = v_role_student
    );

    -- 4. Seed 3 topics & groups in active open batch
    INSERT INTO topics.topic (
        id, batch_id, advisor_id, title_vi, title_en, description, requirements, status, student_number, created_at
    ) VALUES
    (v_t1, v_batch_open, v_gv_khang, 'Hệ thống nhận diện bất thường giao thông đô thị bằng Edge AI', 'Urban Traffic Anomaly Detection via Edge AI', 'Phát hiện sự cố giao thông thời gian thực từ camera biên.', 'YOLOv8, DeepSORT, Edge deployment', 'Approved', 2, NOW() - INTERVAL '40 days'),
    (v_t2, v_batch_open, v_gv_a_id,  'Nền tảng kiểm thử tự động API dựa trên đặc tả OpenAPI & LLM',  'LLM-Assisted Automated API Testing Platform', 'Sinh bộ kiểm thử hợp đồng API tự động từ OpenAPI 3.1.', '.NET, OpenAPI, Automated Testing', 'Approved', 2, NOW() - INTERVAL '40 days'),
    (v_t3, v_batch_open, v_gv_d_id,  'Trợ lý hỏi đáp học vụ UIT ứng dụng Hybrid RAG',                'UIT Academic Q&A Assistant using Hybrid RAG', 'Tra cứu quy chế đào tạo kết hợp BM25 và Vector Search.', 'PostgreSQL pgvector, RAGAS evaluation', 'Approved', 2, NOW() - INTERVAL '40 days')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.student_group (
        id, topic_id, status, created_at, allow_syllabus_resubmission
    ) VALUES
    (v_g1, v_t1, 'FinalReportSubmitted',       NOW() - INTERVAL '35 days', false),
    (v_g2, v_t2, 'ReadyForReviewerAssignment', NOW() - INTERVAL '35 days', false),
    (v_g3, v_t3, 'ReadyForReviewerAssignment', NOW() - INTERVAL '35 days', false)
    ON CONFLICT (id) DO UPDATE SET status = EXCLUDED.status;

    INSERT INTO thesis.student_group_member (
        id, group_id, student_id, batch_id, joined_at, confirmation_status, confirmed_at_utc
    ) VALUES
    ('a1111111-1111-1111-1111-000000000311', v_g1, v_s1, v_batch_open, NOW() - INTERVAL '34 days', 'Confirmed', NOW() - INTERVAL '34 days'),
    ('a1111111-1111-1111-1111-000000000312', v_g1, v_s2, v_batch_open, NOW() - INTERVAL '34 days', 'Confirmed', NOW() - INTERVAL '34 days'),
    ('a1111111-1111-1111-1111-000000000313', v_g2, v_s3, v_batch_open, NOW() - INTERVAL '34 days', 'Confirmed', NOW() - INTERVAL '34 days'),
    ('a1111111-1111-1111-1111-000000000314', v_g2, v_s4, v_batch_open, NOW() - INTERVAL '34 days', 'Confirmed', NOW() - INTERVAL '34 days'),
    ('a1111111-1111-1111-1111-000000000315', v_g3, v_s5, v_batch_open, NOW() - INTERVAL '34 days', 'Confirmed', NOW() - INTERVAL '34 days'),
    ('a1111111-1111-1111-1111-000000000316', v_g3, v_s6, v_batch_open, NOW() - INTERVAL '34 days', 'Confirmed', NOW() - INTERVAL '34 days')
    ON CONFLICT (id) DO NOTHING;

    -- 5. Seed Final Reports + Submission Files
    INSERT INTO thesis.final_report (
        id, group_id, version_type, current_version_no, status, submitted_at
    ) VALUES
    (v_fr1, v_g1, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '2 days'),
    (v_fr2, v_g2, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '4 days'),
    (v_fr3, v_g3, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '6 days')
    ON CONFLICT (id) DO UPDATE SET status = EXCLUDED.status;

    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
    (v_sf1, 'FinalReport', v_fr1, 1, 'demo/grading/g1-final.pdf', 'KLTN-EdgeAI-ToanVan.pdf',  'application/pdf', 3200000, '.pdf', v_sha1, v_s1, NOW() - INTERVAL '2 days'),
    (v_sf2, 'FinalReport', v_fr2, 1, 'demo/grading/g2-final.pdf', 'KLTN-OpenAPI-ToanVan.pdf', 'application/pdf', 3500000, '.pdf', v_sha2, v_s3, NOW() - INTERVAL '4 days'),
    (v_sf3, 'FinalReport', v_fr3, 1, 'demo/grading/g3-final.pdf', 'KLTN-HybridRAG-ToanVan.pdf','application/pdf', 4100000, '.pdf', v_sha3, v_s5, NOW() - INTERVAL '6 days')
    ON CONFLICT (id) DO NOTHING;

    -- 6. Seed Group B: GV0001 Advisor approved -> GV_TEST_01 assigned Reviewer (ready to grade per student!)
    INSERT INTO councils.advisor_evaluation (
        id, group_id, score, comment, evaluated_at, allows_reviewer_assignment,
        decision_at, decision_by, evaluated_by, final_report_id, is_legacy,
        report_version_no, submission_checksum_sha256, submission_file_id
    ) VALUES (
        v_adv_eval_g2, v_g2, 8.50,
        'Đặng Gia Bảo phụ trách tốt phần OpenAPI parser & test runner; Huỳnh Ngọc Lan hoàn thiện UI báo cáo mức khá.',
        NOW() - INTERVAL '3 days', true, NOW() - INTERVAL '3 days', v_gv_a_id, v_gv_a_id, v_fr2, false,
        1, v_sha2, v_sf2
    ) ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.reviewer_assignment (
        id, group_id, reviewer_id, assigned_by, assigned_at, assignment_order, is_active,
        advisor_evaluation_id, advisor_id_at_assignment, final_report_id, is_legacy,
        report_version_no, submission_checksum_sha256, submission_file_id
    ) VALUES (
        v_rev_assign_g2, v_g2, v_gv_khang, v_admin_id, NOW() - INTERVAL '2 days', 1, true,
        v_adv_eval_g2, v_gv_a_id, v_fr2, false, 1, v_sha2, v_sf2
    ) ON CONFLICT (id) DO NOTHING;

    -- 7. Seed Group C: Full Advisor + Reviewer + Council 3 members with distinct SV1/SV2 per-student details!
    INSERT INTO councils.advisor_evaluation (
        id, group_id, score, comment, evaluated_at, allows_reviewer_assignment,
        decision_at, decision_by, evaluated_by, final_report_id, is_legacy,
        report_version_no, submission_checksum_sha256, submission_file_id
    ) VALUES (
        v_adv_eval_g3, v_g3, 8.50,
        'Phạm Anh Tuấn thiết kế lõi Hybrid RAG tốt (9.0); Lý Thu Hà hoàn thiện đánh giá benchmark & báo cáo (8.0).',
        NOW() - INTERVAL '5 days', true, NOW() - INTERVAL '5 days', v_gv_d_id, v_gv_d_id, v_fr3, false,
        1, v_sha3, v_sf3
    ) ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.reviewer_assignment (
        id, group_id, reviewer_id, assigned_by, assigned_at, assignment_order, is_active,
        advisor_evaluation_id, advisor_id_at_assignment, final_report_id, is_legacy,
        report_version_no, submission_checksum_sha256, submission_file_id
    ) VALUES (
        v_rev_assign_g3, v_g3, v_gv_e_id, v_admin_id, NOW() - INTERVAL '4 days', 1, true,
        v_adv_eval_g3, v_gv_d_id, v_fr3, false, 1, v_sha3, v_sf3
    ) ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.reviewer_evaluation (
        id, assignment_id, score, comment, defense_approval, is_visible_to_student,
        evaluated_at, created_at_utc, created_by, evaluated_by, is_legacy,
        status, submitted_at_utc, submitted_by
    ) VALUES (
        v_rev_eval_g3, v_rev_assign_g3, 8.25,
        'Hệ thống truy xuất chính xác, phân biệt rõ đóng góp từng thành viên khi trả lời phản biện.',
        'Approved', true, NOW() - INTERVAL '3 days', NOW() - INTERVAL '3 days', v_gv_e_id, v_gv_e_id, false,
        'Submitted', NOW() - INTERVAL '3 days', v_gv_e_id
    ) ON CONFLICT (id) DO NOTHING;

    -- Active Council HĐ-UIT-01 in Đợt KLTN Thử Nghiệm
    INSERT INTO councils.council (
        id, batch_id, council_code, defense_date, time_slot, room, status, decision_number, signed_date, approved_by, approved_at, is_submitted_to_academic_affairs
    ) VALUES (
        v_council_id, v_batch_open, 'HĐ-UIT-01', CURRENT_DATE + 5, '08:00 - 11:30 (Sáng)', 'E3.1', 'Active', '128/QĐ-ĐHCNTT', CURRENT_DATE, v_admin_id, NOW() - INTERVAL '2 days', false
    ) ON CONFLICT (id) DO UPDATE SET status = 'Active';

    INSERT INTO councils.council_member (id, council_id, user_id, role_in_council) VALUES
    (v_cm_chair, v_council_id, v_gv_khang, 'Chairman'),
    (v_cm_sec,   v_council_id, v_gv_a_id,  'Secretary'),
    (v_cm_mem,   v_council_id, v_gv_c_id,  'Member')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.council_assignment (id, council_id, group_id, order_number) VALUES
    (v_ca_9999, v_council_id, v_group_9999, 1),
    (v_ca_g3,   v_council_id, v_g3,         2)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.council_minute (
        id, council_assignment_id, general_feedback, lock_status, locked_by, locked_at
    ) VALUES (
        v_cm_min_g3, v_ca_g3, 'Nhóm trình bày mạch lạc, trả lời câu hỏi Hội đồng rõ ràng.', 'Locked', v_gv_a_id, NOW() - INTERVAL '12 hours'
    ) ON CONFLICT (id) DO UPDATE SET lock_status = 'Locked', locked_by = EXCLUDED.locked_by, locked_at = EXCLUDED.locked_at;

    -- Council member scores for Group C (SV5 = 9.0 / 8.75 / 8.50; SV6 = 8.0 / 7.75 / 7.50)
    INSERT INTO councils.council_score (
        id, council_assignment_id, council_member_id, score, feedback, evaluated_at, is_ignored_in_calculation
    ) VALUES
    (v_cs_chair_g3, v_ca_g3, v_cm_chair, 8.50, 'Tốt, nắm vững kiến trúc RAG.', NOW() - INTERVAL '1 day', false),
    (v_cs_sec_g3,   v_ca_g3, v_cm_sec,   8.25, 'Demo ổn định, số liệu rõ.', NOW() - INTERVAL '1 day', false),
    (v_cs_mem_g3,   v_ca_g3, v_cm_mem,   8.00, 'Báo cáo trình bày cẩn thận.', NOW() - INTERVAL '1 day', false)
    ON CONFLICT (id) DO NOTHING;

    -- Seed explicit per-student evaluation_detail rows using seeded batch criteria
    IF EXISTS (SELECT 1 FROM councils.grading_criteria WHERE id = v_c42) THEN
        -- Advisor evaluation Group C details: SV5 = 9.00, SV6 = 8.00
        INSERT INTO councils.evaluation_detail (id, criteria_id, evaluation_id, student_id, score) VALUES
        (gen_random_uuid(), v_c11, v_adv_eval_g3, v_s5, 1.50),
        (gen_random_uuid(), v_c12, v_adv_eval_g3, v_s5, 1.25),
        (gen_random_uuid(), v_c21, v_adv_eval_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c22, v_adv_eval_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c23, v_adv_eval_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c31, v_adv_eval_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c32, v_adv_eval_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c33, v_adv_eval_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c41, v_adv_eval_g3, v_s5, 0.50),
        (gen_random_uuid(), v_c42, v_adv_eval_g3, v_s5, 0.50),

        (gen_random_uuid(), v_c11, v_adv_eval_g3, v_s6, 1.25),
        (gen_random_uuid(), v_c12, v_adv_eval_g3, v_s6, 1.25),
        (gen_random_uuid(), v_c21, v_adv_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c22, v_adv_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c23, v_adv_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c31, v_adv_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c32, v_adv_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c33, v_adv_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c41, v_adv_eval_g3, v_s6, 0.50),
        (gen_random_uuid(), v_c42, v_adv_eval_g3, v_s6, 0.50),

        -- Reviewer evaluation Group C details: SV5 = 8.75, SV6 = 7.75
        (gen_random_uuid(), v_c11, v_rev_eval_g3, v_s5, 1.25),
        (gen_random_uuid(), v_c12, v_rev_eval_g3, v_s5, 1.25),
        (gen_random_uuid(), v_c21, v_rev_eval_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c22, v_rev_eval_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c23, v_rev_eval_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c31, v_rev_eval_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c32, v_rev_eval_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c33, v_rev_eval_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c41, v_rev_eval_g3, v_s5, 0.50),
        (gen_random_uuid(), v_c42, v_rev_eval_g3, v_s5, 0.50),

        (gen_random_uuid(), v_c11, v_rev_eval_g3, v_s6, 1.25),
        (gen_random_uuid(), v_c12, v_rev_eval_g3, v_s6, 1.00),
        (gen_random_uuid(), v_c21, v_rev_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c22, v_rev_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c23, v_rev_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c31, v_rev_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c32, v_rev_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c33, v_rev_eval_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c41, v_rev_eval_g3, v_s6, 0.50),
        (gen_random_uuid(), v_c42, v_rev_eval_g3, v_s6, 0.50),

        -- Council Chairman score details for Group C: SV5 = 9.00, SV6 = 8.00
        (gen_random_uuid(), v_c11, v_cs_chair_g3, v_s5, 1.50),
        (gen_random_uuid(), v_c12, v_cs_chair_g3, v_s5, 1.25),
        (gen_random_uuid(), v_c21, v_cs_chair_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c22, v_cs_chair_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c23, v_cs_chair_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c31, v_cs_chair_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c32, v_cs_chair_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c33, v_cs_chair_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c41, v_cs_chair_g3, v_s5, 0.50),
        (gen_random_uuid(), v_c42, v_cs_chair_g3, v_s5, 0.50),

        (gen_random_uuid(), v_c11, v_cs_chair_g3, v_s6, 1.25),
        (gen_random_uuid(), v_c12, v_cs_chair_g3, v_s6, 1.25),
        (gen_random_uuid(), v_c21, v_cs_chair_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c22, v_cs_chair_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c23, v_cs_chair_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c31, v_cs_chair_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c32, v_cs_chair_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c33, v_cs_chair_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c41, v_cs_chair_g3, v_s6, 0.50),
        (gen_random_uuid(), v_c42, v_cs_chair_g3, v_s6, 0.50),

        -- Council Secretary score details for Group C: SV5 = 8.75, SV6 = 7.75
        (gen_random_uuid(), v_c11, v_cs_sec_g3, v_s5, 1.25),
        (gen_random_uuid(), v_c12, v_cs_sec_g3, v_s5, 1.25),
        (gen_random_uuid(), v_c21, v_cs_sec_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c22, v_cs_sec_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c23, v_cs_sec_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c31, v_cs_sec_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c32, v_cs_sec_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c33, v_cs_sec_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c41, v_cs_sec_g3, v_s5, 0.50),
        (gen_random_uuid(), v_c42, v_cs_sec_g3, v_s5, 0.50),

        (gen_random_uuid(), v_c11, v_cs_sec_g3, v_s6, 1.25),
        (gen_random_uuid(), v_c12, v_cs_sec_g3, v_s6, 1.00),
        (gen_random_uuid(), v_c21, v_cs_sec_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c22, v_cs_sec_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c23, v_cs_sec_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c31, v_cs_sec_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c32, v_cs_sec_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c33, v_cs_sec_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c41, v_cs_sec_g3, v_s6, 0.50),
        (gen_random_uuid(), v_c42, v_cs_sec_g3, v_s6, 0.50),

        -- Council Member score details for Group C: SV5 = 8.50, SV6 = 7.50
        (gen_random_uuid(), v_c11, v_cs_mem_g3, v_s5, 1.25),
        (gen_random_uuid(), v_c12, v_cs_mem_g3, v_s5, 1.25),
        (gen_random_uuid(), v_c21, v_cs_mem_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c22, v_cs_mem_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c23, v_cs_mem_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c31, v_cs_mem_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c32, v_cs_mem_g3, v_s5, 0.75),
        (gen_random_uuid(), v_c33, v_cs_mem_g3, v_s5, 1.00),
        (gen_random_uuid(), v_c41, v_cs_mem_g3, v_s5, 0.50),
        (gen_random_uuid(), v_c42, v_cs_mem_g3, v_s5, 0.50),

        (gen_random_uuid(), v_c11, v_cs_mem_g3, v_s6, 1.25),
        (gen_random_uuid(), v_c12, v_cs_mem_g3, v_s6, 1.00),
        (gen_random_uuid(), v_c21, v_cs_mem_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c22, v_cs_mem_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c23, v_cs_mem_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c31, v_cs_mem_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c32, v_cs_mem_g3, v_s6, 0.75),
        (gen_random_uuid(), v_c33, v_cs_mem_g3, v_s6, 0.50),
        (gen_random_uuid(), v_c41, v_cs_mem_g3, v_s6, 0.50),
        (gen_random_uuid(), v_c42, v_cs_mem_g3, v_s6, 0.50)
        ON CONFLICT (criteria_id, evaluation_id, student_id) DO NOTHING;
    END IF;
END $$;
'@

$seedSql.Replace('__DEMO_PWD_HASH__', $demoPasswordHash) | & $psql -v ON_ERROR_STOP=1 -f -
if ($LASTEXITCODE -ne 0) { throw "Failed to seed grading demo data (exit $LASTEXITCODE)." }

Write-Host 'Seeded grading & council demo data cleanly!' -ForegroundColor Green
Write-Host "Dev accounts password set to: $demoPasswordPlain"
Write-Host ' - Admin:                 admin@ugtm.edu.vn'
Write-Host ' - Lecturer + Council:    gv.test.khang@ugtm.edu.vn (GV_TEST_01 - has Advisor + Reviewer + Council Chairman groups)'
Write-Host ' - Lecturer + Council:    gv.nguyenvana@ugtm.edu.vn  (GV0001     - Council Secretary + Reviewer)'
Write-Host ' - Lecturer + Council:    gv.lequangc@ugtm.edu.vn    (GV0002     - Council Member)'
