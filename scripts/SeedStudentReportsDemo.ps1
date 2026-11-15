[CmdletBinding()]
param(
    [switch]$Reset
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoRoot '.env'

if (-not (Test-Path -LiteralPath $envFile)) {
    throw ".env file not found at $envFile"
}

$connStr = $null
foreach ($line in Get-Content -LiteralPath $envFile) {
    if ($line -match '^DATABASE_CONNECTION_STRING=(.+)$') {
        $connStr = $Matches[1].Trim().Trim('"').Trim("'")
        break
    }
}
if (-not $connStr) {
    throw 'DATABASE_CONNECTION_STRING not found in .env'
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
DELETE FROM councils.reviewer_evaluation WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM councils.reviewer_assignment WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM councils.advisor_evaluation WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM thesis.final_report_plagiarism_scan WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM thesis.final_report WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM thesis.submission_review WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM thesis.midterm_report WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM thesis.syllabus WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM thesis.progress_report_version WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM thesis.progress_report WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM thesis.progress_milestone WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM thesis.submission_file WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM thesis.student_group_member WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM thesis.student_group WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM topics.topic WHERE id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM identity.user_role WHERE user_id::text LIKE '99999999-9999-9999-9999-%';
DELETE FROM identity."user" WHERE id::text LIKE '99999999-9999-9999-9999-%';
"@
    $resetSql | & $psql -v ON_ERROR_STOP=1 -f -
    if ($LASTEXITCODE -ne 0) { throw "Failed to reset student reports demo records (exit $LASTEXITCODE)." }
    Write-Host 'Reset completed: removed student reports demo records (99999999-9999-9999-9999-*).' -ForegroundColor Yellow
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

$demoPasswordPlain = 'DemoStudent@2026'
$demoPasswordHash = Get-AspNetIdentityV3Hash $demoPasswordPlain

$seedSql = @'
DO $$
DECLARE
    -- Existing student 24520700 enrichment
    v_khanh_id uuid;
    v_khanh_group uuid := '13c2a7dc-a0bc-4c6a-a560-26ad3b4fda44';
    v_khanh_advisor uuid;
    v_khanh_m1 uuid := '4bd08461-6092-4432-985c-d6cb73120e74';
    v_khanh_m2 uuid := '54719d4d-3bbe-4d37-aa55-675f125729b3';
    v_khanh_m3 uuid := 'afeb7663-9abd-4db4-9ce0-9526b075ff46';
    v_khanh_pr2 uuid := '99999999-9999-9999-9999-100000000202';

    -- Separate clean demo student group (22520201 & 22520202)
    v_batch_id uuid;
    v_advisor_id uuid;
    v_reviewer_id uuid;
    v_admin_id uuid;
    v_student_role_id integer;
    v_pwd_hash text := '__DEMO_PWD_HASH__';

    v_s1 uuid := '99999999-9999-9999-9999-000000000301';
    v_s2 uuid := '99999999-9999-9999-9999-000000000302';
    v_topic uuid := '99999999-9999-9999-9999-000000000101';
    v_group uuid := '99999999-9999-9999-9999-000000000201';

    v_m1 uuid := '99999999-9999-9999-9999-000000000501';
    v_m2 uuid := '99999999-9999-9999-9999-000000000502';
    v_m3 uuid := '99999999-9999-9999-9999-000000000503';
    v_m4 uuid := '99999999-9999-9999-9999-000000000504';

    v_r1 uuid := '99999999-9999-9999-9999-000000000601';
    v_r2 uuid := '99999999-9999-9999-9999-000000000602';
    v_r3 uuid := '99999999-9999-9999-9999-000000000603';

    v_syl uuid := '99999999-9999-9999-9999-000000000401';
    v_mid uuid := '99999999-9999-9999-9999-000000000402';
    v_fin uuid := '99999999-9999-9999-9999-000000000403';

    v_fin_f1 uuid := '99999999-9999-9999-9999-000000000821';
    v_fin_f2 uuid := '99999999-9999-9999-9999-000000000822';
    v_sha_v1 text := '9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08';
    v_sha_v2 text := '4b227777d4dd1fc61c6f884f48641d02b4d121d3fd328cb08b5531fcacdabf8a';

    v_adv_eval uuid := '99999999-9999-9999-9999-000000000901';
    v_rev_assign uuid := '99999999-9999-9999-9999-000000000902';
    v_rev_eval uuid := '99999999-9999-9999-9999-000000000903';
BEGIN
    SELECT id INTO v_student_role_id
    FROM identity.role
    WHERE role_name = 'Student'
    LIMIT 1;

    -- =========================================================================
    -- PART A: Enrich existing student 24520700 (Nhóm 13C2 / KLTN-39B5FA50)
    -- =========================================================================
    SELECT id INTO v_khanh_id FROM identity."user" WHERE user_code = '24520700' LIMIT 1;
    SELECT t.advisor_id INTO v_khanh_advisor
    FROM thesis.student_group sg
    JOIN topics.topic t ON t.id = sg.topic_id
    WHERE sg.id = v_khanh_group
    LIMIT 1;

    IF v_khanh_id IS NOT NULL AND v_khanh_advisor IS NOT NULL THEN
        UPDATE thesis.progress_milestone
        SET description = E'- Khởi tạo repository Git và chuẩn hóa cấu trúc dự án.\n- Nộp tài liệu đặc tả phạm vi ban đầu (`PDF/DOCX`).'
        WHERE id = v_khanh_m1 AND (description IS NULL OR btrim(description) = 'Khởi tạo Repo');

        UPDATE thesis.progress_milestone
        SET description = E'- Hoàn thiện **kiến trúc dữ liệu & sơ đồ xử lý** chi tiết.\n- Bổ sung bộ test đơn vị (`>= 80% coverage` ở tầng xử lý chính).\n- Đính kèm đường dẫn Git branch và nhật ký thực nghiệm.'
        WHERE id = v_khanh_m2 AND (description IS NULL OR btrim(description) = '');

        UPDATE thesis.progress_milestone
        SET description = E'- Tổng hợp **kết quả benchmark thực nghiệm** và so sánh baseline.\n- Chuẩn bị slide demo thử nghiệm trước buổi báo cáo giữa kỳ.'
        WHERE id = v_khanh_m3 AND (description IS NULL OR btrim(description) = '');

        -- Seed 2 versions (v1 NeedRevision -> v2 Submitted) for Mốc 2 if no report exists yet
        IF EXISTS (SELECT 1 FROM thesis.progress_milestone WHERE id = v_khanh_m2)
           AND NOT EXISTS (SELECT 1 FROM thesis.progress_report WHERE milestone_id = v_khanh_m2) THEN
            INSERT INTO thesis.progress_report (
                id, milestone_id, group_id, current_version_no, status, submitted_at, reviewed_at, student_note, advisor_feedback
            ) VALUES (
                v_khanh_pr2, v_khanh_m2, v_khanh_group, 2, 'Submitted',
                NOW() - INTERVAL '3 hours', NULL,
                E'Đã bổ sung bộ test `xUnit` cho module phân tích chỉ số và cập nhật lại tài liệu thiết kế **v2**.',
                NULL
            );

            INSERT INTO thesis.progress_report_version (
                id, progress_report_id, version_no, git_url, student_note, submitted_by, submitted_at,
                is_late, status, reviewed_by, reviewed_at, advisor_feedback
            ) VALUES
                (
                    '99999999-9999-9999-9999-100000000701', v_khanh_pr2, 1,
                    'https://github.com/uit-kltn/nhom-13c2/tree/dot-2-v1',
                    'Nộp bản nháp thiết kế dữ liệu và module xử lý sơ bộ.',
                    v_khanh_id, NOW() - INTERVAL '4 days', false, 'NeedRevision',
                    v_khanh_advisor, NOW() - INTERVAL '2 days',
                    E'Bản v1 trình bày rõ hướng tiếp cận, cần bổ sung **2 nội dung**:\n- Làm rõ biểu đồ tuần tự luồng xử lý lỗi.\n- Đính kèm kết quả chạy unit test trước khi chốt mốc.'
                ),
                (
                    '99999999-9999-9999-9999-100000000702', v_khanh_pr2, 2,
                    'https://github.com/uit-kltn/nhom-13c2/tree/dot-2-v2',
                    E'Đã bổ sung bộ test `xUnit` cho module phân tích chỉ số và cập nhật lại tài liệu thiết kế **v2**.',
                    v_khanh_id, NOW() - INTERVAL '3 hours', false, 'Submitted',
                    NULL, NULL, NULL
                )
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO thesis.submission_file (
                id, entity_type, entity_id, version_no, object_key, file_name,
                content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
            ) VALUES
                (
                    '99999999-9999-9999-9999-100000000801', 'ProgressReport', v_khanh_pr2, 1,
                    'demo/khanh/dot-2-thiet-ke-v1.pdf', 'Bao-cao-Dot-2-Thiet-ke-v1.pdf',
                    'application/pdf', 1650420, '.pdf', repeat('c', 64), v_khanh_id, NOW() - INTERVAL '4 days'
                ),
                (
                    '99999999-9999-9999-9999-100000000802', 'ProgressReport', v_khanh_pr2, 2,
                    'demo/khanh/dot-2-thiet-ke-v2.docx', 'Bao-cao-Dot-2-Hoan-thien-v2.docx',
                    'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 2380110, '.docx', repeat('d', 64), v_khanh_id, NOW() - INTERVAL '3 hours'
                )
            ON CONFLICT (id) DO NOTHING;
        END IF;

        -- Make existing Final Report similarity scan Completed if still PendingSubmission
        UPDATE thesis.final_report_plagiarism_scan
        SET status = 'Completed',
            similarity_coverage = 11.8,
            max_reranker_score = 0.72,
            scanned_chunks = 64,
            scanned_characters = 38400,
            matched_characters = 4530,
            cited_findings_count = 3,
            uncited_findings_count = 1,
            suspected_findings_count = 1,
            completed_at = NOW() - INTERVAL '2 hours',
            last_polled_at = NOW() - INTERVAL '2 hours'
        WHERE final_report_id = '32aaaa94-c2cd-4a4f-837a-0ef56d25aff6'
          AND status = 'PendingSubmission';
    END IF;

    -- =========================================================================
    -- PART B: Separate Clean Demo Student Account & Full 4-Gate Group
    -- =========================================================================
    SELECT b.id INTO v_batch_id
    FROM academic.kltn_batch b
    WHERE b.status = 'Open'
    ORDER BY b.created_at DESC
    LIMIT 1;

    IF v_batch_id IS NULL THEN
        SELECT id INTO v_batch_id FROM academic.kltn_batch ORDER BY created_at DESC LIMIT 1;
    END IF;

    SELECT u.id INTO v_advisor_id
    FROM identity."user" u
    JOIN identity.user_role ur ON ur.user_id = u.id
    JOIN identity.role r ON r.id = ur.role_id
    WHERE r.role_name = 'Lecturer' AND u.status = 'Active'
    ORDER BY (u.user_code = 'GV_TEST_01') DESC, u.user_code
    LIMIT 1;

    SELECT u.id INTO v_reviewer_id
    FROM identity."user" u
    JOIN identity.user_role ur ON ur.user_id = u.id
    JOIN identity.role r ON r.id = ur.role_id
    WHERE r.role_name = 'Lecturer' AND u.status = 'Active' AND u.id <> v_advisor_id
    ORDER BY (u.user_code = 'GV0001') DESC, u.user_code
    LIMIT 1;

    SELECT u.id INTO v_admin_id
    FROM identity."user" u
    JOIN identity.user_role ur ON ur.user_id = u.id
    JOIN identity.role r ON r.id = ur.role_id
    WHERE r.role_name IN ('Admin', 'FacultyStaff') AND u.status = 'Active'
    ORDER BY (u.user_code = 'ADMIN001') DESC, u.user_code
    LIMIT 1;

    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    (
        v_s1, '22520201', 'Lê Minh Quân', '22520201@gm.uit.edu.vn', '22520201@GM.UIT.EDU.VN',
        v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0,
        NOW(), NOW(), NOW(), NOW()
    ),
    (
        v_s2, '22520202', 'Phạm Thảo Vy', '22520202@gm.uit.edu.vn', '22520202@GM.UIT.EDU.VN',
        v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0,
        NOW(), NOW(), NOW(), NOW()
    )
    ON CONFLICT (id) DO UPDATE SET
        full_name = EXCLUDED.full_name,
        user_code = EXCLUDED.user_code,
        password_hash = EXCLUDED.password_hash,
        status = 'Active';

    IF v_student_role_id IS NOT NULL THEN
        INSERT INTO identity.user_role (user_id, role_id, assigned_at)
        VALUES
            (v_s1, v_student_role_id, NOW()),
            (v_s2, v_student_role_id, NOW())
        ON CONFLICT DO NOTHING;
    END IF;

    INSERT INTO topics.topic (
        id, batch_id, advisor_id, title_vi, title_en, description, requirements,
        status, student_number, created_at
    ) VALUES (
        v_topic, v_batch_id, v_advisor_id,
        'Nền tảng phân tích tĩnh kết hợp LLM phát hiện lỗ hổng bảo mật trong quy trình CI/CD',
        'Static Analysis and LLM-Assisted Security Vulnerability Detection in CI/CD Pipelines',
        E'Kết hợp Tree-sitter AST, Semgrep và mô hình ngôn ngữ để phát hiện lỗ hổng logic nghiệp vụ và gợi ý bản vá trực tiếp trên Pull Request.',
        E'- Go / .NET Core, TypeScript React\n- Tree-sitter, Docker, PostgreSQL',
        'Published', 2, NOW() - INTERVAL '50 days'
    )
    ON CONFLICT (id) DO UPDATE SET
        batch_id = EXCLUDED.batch_id,
        advisor_id = EXCLUDED.advisor_id,
        title_vi = EXCLUDED.title_vi,
        title_en = EXCLUDED.title_en,
        status = 'Published';

    INSERT INTO thesis.student_group (id, topic_id, status, allow_syllabus_resubmission, created_at)
    VALUES (v_group, v_topic, 'FinalReportSubmitted', false, NOW() - INTERVAL '42 days')
    ON CONFLICT (id) DO UPDATE SET
        topic_id = EXCLUDED.topic_id,
        status = EXCLUDED.status;

    INSERT INTO thesis.student_group_member (
        id, group_id, student_id, batch_id, confirmation_status, joined_at, confirmed_at_utc
    ) VALUES
        ('99999999-9999-9999-9999-000000000311', v_group, v_s1, v_batch_id, 'Confirmed', NOW() - INTERVAL '42 days', NOW() - INTERVAL '42 days'),
        ('99999999-9999-9999-9999-000000000312', v_group, v_s2, v_batch_id, 'Confirmed', NOW() - INTERVAL '42 days', NOW() - INTERVAL '42 days')
    ON CONFLICT (id) DO UPDATE SET
        group_id = EXCLUDED.group_id,
        batch_id = EXCLUDED.batch_id,
        confirmation_status = 'Confirmed';

    -- 4 Milestones covering all states: Accepted, Submitted (v2), NeedRevision (v1), NotSubmitted
    INSERT INTO thesis.progress_milestone (
        id, group_id, title, description, deadline, created_by, created_at
    ) VALUES
    (
        v_m1, v_group,
        'Mốc 1 — Khảo sát tập luật bảo mật & kiến trúc hệ thống',
        E'- Tổng hợp bộ quy tắc **OWASP Top 10** và **CWE-79/CWE-89** dùng cho Semgrep.\n- Hoàn thiện đặc tả kiến trúc microservice và luồng GitHub Webhook.',
        NOW() - INTERVAL '25 days', v_advisor_id, NOW() - INTERVAL '35 days'
    ),
    (
        v_m2, v_group,
        'Mốc 2 — Bộ phân tích AST & truy xuất ngữ cảnh đa tệp',
        E'- Xây dựng **AST call-graph builder** bằng Tree-sitter cho Go và C#.\n- Tối ưu chunk ngữ cảnh khi gửi qua LLM (`<= 4,000 tokens/finding`).',
        NOW() - INTERVAL '7 days', v_advisor_id, NOW() - INTERVAL '24 days'
    ),
    (
        v_m3, v_group,
        'Mốc 3 — Sinh bản vá tự động & đánh giá trên tập benchmark CVE',
        E'- Tự động đề xuất patch `unified diff` trên Pull Request.\n- Đo tỷ lệ **False Positive** và độ chính xác biên dịch sau vá trên `30 CVE` mẫu.',
        NOW() + INTERVAL '2 days', v_advisor_id, NOW() - INTERVAL '10 days'
    ),
    (
        v_m4, v_group,
        'Mốc 4 — Hoàn thiện báo cáo toàn văn & slide bảo vệ Hội đồng',
        E'- Hoàn tất bản thảo toàn văn **Chương 1–5** theo mẫu khoa.\n- Đính kèm link video demo tích hợp trực tiếp trên GitHub Actions.',
        NOW() + INTERVAL '14 days', v_advisor_id, NOW() - INTERVAL '4 days'
    )
    ON CONFLICT (id) DO UPDATE SET
        title = EXCLUDED.title,
        description = EXCLUDED.description,
        deadline = EXCLUDED.deadline;

    INSERT INTO thesis.progress_report (
        id, milestone_id, group_id, current_version_no, status, submitted_at, reviewed_at, student_note, advisor_feedback
    ) VALUES
    (
        v_r1, v_m1, v_group, 1, 'Accepted', NOW() - INTERVAL '26 days', NOW() - INTERVAL '24 days',
        'Nhóm đã hoàn thiện ma trận đối chiếu 28 luật Semgrep và đặc tả OpenAPI.',
        E'**Đạt yêu cầu mốc 1:**\n- Ma trận luật OWASP phân loại rõ theo độ nghiêm trọng CVSS.\n- Tiếp tục tối ưu phần trích xuất call-graph ở Mốc 2.'
    ),
    (
        v_r2, v_m2, v_group, 2, 'Submitted', NOW() - INTERVAL '5 hours', NULL,
        E'Nhóm đã bổ sung cơ chế **cross-file symbol resolution** và giảm token ngữ cảnh xuống còn `3,100 tokens/finding`.',
        NULL
    ),
    (
        v_r3, v_m3, v_group, 1, 'NeedRevision', NOW() - INTERVAL '3 days', NOW() - INTERVAL '1 day',
        'Đã chạy benchmark trên 30 CVE mẫu với độ chính xác vá 76%.',
        E'Cần bổ sung thêm trước khi xác nhận Đạt:\n- Tách bảng **False Positive Rate** trước và sau khi áp dụng AST filter.\n- Đính kèm `log kiểm thử tự động` khi sinh patch không gây lỗi biên dịch.'
    )
    ON CONFLICT (id) DO UPDATE SET
        current_version_no = EXCLUDED.current_version_no,
        status = EXCLUDED.status,
        submitted_at = EXCLUDED.submitted_at,
        reviewed_at = EXCLUDED.reviewed_at,
        student_note = EXCLUDED.student_note,
        advisor_feedback = EXCLUDED.advisor_feedback;

    INSERT INTO thesis.progress_report_version (
        id, progress_report_id, version_no, git_url, student_note, submitted_by, submitted_at,
        is_late, status, reviewed_by, reviewed_at, advisor_feedback
    ) VALUES
    (
        '99999999-9999-9999-9999-000000000701', v_r1, 1,
        'https://github.com/uit-kltn/secguard-ci/tree/milestone-1',
        'Nhóm đã hoàn thiện ma trận đối chiếu 28 luật Semgrep và đặc tả OpenAPI.',
        v_s1, NOW() - INTERVAL '26 days', false, 'Accepted', v_advisor_id, NOW() - INTERVAL '24 days',
        E'**Đạt yêu cầu mốc 1:**\n- Ma trận luật OWASP phân loại rõ theo độ nghiêm trọng CVSS.\n- Tiếp tục tối ưu phần trích xuất call-graph ở Mốc 2.'
    ),
    (
        '99999999-9999-9999-9999-000000000702', v_r2, 1,
        'https://github.com/uit-kltn/secguard-ci/tree/milestone-2-v1',
        'Bản đầu AST call-graph cho một tệp đơn lẻ.',
        v_s2, NOW() - INTERVAL '9 days', false, 'NeedRevision', v_advisor_id, NOW() - INTERVAL '8 days',
        E'Cần bổ sung phân tích liên tệp (**cross-file**) cho luồng gọi hàm qua interface/service.'
    ),
    (
        '99999999-9999-9999-9999-000000000703', v_r2, 2,
        'https://github.com/uit-kltn/secguard-ci/tree/milestone-2-v2',
        E'Nhóm đã bổ sung cơ chế **cross-file symbol resolution** và giảm token ngữ cảnh xuống còn `3,100 tokens/finding`.',
        v_s2, NOW() - INTERVAL '5 hours', false, 'Submitted', NULL, NULL, NULL
    ),
    (
        '99999999-9999-9999-9999-000000000704', v_r3, 1,
        'https://github.com/uit-kltn/secguard-ci/tree/milestone-3-cve-bench',
        'Đã chạy benchmark trên 30 CVE mẫu với độ chính xác vá 76%.',
        v_s1, NOW() - INTERVAL '3 days', false, 'NeedRevision', v_advisor_id, NOW() - INTERVAL '1 day',
        E'Cần bổ sung thêm trước khi xác nhận Đạt:\n- Tách bảng **False Positive Rate** trước và sau khi áp dụng AST filter.\n- Đính kèm `log kiểm thử tự động` khi sinh patch không gây lỗi biên dịch.'
    )
    ON CONFLICT (id) DO UPDATE SET
        status = EXCLUDED.status,
        advisor_feedback = EXCLUDED.advisor_feedback;

    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
    ('99999999-9999-9999-9999-000000000801', 'ProgressReport', v_r1, 1, 'demo/secguard/m1-v1.pdf', 'Moc-1-Khao-sat-kien-truc-SecGuard.pdf', 'application/pdf', 2145000, '.pdf', repeat('1', 64), v_s1, NOW() - INTERVAL '26 days'),
    ('99999999-9999-9999-9999-000000000802', 'ProgressReport', v_r2, 1, 'demo/secguard/m2-v1.pdf', 'Moc-2-AST-SingleFile-v1.pdf', 'application/pdf', 1780000, '.pdf', repeat('2', 64), v_s2, NOW() - INTERVAL '9 days'),
    ('99999999-9999-9999-9999-000000000803', 'ProgressReport', v_r2, 2, 'demo/secguard/m2-v2.docx', 'Moc-2-CrossFile-CallGraph-v2.docx', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 2840000, '.docx', repeat('3', 64), v_s2, NOW() - INTERVAL '5 hours'),
    ('99999999-9999-9999-9999-000000000804', 'ProgressReport', v_r3, 1, 'demo/secguard/m3-v1.pdf', 'Moc-3-Benchmark-30-CVE-v1.pdf', 'application/pdf', 3250000, '.pdf', repeat('4', 64), v_s1, NOW() - INTERVAL '3 days')
    ON CONFLICT (id) DO NOTHING;

    -- Gate 1: Syllabus (v1 NeedRevision -> v2 Approved)
    INSERT INTO thesis.syllabus (
        id, group_id, current_version_no, status, reviewed_by, reviewed_at, faculty_feedback
    ) VALUES (
        v_syl, v_group, 2, 'Approved', v_admin_id, NOW() - INTERVAL '28 days',
        'Đề cương bản v2 đã bổ sung đầy đủ phạm vi và phương pháp đánh giá thực nghiệm. Đạt Cổng 1.'
    )
    ON CONFLICT (id) DO UPDATE SET
        current_version_no = EXCLUDED.current_version_no,
        status = EXCLUDED.status,
        faculty_feedback = EXCLUDED.faculty_feedback;

    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
    ('99999999-9999-9999-9999-000000000811', 'Syllabus', v_syl, 1, 'demo/secguard/syllabus-v1.docx', 'De-cuong-SecGuard-CICD-v1.docx', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 1420000, '.docx', repeat('5', 64), v_s1, NOW() - INTERVAL '33 days'),
    ('99999999-9999-9999-9999-000000000812', 'Syllabus', v_syl, 2, 'demo/secguard/syllabus-v2.pdf', 'De-cuong-chi-tiet-SecGuard-CICD-v2.pdf', 'application/pdf', 1890000, '.pdf', repeat('6', 64), v_s1, NOW() - INTERVAL '29 days')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.submission_review (
        id, gate_type, submission_id, version_no, decision, feedback, reviewed_by, reviewed_at_utc
    ) VALUES
    ('99999999-9999-9999-9999-000000000851', 'Syllabus', v_syl, 1, 'NeedRevision', E'Cần làm rõ **tập dữ liệu CVE đối chứng** và tiêu chí đo tỷ lệ dương tính giả (False Positive Rate) ở Chương 3.', v_admin_id, NOW() - INTERVAL '31 days'),
    ('99999999-9999-9999-9999-000000000852', 'Syllabus', v_syl, 2, 'Approved', 'Đề cương bản v2 đã bổ sung đầy đủ phạm vi và phương pháp đánh giá thực nghiệm. Đạt Cổng 1.', v_admin_id, NOW() - INTERVAL '28 days')
    ON CONFLICT (id) DO UPDATE SET decision = EXCLUDED.decision, feedback = EXCLUDED.feedback;

    -- Gate 2: Midterm (v1 NeedRevision -> v2 Approved)
    INSERT INTO thesis.midterm_report (
        id, group_id, current_version_no, status, reviewed_by, reviewed_at, faculty_feedback
    ) VALUES (
        v_mid, v_group, 2, 'Approved', v_admin_id, NOW() - INTERVAL '14 days',
        'Báo cáo giữa kỳ đầy đủ kết quả thử nghiệm bước đầu và kiến trúc hoàn chỉnh. Đủ điều kiện nộp toàn văn.'
    )
    ON CONFLICT (id) DO UPDATE SET
        current_version_no = EXCLUDED.current_version_no,
        status = EXCLUDED.status,
        faculty_feedback = EXCLUDED.faculty_feedback;

    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
    ('99999999-9999-9999-9999-000000000813', 'MidtermReport', v_mid, 1, 'demo/secguard/midterm-v1.pdf', 'Bao-cao-giua-ky-SecGuard-v1.pdf', 'application/pdf', 2910000, '.pdf', repeat('7', 64), v_s2, NOW() - INTERVAL '18 days'),
    ('99999999-9999-9999-9999-000000000814', 'MidtermReport', v_mid, 2, 'demo/secguard/midterm-v2.pdf', 'Bao-cao-giua-ky-SecGuard-v2-Chinh-thuc.pdf', 'application/pdf', 3450000, '.pdf', repeat('8', 64), v_s2, NOW() - INTERVAL '15 days')
    ON CONFLICT (id) DO UPDATE SET
        entity_type = EXCLUDED.entity_type,
        file_name = EXCLUDED.file_name;

    INSERT INTO thesis.submission_review (
        id, gate_type, submission_id, version_no, decision, feedback, reviewed_by, reviewed_at_utc
    ) VALUES
    ('99999999-9999-9999-9999-000000000853', 'Midterm', v_mid, 1, 'NeedRevision', 'Bổ sung biểu đồ độ trễ phân tích trung bình trên từng Pull Request (> 500 LOC) và đối chiếu tiến độ đạt các mốc GVHD giao.', v_admin_id, NOW() - INTERVAL '16 days'),
    ('99999999-9999-9999-9999-000000000854', 'Midterm', v_mid, 2, 'Approved', 'Báo cáo giữa kỳ đầy đủ kết quả thử nghiệm bước đầu và kiến trúc hoàn chỉnh. Đủ điều kiện nộp toàn văn.', v_admin_id, NOW() - INTERVAL '14 days')
    ON CONFLICT (id) DO UPDATE SET decision = EXCLUDED.decision, feedback = EXCLUDED.feedback;

    -- Gate 3: Final Report (v1 -> v2 UnderEvaluation for Reviewer, Completed Plagiarism Scan, GVPB Feedback)
    INSERT INTO thesis.final_report (
        id, group_id, version_type, current_version_no, status, submitted_at
    ) VALUES (
        v_fin, v_group, 'InitialFinal', 2, 'UnderEvaluation', NOW() - INTERVAL '2 days'
    )
    ON CONFLICT (id) DO UPDATE SET
        current_version_no = EXCLUDED.current_version_no,
        status = EXCLUDED.status;

    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
    (v_fin_f1, 'FinalReport', v_fin, 1, 'demo/secguard/final-v1.docx', 'KLTN-SecGuard-Toan-van-v1.docx', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 4850000, '.docx', v_sha_v1, v_s1, NOW() - INTERVAL '5 days'),
    (v_fin_f2, 'FinalReport', v_fin, 2, 'demo/secguard/final-v2.pdf', 'KLTN-SecGuard-Toan-van-v2-Chinh-thuc.pdf', 'application/pdf', 5340000, '.pdf', v_sha_v2, v_s1, NOW() - INTERVAL '2 days')
    ON CONFLICT (id) DO UPDATE SET
        checksum_sha256 = EXCLUDED.checksum_sha256;

    INSERT INTO thesis.final_report_plagiarism_scan (
        id, final_report_id, submission_file_id, version_no, status,
        similarity_coverage, max_reranker_score, scanned_chunks, scanned_characters,
        matched_characters, cited_findings_count, uncited_findings_count, suspected_findings_count,
        attempt_count, created_at, last_polled_at, completed_at
    ) VALUES (
        '99999999-9999-9999-9999-000000000831', v_fin, v_fin_f2, 2, 'Completed',
        8.6, 0.64, 112, 68400, 5882, 4, 0, 0, 1,
        NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days'
    )
    ON CONFLICT (id) DO UPDATE SET
        status = EXCLUDED.status,
        similarity_coverage = EXCLUDED.similarity_coverage,
        max_reranker_score = EXCLUDED.max_reranker_score;

    -- Published GVPB Feedback via valid advisor_evaluation -> reviewer_assignment -> reviewer_evaluation
    IF v_reviewer_id IS NOT NULL AND v_admin_id IS NOT NULL THEN
        INSERT INTO councils.advisor_evaluation (
            id, group_id, score, comment, evaluated_at, allows_reviewer_assignment,
            decision_at, decision_by, evaluated_by, final_report_id, is_legacy,
            report_version_no, submission_checksum_sha256, submission_file_id
        ) VALUES (
            v_adv_eval, v_group, 8.80,
            'Nhóm thực hiện nghiêm túc, hệ thống chạy ổn định trên CI/CD thực tế.',
            NOW() - INTERVAL '36 hours', true,
            NOW() - INTERVAL '36 hours', v_advisor_id, v_advisor_id, v_fin, false,
            2, v_sha_v2, v_fin_f2
        )
        ON CONFLICT (id) DO NOTHING;

        INSERT INTO councils.reviewer_assignment (
            id, group_id, reviewer_id, assigned_by, assigned_at, assignment_order, is_active,
            advisor_evaluation_id, advisor_id_at_assignment, final_report_id, is_legacy,
            report_version_no, submission_checksum_sha256, submission_file_id
        ) VALUES (
            v_rev_assign, v_group, v_reviewer_id, v_admin_id, NOW() - INTERVAL '24 hours', 1, true,
            v_adv_eval, v_advisor_id, v_fin, false,
            2, v_sha_v2, v_fin_f2
        )
        ON CONFLICT (id) DO NOTHING;

        INSERT INTO councils.reviewer_evaluation (
            id, assignment_id, score, comment, defense_approval, is_visible_to_student,
            evaluated_at, created_at_utc, created_by, evaluated_by, is_legacy,
            status, submitted_at_utc, submitted_by
        ) VALUES (
            v_rev_eval, v_rev_assign, 8.60,
            E'- Luận văn trình bày mạch lạc, cơ sở lý thuyết về AST & LLM prompt filtering rõ ràng.\n- Đề nghị bổ sung thêm bảng so sánh thời gian phản hồi khi chạy song song trên 5 Pull Request trong buổi bảo vệ trước Hội đồng.',
            'Approved', true,
            NOW() - INTERVAL '6 hours', NOW() - INTERVAL '8 hours', v_reviewer_id, v_reviewer_id, false,
            'Submitted', NOW() - INTERVAL '6 hours', v_reviewer_id
        )
        ON CONFLICT (id) DO NOTHING;
    END IF;
END $$;
'@

$seedSql.Replace('__DEMO_PWD_HASH__', $demoPasswordHash) | & $psql -v ON_ERROR_STOP=1 -f -
if ($LASTEXITCODE -ne 0) { throw "Failed to seed student reports demo data (exit $LASTEXITCODE)." }

Write-Host 'Seeded student reports demo data cleanly!' -ForegroundColor Green
Write-Host '1. Enriched existing student: 24520700 (Nhóm 13C2) with rich Markdown descriptions, 2-version Mốc 2, Completed similarity scan.'
Write-Host "2. Clean demo student account: 22520201 / Lê Minh Quân (password: $demoPasswordPlain) with 4 milestones across all states, multi-version Proposal/Midterm/Final, and GVPB feedback."
