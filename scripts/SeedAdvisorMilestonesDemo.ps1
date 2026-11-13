[CmdletBinding()]
param(
    [switch]$Reset
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$envPath = Join-Path $repoRoot '.env'

if (-not (Test-Path $envPath)) {
    throw "Missing .env file at $envPath"
}

$envMap = @{}
foreach ($line in Get-Content $envPath) {
    if ($line -match '^\s*#' -or $line -notmatch '=') {
        continue
    }
    $parts = $line.Split('=', 2)
    $envMap[$parts[0].Trim()] = $parts[1].Trim()
}

$connStr = $envMap['DATABASE_CONNECTION_STRING']
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
DELETE FROM thesis.submission_file WHERE id::text LIKE '88888888-8888-8888-8888-000000000%';
DELETE FROM thesis.progress_report_version WHERE id::text LIKE '88888888-8888-8888-8888-000000000%';
DELETE FROM thesis.progress_report WHERE id::text LIKE '88888888-8888-8888-8888-000000000%';
DELETE FROM thesis.progress_milestone WHERE id::text LIKE '88888888-8888-8888-8888-000000000%';
DELETE FROM thesis.syllabus WHERE id::text LIKE '88888888-8888-8888-8888-000000000%';
DELETE FROM thesis.student_group_member WHERE id::text LIKE '88888888-8888-8888-8888-000000000%';
DELETE FROM thesis.student_group WHERE id::text LIKE '88888888-8888-8888-8888-000000000%';
DELETE FROM topics.topic WHERE id::text LIKE '88888888-8888-8888-8888-000000000%';
DELETE FROM identity.user_role WHERE user_id::text LIKE '88888888-8888-8888-8888-000000000%';
DELETE FROM identity."user" WHERE id::text LIKE '88888888-8888-8888-8888-000000000%';
"@
    $resetSql | & $psql -v ON_ERROR_STOP=1 -f -
    if ($LASTEXITCODE -ne 0) { throw "Failed to reset advisor milestone demo records (exit $LASTEXITCODE)." }
    Write-Host 'Reset completed: removed advisor milestone demo records (88888888-8888-8888-8888-000000000*).' -ForegroundColor Yellow
    return
}

$seedSql = @'
DO $$
DECLARE
    v_batch_id uuid;
    v_gv_id uuid;
    v_student_role_id integer;
    v_pwd_hash text;

    v_s1 uuid := '88888888-8888-8888-8888-000000000301';
    v_s2 uuid := '88888888-8888-8888-8888-000000000302';
    v_s3 uuid := '88888888-8888-8888-8888-000000000303';
    v_s4 uuid := '88888888-8888-8888-8888-000000000304';
    v_s5 uuid := '88888888-8888-8888-8888-000000000305';

    v_t1 uuid := '88888888-8888-8888-8888-000000000101';
    v_t2 uuid := '88888888-8888-8888-8888-000000000102';
    v_t3 uuid := '88888888-8888-8888-8888-000000000103';

    v_g1 uuid := '88888888-8888-8888-8888-000000000201';
    v_g2 uuid := '88888888-8888-8888-8888-000000000202';
    v_g3 uuid := '88888888-8888-8888-8888-000000000203';

    v_m11 uuid := '88888888-8888-8888-8888-000000000501';
    v_m12 uuid := '88888888-8888-8888-8888-000000000502';
    v_m13 uuid := '88888888-8888-8888-8888-000000000503';
    v_m14 uuid := '88888888-8888-8888-8888-000000000504';
    v_m15 uuid := '88888888-8888-8888-8888-000000000505';
    v_m21 uuid := '88888888-8888-8888-8888-000000000506';
    v_m22 uuid := '88888888-8888-8888-8888-000000000507';

    v_r11 uuid := '88888888-8888-8888-8888-000000000601';
    v_r12 uuid := '88888888-8888-8888-8888-000000000602';
    v_r13 uuid := '88888888-8888-8888-8888-000000000603';
    v_r21 uuid := '88888888-8888-8888-8888-000000000604';
    v_r22 uuid := '88888888-8888-8888-8888-000000000605';
BEGIN
    SELECT b.id INTO v_batch_id
    FROM academic.kltn_batch b
    WHERE b.status = 'Open'
      AND NOT EXISTS (
          SELECT 1
          FROM thesis.review_gate_configuration rg
          WHERE rg.batch_id = b.id
            AND rg.final_closure_deadline_utc < NOW()
      )
    ORDER BY b.created_at DESC
    LIMIT 1;

    IF v_batch_id IS NULL THEN
        SELECT id INTO v_batch_id
        FROM academic.kltn_batch
        WHERE status = 'Open'
        ORDER BY created_at DESC
        LIMIT 1;
    END IF;

    IF v_batch_id IS NULL THEN
        SELECT id INTO v_batch_id
        FROM academic.kltn_batch
        ORDER BY created_at DESC
        LIMIT 1;
    END IF;

    IF v_batch_id IS NULL THEN
        RAISE EXCEPTION 'No batch found in academic.kltn_batch for seeding advisor milestones.';
    END IF;

    SELECT u.id INTO v_gv_id
    FROM identity."user" u
    JOIN identity.user_role ur ON ur.user_id = u.id
    JOIN identity.role r ON r.id = ur.role_id
    WHERE r.role_name = 'Lecturer' AND u.status = 'Active'
    ORDER BY (u.user_code = 'GV_TEST_01') DESC, u.user_code
    LIMIT 1;

    IF v_gv_id IS NULL THEN
        RAISE EXCEPTION 'No Active Lecturer found in identity."user".';
    END IF;

    SELECT id INTO v_student_role_id
    FROM identity.role
    WHERE role_name = 'Student'
    LIMIT 1;

    SELECT COALESCE(
        (SELECT password_hash FROM identity."user" WHERE password_hash IS NOT NULL LIMIT 1),
        'AQAAAAIAAYagAAAAEDemoSeedPasswordHashPlaceholder=='
    ) INTO v_pwd_hash;

    -- 1. Seed 5 deterministic demo students so student_group_member never collides
    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    (v_s1, '22520101', 'Nguyễn Minh Khôi', '22520101@gm.uit.edu.vn', '22520101@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s2, '22520102', 'Trần Ngọc Thảo', '22520102@gm.uit.edu.vn', '22520102@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s3, '22520103', 'Lê Hoàng Phúc', '22520103@gm.uit.edu.vn', '22520103@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s4, '22520104', 'Phạm Gia Hân', '22520104@gm.uit.edu.vn', '22520104@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s5, '22520105', 'Võ Tấn Phát', '22520105@gm.uit.edu.vn', '22520105@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO UPDATE SET
        full_name = EXCLUDED.full_name,
        user_code = EXCLUDED.user_code,
        status = 'Active';

    IF v_student_role_id IS NOT NULL THEN
        INSERT INTO identity.user_role (user_id, role_id, assigned_at)
        VALUES
            (v_s1, v_student_role_id, NOW()),
            (v_s2, v_student_role_id, NOW()),
            (v_s3, v_student_role_id, NOW()),
            (v_s4, v_student_role_id, NOW()),
            (v_s5, v_student_role_id, NOW())
        ON CONFLICT DO NOTHING;
    END IF;

    -- 2. Seed 3 Published topics advised by v_gv_id (TS. Phạm Minh Khang / GV_TEST_01)
    INSERT INTO topics.topic (
        id, batch_id, advisor_id, title_vi, title_en, description, requirements,
        status, student_number, created_at
    ) VALUES
    (
        v_t1, v_batch_id, v_gv_id,
        'Hệ thống gợi ý lộ trình học tập cá nhân hóa ứng dụng đồ thị tri thức (GraphRAG)',
        'Personalized Academic Roadmap Recommender Using GraphRAG',
        E'Kết hợp Neo4j Knowledge Graph và mô hình ngôn ngữ lớn để tư vấn môn học theo chuẩn đầu ra.',
        E'- Python, FastAPI, Neo4j\n- React TypeScript',
        'Published', 2, NOW() - INTERVAL '45 days'
    ),
    (
        v_t2, v_batch_id, v_gv_id,
        'Nền tảng phát hiện bất thường mã nguồn và lỗ hổng phụ thuộc CI/CD theo thời gian thực',
        'Real-Time CI/CD Dependency Vulnerability and Code Anomaly Detection Platform',
        E'Phân tích AST và SBOM tự động trên Pull Request, cảnh báo sớm các gói phụ thuộc rủi ro cao.',
        E'- .NET Core / Go, GitHub Actions\n- PostgreSQL, Docker',
        'Published', 2, NOW() - INTERVAL '44 days'
    ),
    (
        v_t3, v_batch_id, v_gv_id,
        'Ứng dụng phân tích cảm xúc đa phương thức hỗ trợ đánh giá bài thuyết trình',
        'Multimodal Sentiment and Delivery Analysis for Student Presentations',
        E'Trích xuất đặc trưng giọng nói và biểu cảm khuôn mặt giúp giảng viên phản hồi trực quan.',
        E'- PyTorch, Whisper, OpenCV',
        'Published', 1, NOW() - INTERVAL '40 days'
    )
    ON CONFLICT (id) DO UPDATE SET
        batch_id = EXCLUDED.batch_id,
        advisor_id = EXCLUDED.advisor_id,
        title_vi = EXCLUDED.title_vi,
        status = 'Published';

    -- 3. Seed 3 Student Groups (InProgress, MidtermSubmitted, SyllabusSubmitted)
    INSERT INTO thesis.student_group (id, topic_id, status, allow_syllabus_resubmission, created_at)
    VALUES
        (v_g1, v_t1, 'InProgress', false, NOW() - INTERVAL '38 days'),
        (v_g2, v_t2, 'MidtermSubmitted', false, NOW() - INTERVAL '37 days'),
        (v_g3, v_t3, 'SyllabusSubmitted', false, NOW() - INTERVAL '14 days')
    ON CONFLICT (id) DO UPDATE SET
        topic_id = EXCLUDED.topic_id,
        status = EXCLUDED.status;

    INSERT INTO thesis.student_group_member (
        id, group_id, student_id, batch_id, confirmation_status, joined_at, confirmed_at_utc
    ) VALUES
        ('88888888-8888-8888-8888-000000000311', v_g1, v_s1, v_batch_id, 'Confirmed', NOW() - INTERVAL '38 days', NOW() - INTERVAL '38 days'),
        ('88888888-8888-8888-8888-000000000312', v_g1, v_s2, v_batch_id, 'Confirmed', NOW() - INTERVAL '38 days', NOW() - INTERVAL '38 days'),
        ('88888888-8888-8888-8888-000000000313', v_g2, v_s3, v_batch_id, 'Confirmed', NOW() - INTERVAL '37 days', NOW() - INTERVAL '37 days'),
        ('88888888-8888-8888-8888-000000000314', v_g2, v_s4, v_batch_id, 'Confirmed', NOW() - INTERVAL '37 days', NOW() - INTERVAL '37 days'),
        ('88888888-8888-8888-8888-000000000315', v_g3, v_s5, v_batch_id, 'Confirmed', NOW() - INTERVAL '14 days', NOW() - INTERVAL '14 days')
    ON CONFLICT (id) DO UPDATE SET
        group_id = EXCLUDED.group_id,
        student_id = EXCLUDED.student_id,
        batch_id = EXCLUDED.batch_id,
        confirmation_status = 'Confirmed';

    -- 4. Seed Syllabus status (Group 1 & 2 Approved, Group 3 Submitted)
    INSERT INTO thesis.syllabus (
        id, group_id, current_version_no, status, reviewed_by, reviewed_at, faculty_feedback
    ) VALUES
        ('88888888-8888-8888-8888-000000000401', v_g1, 1, 'Approved', v_gv_id, NOW() - INTERVAL '30 days', 'Đề cương đạt chuẩn Cổng 1, phạm vi GraphRAG hợp lý.'),
        ('88888888-8888-8888-8888-000000000402', v_g2, 1, 'Approved', v_gv_id, NOW() - INTERVAL '29 days', 'Đề cương đạt yêu cầu, triển khai theo từng sprint 2 tuần.'),
        ('88888888-8888-8888-8888-000000000403', v_g3, 1, 'Submitted', NULL, NULL, NULL)
    ON CONFLICT (id) DO UPDATE SET
        status = EXCLUDED.status,
        faculty_feedback = EXCLUDED.faculty_feedback;

    -- 5. Seed Progress Milestones (rich Markdown deliverables)
    INSERT INTO thesis.progress_milestone (
        id, group_id, title, description, deadline, created_by, created_at
    ) VALUES
    (
        v_m11, v_g1,
        'Mốc 1 — Khảo sát kiến trúc & thiết kế đồ thị tri thức',
        E'- Hoàn thiện **Ontology** môn học và quan hệ tiên quyết trên Neo4j.\n- Nộp tài liệu đặc tả API (`OpenAPI 3.1`) và sơ đồ **ERD**.',
        NOW() - INTERVAL '18 days', v_gv_id, NOW() - INTERVAL '28 days'
    ),
    (
        v_m12, v_g1,
        'Mốc 2 — Pipeline trích xuất tri thức & API truy xuất GraphRAG',
        E'- Xây dựng **ingestion pipeline** từ quy chế đào tạo PDF/Markdown sang Neo4j.\n- Triển khai endpoint `/api/chat/query` kèm **trích dẫn điều khoản gốc**.\n- Đính kèm kết quả benchmark ban đầu (`>= 30` câu hỏi mẫu).',
        NOW() + INTERVAL '2 days', v_gv_id, NOW() - INTERVAL '17 days'
    ),
    (
        v_m13, v_g1,
        'Mốc 3 — Giao diện hội thoại & đánh giá độ chính xác RAGAS',
        E'- Hoàn thiện giao diện chat hiển thị **citation card** điều khoản.\n- Chạy bộ kiểm thử tự động `RAGAS` (Faithfulness & Answer Relevancy).',
        NOW() + INTERVAL '6 days', v_gv_id, NOW() - INTERVAL '10 days'
    ),
    (
        v_m14, v_g1,
        'Mốc 4 — Kiểm thử tải & tối ưu hóa truy vấn Cypher',
        E'- Thiết lập kịch bản **k6 load test** cho `50 CCU`.\n- Tối ưu hóa index Neo4j và cache kết quả truy vấn lặp.',
        NOW() - INTERVAL '2 days', v_gv_id, NOW() - INTERVAL '8 days'
    ),
    (
        v_m15, v_g1,
        'Mốc 5 — Luận văn sơ thảo & video demo toàn hệ thống',
        E'- Nộp bản thảo **Chương 1–4** theo mẫu quy định khoa.\n- Quay video demo luồng hỏi đáp thực tế (`3-5 phút`).',
        NOW() + INTERVAL '14 days', v_gv_id, NOW() - INTERVAL '3 days'
    ),
    (
        v_m21, v_g2,
        'Mốc 1 — Phân tích AST & tích hợp GitHub Webhook',
        E'- Triển khai **Webhook listener** nhận sự kiện Pull Request.\n- Sinh báo cáo **SBOM CycloneDX** tự động.',
        NOW() - INTERVAL '14 days', v_gv_id, NOW() - INTERVAL '26 days'
    ),
    (
        v_m22, v_g2,
        'Mốc 2 — Dashboard cảnh báo bảo mật & báo cáo giữa kỳ',
        E'- Xây dựng bảng điều khiển mức độ **CVSS** theo từng repository.\n- Nộp kèm log thử nghiệm trên 3 repo mã nguồn mở.',
        NOW() - INTERVAL '1 day', v_gv_id, NOW() - INTERVAL '12 days'
    )
    ON CONFLICT (id) DO UPDATE SET
        title = EXCLUDED.title,
        description = EXCLUDED.description,
        deadline = EXCLUDED.deadline;

    -- 6. Seed Progress Reports & Multi-Version Submissions
    INSERT INTO thesis.progress_report (
        id, milestone_id, group_id, current_version_no, status, submitted_at, reviewed_at, student_note, advisor_feedback
    ) VALUES
        (v_r11, v_m11, v_g1, 1, 'Accepted', NOW() - INTERVAL '19 days', NOW() - INTERVAL '18 days',
         'Nhóm đã hoàn thiện schema Neo4j gồm 6 loại node và import 140 môn học thử nghiệm.',
         E'**Đạt yêu cầu mốc 1:**\n- Mô hình quan hệ tiên quyết và tương đương tách bạch rõ ràng.\n- Tiếp tục tối ưu chỉ mục cho bước truy xuất vector.'),
        (v_r12, v_m12, v_g1, 2, 'Submitted', NOW() - INTERVAL '4 hours', NULL,
         E'Nhóm đã bổ sung bộ benchmark **45 câu hỏi thực tế** và khắc phục lỗi tách điều khoản bị cắt đoạn ở `v1`.',
         NULL),
        (v_r13, v_m13, v_g1, 1, 'NeedRevision', NOW() - INTERVAL '3 days', NOW() - INTERVAL '2 days',
         'Đã ghép luồng chat frontend với API GraphRAG, đính kèm bảng điểm RAGAS sơ bộ.',
         E'Cần bổ sung thêm trước khi duyệt:\n- Phân tách điểm `Context Recall` cho nhóm câu hỏi đa bước (multi-hop).\n- Hiển thị rõ **trạng thái đang suy luận** trên UI khi truy vấn đồ thị mất > 2 giây.'),
        (v_r21, v_m21, v_g2, 1, 'Accepted', NOW() - INTERVAL '15 days', NOW() - INTERVAL '14 days',
         'Đã kiểm thử webhook trên repo mẫu và sinh được file SBOM JSON.',
         'Luồng tiếp nhận sự kiện ổn định, nhóm chuyển sang hoàn thiện dashboard cảnh báo.'),
        (v_r22, v_m22, v_g2, 1, 'Submitted', NOW() - INTERVAL '6 hours', NULL,
         'Nhóm xin lỗi vì nộp trễ 1 ngày do bổ sung thêm bộ dữ liệu test cho thư viện npm và NuGet.',
         NULL)
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
        ('88888888-8888-8888-8888-000000000701', v_r11, 1, 'https://github.com/uit-kltn/graphrag-advisor/tree/milestone-1',
         'Nhóm đã hoàn thiện schema Neo4j gồm 6 loại node và import 140 môn học thử nghiệm.',
         v_s1, NOW() - INTERVAL '19 days', false, 'Accepted', v_gv_id, NOW() - INTERVAL '18 days',
         E'**Đạt yêu cầu mốc 1:**\n- Mô hình quan hệ tiên quyết và tương đương tách bạch rõ ràng.\n- Tiếp tục tối ưu chỉ mục cho bước truy xuất vector.'),
        ('88888888-8888-8888-8888-000000000702', v_r12, 1, 'https://github.com/uit-kltn/graphrag-advisor/tree/milestone-2-v1',
         'Phiên bản đầu của pipeline trích xuất quy chế đào tạo.',
         v_s2, NOW() - INTERVAL '5 days', false, 'NeedRevision', v_gv_id, NOW() - INTERVAL '3 days',
         E'Pipeline chạy được nhưng cần sửa 2 điểm:\n- Điều khoản dài bị chia đôi giữa chunk làm mất điều kiện tiên quyết.\n- Bộ test mới có 12 câu hỏi, cần mở rộng tối thiểu **30 câu** nhiều trường hợp biên.'),
        ('88888888-8888-8888-8888-000000000703', v_r12, 2, 'https://github.com/uit-kltn/graphrag-advisor/tree/milestone-2-v2',
         E'Nhóm đã bổ sung bộ benchmark **45 câu hỏi thực tế** và khắc phục lỗi tách điều khoản bị cắt đoạn ở `v1`.',
         v_s2, NOW() - INTERVAL '4 hours', false, 'Submitted', NULL, NULL, NULL),
        ('88888888-8888-8888-8888-000000000704', v_r13, 1, 'https://github.com/uit-kltn/graphrag-advisor/tree/milestone-3-ui',
         'Đã ghép luồng chat frontend với API GraphRAG, đính kèm bảng điểm RAGAS sơ bộ.',
         v_s1, NOW() - INTERVAL '3 days', false, 'NeedRevision', v_gv_id, NOW() - INTERVAL '2 days',
         E'Cần bổ sung thêm trước khi duyệt:\n- Phân tách điểm `Context Recall` cho nhóm câu hỏi đa bước (multi-hop).\n- Hiển thị rõ **trạng thái đang suy luận** trên UI khi truy vấn đồ thị mất > 2 giây.'),
        ('88888888-8888-8888-8888-000000000705', v_r21, 1, 'https://github.com/uit-kltn/cicd-security-guard/tree/v1-ast',
         'Đã kiểm thử webhook trên repo mẫu và sinh được file SBOM JSON.',
         v_s3, NOW() - INTERVAL '15 days', false, 'Accepted', v_gv_id, NOW() - INTERVAL '14 days',
         'Luồng tiếp nhận sự kiện ổn định, nhóm chuyển sang hoàn thiện dashboard cảnh báo.'),
        ('88888888-8888-8888-8888-000000000706', v_r22, 1, 'https://github.com/uit-kltn/cicd-security-guard/tree/v2-dashboard',
         'Nhóm xin lỗi vì nộp trễ 1 ngày do bổ sung thêm bộ dữ liệu test cho thư viện npm và NuGet.',
         v_s4, NOW() - INTERVAL '6 hours', true, 'Submitted', NULL, NULL, NULL)
    ON CONFLICT (id) DO UPDATE SET
        git_url = EXCLUDED.git_url,
        student_note = EXCLUDED.student_note,
        submitted_at = EXCLUDED.submitted_at,
        is_late = EXCLUDED.is_late,
        status = EXCLUDED.status,
        reviewed_by = EXCLUDED.reviewed_by,
        reviewed_at = EXCLUDED.reviewed_at,
        advisor_feedback = EXCLUDED.advisor_feedback;

    -- 7. Seed Submission File metadata linked to progress reports
    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
        ('88888888-8888-8888-8888-000000000801', 'ProgressReport', v_r11, 1,
         'demo/progress-reports/r11-v1-architecture.pdf', 'Bao-cao-kien-truc-GraphRAG-v1.pdf',
         'application/pdf', 2486120, '.pdf', 'demo-sha256-r11-v1', v_s1, NOW() - INTERVAL '19 days'),
        ('88888888-8888-8888-8888-000000000802', 'ProgressReport', v_r12, 1,
         'demo/progress-reports/r12-v1-pipeline.pdf', 'Pipeline-Ingestion-v1.pdf',
         'application/pdf', 1840300, '.pdf', 'demo-sha256-r12-v1', v_s2, NOW() - INTERVAL '5 days'),
        ('88888888-8888-8888-8888-000000000803', 'ProgressReport', v_r12, 2,
         'demo/progress-reports/r12-v2-pipeline-benchmark.docx', 'Bao-cao-Pipeline-va-Benchmark-v2.docx',
         'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 3120540, '.docx', 'demo-sha256-r12-v2', v_s2, NOW() - INTERVAL '4 hours'),
        ('88888888-8888-8888-8888-000000000804', 'ProgressReport', v_r13, 1,
         'demo/progress-reports/r13-v1-ragas.xlsx', 'Ket-qua-danh-gia-RAGAS-v1.xlsx',
         'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 645120, '.xlsx', 'demo-sha256-r13-v1', v_s1, NOW() - INTERVAL '3 days'),
        ('88888888-8888-8888-8888-000000000805', 'ProgressReport', v_r21, 1,
         'demo/progress-reports/r21-v1-sbom.pdf', 'Bao-cao-AST-va-SBOM-v1.pdf',
         'application/pdf', 1520400, '.pdf', 'demo-sha256-r21-v1', v_s3, NOW() - INTERVAL '15 days'),
        ('88888888-8888-8888-8888-000000000806', 'ProgressReport', v_r22, 1,
         'demo/progress-reports/r22-v1-dashboard.pdf', 'Dashboard-Canh-bao-CVSS-v1.pdf',
         'application/pdf', 2910800, '.pdf', 'demo-sha256-r22-v1', v_s4, NOW() - INTERVAL '6 hours')
    ON CONFLICT (id) DO UPDATE SET
        file_name = EXCLUDED.file_name,
        file_size = EXCLUDED.file_size;
END $$;
'@

$seedSql | & $psql -v ON_ERROR_STOP=1 -f -
if ($LASTEXITCODE -ne 0) { throw "Failed to seed advisor milestone demo records (exit $LASTEXITCODE)." }

Write-Host 'Seeded demo groups, syllabi, milestones, and multi-version reports for /advisor/milestones (GV_TEST_01).' -ForegroundColor Green
