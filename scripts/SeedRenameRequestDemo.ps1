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
    $resetSql = @'
DELETE FROM thesis.rename_request
WHERE id::text LIKE 'd1111111-1111-1111-1111-%'
   OR group_id::text LIKE 'd1111111-1111-1111-1111-%';
DELETE FROM thesis.student_group_member WHERE id::text LIKE 'd1111111-1111-1111-1111-%';
DELETE FROM thesis.student_group WHERE id::text LIKE 'd1111111-1111-1111-1111-%';
DELETE FROM topics.topic WHERE id::text LIKE 'd1111111-1111-1111-1111-%';
DELETE FROM academic.stage WHERE batch_id::text LIKE 'd1111111-1111-1111-1111-%';
DELETE FROM academic.kltn_batch WHERE id::text LIKE 'd1111111-1111-1111-1111-%';
DELETE FROM identity.user_role WHERE user_id::text LIKE 'd1111111-1111-1111-1111-%';
DELETE FROM identity."user" WHERE id::text LIKE 'd1111111-1111-1111-1111-%';
'@
    $resetSql | & $psql -v ON_ERROR_STOP=1 -f -
    if ($LASTEXITCODE -ne 0) { throw "Failed to reset rename request demo data (exit $LASTEXITCODE)." }
    Write-Host 'Reset completed: removed rename request demo records (d1111111-1111-1111-1111-*).' -ForegroundColor Yellow
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
    v_admin_id uuid := '11111111-1111-1111-1111-111111111111';
    v_gv_khang uuid := '77777777-7777-7777-7777-777777777701'; -- gv.test.khang@ugtm.edu.vn
    v_gv_a_id  uuid := '22222222-2222-2222-2222-222222222222'; -- gv.nguyenvana@ugtm.edu.vn
    v_gv_d_id  uuid := 'a1111111-1111-1111-1111-000000000093'; -- gv.tranminhduc@ugtm.edu.vn
    v_faculty_id uuid;
    v_aa_id      uuid;
    v_template_id uuid;
    v_role_student integer;

    -- Dedicated open batch currently in Stage 3 (Week 3-10 rename window OPEN)
    v_batch_rename uuid := 'd1111111-1111-1111-1111-000000000001';

    -- 4 students across 3 demo groups
    v_s7  uuid := 'd1111111-1111-1111-1111-000000000307'; -- 22520307 (Group D1 - Ready to submit + 2 rejected history items)
    v_s8  uuid := 'd1111111-1111-1111-1111-000000000308'; -- 22520308 (Group D1 partner)
    v_s9  uuid := 'd1111111-1111-1111-1111-000000000309'; -- 22520309 (Group D2 - Active request pending Faculty at Level 2)
    v_s10 uuid := 'd1111111-1111-1111-1111-000000000310'; -- 22520310 (Group D3 - Completed 3/3 + Syllabus unlocked)

    v_t1 uuid := 'd1111111-1111-1111-1111-000000000101';
    v_t2 uuid := 'd1111111-1111-1111-1111-000000000102';
    v_t3 uuid := 'd1111111-1111-1111-1111-000000000103';

    v_g1 uuid := 'd1111111-1111-1111-1111-000000000201';
    v_g2 uuid := 'd1111111-1111-1111-1111-000000000202';
    v_g3 uuid := 'd1111111-1111-1111-1111-000000000203';
BEGIN
    SELECT id INTO v_role_student FROM identity.role WHERE role_name = 'Student' LIMIT 1;
    SELECT id INTO v_template_id FROM academic.schedule_template_version LIMIT 1;

    SELECT u.id INTO v_faculty_id
    FROM identity."user" u
    JOIN identity.user_role ur ON ur.user_id = u.id
    JOIN identity.role r ON r.id = ur.role_id
    WHERE r.role_name = 'Faculty'
    LIMIT 1;

    SELECT u.id INTO v_aa_id
    FROM identity."user" u
    JOIN identity.user_role ur ON ur.user_id = u.id
    JOIN identity.role r ON r.id = ur.role_id
    WHERE r.role_name = 'AcademicAffairs'
    LIMIT 1;

    -- Fallback advisors if SeedGradingDemo wasn't run for GV0003
    IF NOT EXISTS (SELECT 1 FROM identity."user" WHERE id = v_gv_d_id) THEN
        v_gv_d_id := v_gv_khang;
    END IF;

    -- 1. Open batch currently inside Stage 3..4 window (CURRENT_DATE - 20 .. CURRENT_DATE + 20)
    INSERT INTO academic.kltn_batch (
        id, batch_name, academic_year, semester, start_date, end_date, status,
        created_at, created_by, schedule_template_version_id, time_zone_id, updated_at, updated_by
    ) VALUES (
        v_batch_rename,
        'Đợt KLTN Thực hiện (Demo Đổi tên)',
        '2027-2028',
        2,
        CURRENT_DATE - 35,
        CURRENT_DATE + 90,
        'Open',
        NOW() - INTERVAL '35 days',
        v_admin_id,
        v_template_id,
        'Asia/Ho_Chi_Minh',
        NOW(),
        v_admin_id
    )
    ON CONFLICT (id) DO UPDATE SET
        status = 'Open',
        start_date = EXCLUDED.start_date,
        end_date = EXCLUDED.end_date,
        updated_at = NOW();

    INSERT INTO academic.stage (
        id, batch_id, order_index, stage_code, stage_name, start_date, end_date, deadline_at_utc, is_overridden
    ) VALUES
    ('d1111111-1111-1111-1111-000000000011', v_batch_rename, 1, 'TOPIC_PREPARATION',         'Chuẩn bị và công bố đề tài',      CURRENT_DATE - 35, CURRENT_DATE - 28, ((CURRENT_DATE - 28)::text || ' 23:59:00+07')::timestamptz, false),
    ('d1111111-1111-1111-1111-000000000012', v_batch_rename, 2, 'REGISTRATION_AND_SYLLABUS', 'Đăng ký và đề cương',             CURRENT_DATE - 27, CURRENT_DATE - 21, ((CURRENT_DATE - 21)::text || ' 23:59:00+07')::timestamptz, false),
    ('d1111111-1111-1111-1111-000000000013', v_batch_rename, 3, 'PROGRESS_EXECUTION',        'Thực hiện và theo dõi tiến độ',   CURRENT_DATE - 20, CURRENT_DATE + 10, ((CURRENT_DATE + 10)::text || ' 23:59:00+07')::timestamptz, false),
    ('d1111111-1111-1111-1111-000000000014', v_batch_rename, 4, 'MIDTERM_GATE',              'Đánh giá giữa kỳ',                CURRENT_DATE + 11, CURRENT_DATE + 20, ((CURRENT_DATE + 20)::text || ' 23:59:00+07')::timestamptz, false),
    ('d1111111-1111-1111-1111-000000000015', v_batch_rename, 5, 'FINAL_EVALUATION',          'Báo cáo cuối kỳ và phản biện',    CURRENT_DATE + 21, CURRENT_DATE + 60, ((CURRENT_DATE + 60)::text || ' 23:59:00+07')::timestamptz, false),
    ('d1111111-1111-1111-1111-000000000016', v_batch_rename, 6, 'DEFENSE',                   'Hội đồng và bảo vệ',              CURRENT_DATE + 61, CURRENT_DATE + 75, ((CURRENT_DATE + 75)::text || ' 23:59:00+07')::timestamptz, false),
    ('d1111111-1111-1111-1111-000000000017', v_batch_rename, 7, 'ARCHIVAL_AND_PUBLICATION',  'Lưu chiểu và công bố kết quả',    CURRENT_DATE + 76, CURRENT_DATE + 90, ((CURRENT_DATE + 90)::text || ' 23:59:00+07')::timestamptz, false)
    ON CONFLICT (id) DO UPDATE SET
        start_date = EXCLUDED.start_date,
        end_date = EXCLUDED.end_date,
        deadline_at_utc = EXCLUDED.deadline_at_utc;

    -- 2. Seed 4 demo students
    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    (v_s7,  '22520307', 'Trần Gia Minh',  '22520307@gm.uit.edu.vn', '22520307@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s8,  '22520308', 'Lê Bảo Ngọc',    '22520308@gm.uit.edu.vn', '22520308@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s9,  '22520309', 'Hoàng Đức Anh',  '22520309@gm.uit.edu.vn', '22520309@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s10, '22520310', 'Vũ Khánh Linh',  '22520310@gm.uit.edu.vn', '22520310@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO UPDATE SET
        password_hash = EXCLUDED.password_hash,
        status = 'Active',
        access_failed_count = 0,
        updated_at = NOW();

    INSERT INTO identity.user_role (user_id, role_id, assigned_at)
    SELECT s_id, v_role_student, NOW()
    FROM (VALUES (v_s7), (v_s8), (v_s9), (v_s10)) AS t(s_id)
    WHERE NOT EXISTS (
        SELECT 1 FROM identity.user_role ur WHERE ur.user_id = t.s_id AND ur.role_id = v_role_student
    );

    -- 3. Seed 3 topics & groups in open rename window
    INSERT INTO topics.topic (
        id, batch_id, advisor_id, title_vi, title_en, description, requirements, status, student_number, created_at
    ) VALUES
    (
        v_t1, v_batch_rename, v_gv_khang,
        'Hệ thống phân tích cảm xúc đa phương thức trong lớp học trực tuyến',
        'Multimodal Emotion Recognition System for Online Classrooms',
        'Nhận diện trạng thái tập trung và cảm xúc của người học qua khuôn mặt và giọng nói theo thời gian thực.',
        'PyTorch, OpenCV, WebRTC, React',
        'Approved', 2, NOW() - INTERVAL '30 days'
    ),
    (
        v_t2, v_batch_rename, v_gv_a_id,
        'Nền tảng tự động hóa kiểm thử hồi quy giao diện web bằng Playwright và LLM',
        'Automated Web UI Regression Testing Platform with Playwright and LLM',
        'Tự sinh kịch bản kiểm thử giao diện và tự sửa bộ chọn DOM khi giao diện thay đổi.',
        'TypeScript, Playwright, OpenAI API',
        'Approved', 1, NOW() - INTERVAL '30 days'
    ),
    (
        v_t3, v_batch_rename, v_gv_d_id,
        'Trợ lý trích xuất tri thức từ văn bản hành chính tiếng Việt dùng GraphRAG',
        'Vietnamese Administrative Document Knowledge Extraction Assistant via GraphRAG',
        'Xây dựng đồ thị tri thức và truy vấn ngữ cảnh đa bước cho kho văn bản hành chính.',
        'Python, Neo4j, pgvector, FastAPI',
        'Approved', 1, NOW() - INTERVAL '30 days'
    )
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.student_group (
        id, topic_id, status, created_at, allow_syllabus_resubmission
    ) VALUES
    (v_g1, v_t1, 'InProgress',       NOW() - INTERVAL '25 days', false),
    (v_g2, v_t2, 'MidtermSubmitted', NOW() - INTERVAL '25 days', false),
    (v_g3, v_t3, 'InProgress',       NOW() - INTERVAL '25 days', true)
    ON CONFLICT (id) DO UPDATE SET
        status = EXCLUDED.status,
        allow_syllabus_resubmission = EXCLUDED.allow_syllabus_resubmission;

    INSERT INTO thesis.student_group_member (
        id, group_id, student_id, batch_id, joined_at, confirmation_status, confirmed_at_utc
    ) VALUES
    ('d1111111-1111-1111-1111-000000000317', v_g1, v_s7,  v_batch_rename, NOW() - INTERVAL '24 days', 'Confirmed', NOW() - INTERVAL '24 days'),
    ('d1111111-1111-1111-1111-000000000318', v_g1, v_s8,  v_batch_rename, NOW() - INTERVAL '24 days', 'Confirmed', NOW() - INTERVAL '24 days'),
    ('d1111111-1111-1111-1111-000000000319', v_g2, v_s9,  v_batch_rename, NOW() - INTERVAL '24 days', 'Confirmed', NOW() - INTERVAL '24 days'),
    ('d1111111-1111-1111-1111-000000000320', v_g3, v_s10, v_batch_rename, NOW() - INTERVAL '24 days', 'Confirmed', NOW() - INTERVAL '24 days')
    ON CONFLICT (id) DO NOTHING;

    -- 4. Seed realistic Rename Requests covering all lifecycle states
    -- Group D1 (22520307 & 22520308): 2 past rejected requests, NO active request -> ready to compose & reuse!
    INSERT INTO thesis.rename_request (
        id, group_id, requested_by_student_id, old_title_vi, new_title_vi, old_title_en, new_title_en,
        reason, status, rejection_reason, rejected_by, rejected_at, advisor_confirmed_at, faculty_approved_at, aa_approved_at, created_at
    ) VALUES
    (
        'd1111111-1111-1111-1111-000000000801',
        v_g1, v_s7,
        'Hệ thống phân tích cảm xúc đa phương thức trong lớp học trực tuyến',
        'Ứng dụng trí tuệ nhân tạo nhận diện cảm xúc sinh viên',
        'Multimodal Emotion Recognition System for Online Classrooms',
        'AI-based Student Emotion Recognition Application',
        'Muốn rút ngắn tên đề tài cho gọn hơn trước khi viết báo cáo.',
        'RejectedByAdvisor',
        'Tên đề tài mới quá chung chung, làm mất phạm vi đa phương thức (hình ảnh + âm thanh) và ngữ cảnh lớp học trực tuyến đã đăng ký.',
        v_gv_khang,
        NOW() - INTERVAL '11 days',
        NULL, NULL, NULL,
        NOW() - INTERVAL '12 days'
    ),
    (
        'd1111111-1111-1111-1111-000000000802',
        v_g1, v_s8,
        'Hệ thống phân tích cảm xúc đa phương thức trong lớp học trực tuyến',
        'Hệ thống phân tích cảm xúc đa phương thức ứng dụng hỗ trợ chẩn đoán tâm lý y tế',
        'Multimodal Emotion Recognition System for Online Classrooms',
        'Multimodal Emotion Analysis System for Medical Psychological Screening',
        'Nhóm dự kiến mở rộng bộ dữ liệu sang kịch bản phỏng vấn tâm lý trực tuyến.',
        'RejectedByFaculty',
        'Đổi lĩnh vực ứng dụng từ giáo dục trực tuyến sang chẩn đoán y tế vượt quá phạm vi đề cương đã thẩm định. Đề nghị giữ phạm vi lớp học trực tuyến và chỉ cập nhật kỹ thuật nếu cần.',
        COALESCE(v_faculty_id, v_admin_id),
        NOW() - INTERVAL '4 days',
        NOW() - INTERVAL '5 days',
        NULL, NULL,
        NOW() - INTERVAL '6 days'
    ),
    -- Group D2 (22520309): 1 rejected + 1 active pending at Faculty (Level 2)
    (
        'd1111111-1111-1111-1111-000000000803',
        v_g2, v_s9,
        'Nền tảng tự động hóa kiểm thử hồi quy giao diện web bằng Playwright và LLM',
        'Công cụ kiểm thử web tự động thông minh',
        'Automated Web UI Regression Testing Platform with Playwright and LLM',
        'Intelligent Automated Web Testing Tool',
        'Đề nghị đơn giản hóa tên đề tài.',
        'RejectedByAdvisor',
        'Cần giữ tên công nghệ cốt lõi (Playwright và LLM) để phản ánh đúng đóng góp kỹ thuật.',
        v_gv_a_id,
        NOW() - INTERVAL '8 days',
        NULL, NULL, NULL,
        NOW() - INTERVAL '9 days'
    ),
    (
        'd1111111-1111-1111-1111-000000000804',
        v_g2, v_s9,
        'Nền tảng tự động hóa kiểm thử hồi quy giao diện web bằng Playwright và LLM',
        'Nền tảng tự động hóa kiểm thử hồi quy giao diện web kết hợp Playwright và tác tử LLM tự phục hồi bộ chọn',
        'Automated Web UI Regression Testing Platform with Playwright and LLM',
        'Self-Healing Web UI Regression Testing Platform Using Playwright and LLM Agents',
        'Bổ sung cơ chế Self-Healing DOM Selector bằng LLM Agents đã được hiện thực ở tuần 6 theo góp ý của GVHD.',
        'AdvisorConfirmed',
        NULL, NULL, NULL,
        NOW() - INTERVAL '1 day',
        NULL, NULL,
        NOW() - INTERVAL '2 days'
    ),
    -- Group D3 (22520310): 1 Completed (all 3 levels approved + syllabus unlocked)
    (
        'd1111111-1111-1111-1111-000000000805',
        v_g3, v_s10,
        'Hệ thống hỏi đáp văn bản hành chính bằng tìm kiếm ngữ nghĩa',
        'Trợ lý trích xuất tri thức từ văn bản hành chính tiếng Việt dùng GraphRAG',
        'Semantic Search Q&A System for Administrative Documents',
        'Vietnamese Administrative Document Knowledge Extraction Assistant via GraphRAG',
        'Chuyển kiến trúc truy xuất từ Vector Search thuần túy sang GraphRAG để xử lý tốt các quan hệ tham chiếu chéo giữa văn bản.',
        'Completed',
        NULL, NULL, NULL,
        NOW() - INTERVAL '8 days',
        NOW() - INTERVAL '7 days',
        NOW() - INTERVAL '6 days',
        NOW() - INTERVAL '9 days'
    )
    ON CONFLICT (id) DO NOTHING;

    RAISE NOTICE 'Seeded Rename Request Demo:';
    RAISE NOTICE '  Batch: % (Open, Stage 3-4 active for 20 days)', v_batch_rename;
    RAISE NOTICE '  D1 (22520307 / 22520308): InProgress, 2 rejected history items, READY to submit & test 3-tier flow';
    RAISE NOTICE '  D2 (22520309): MidtermSubmitted, 1 active AdvisorConfirmed request (waiting Faculty)';
    RAISE NOTICE '  D3 (22520310): InProgress, 1 Completed request (Syllabus Resubmission unlocked)';
END $$;
'@

$seedSql = $seedSql.Replace('__DEMO_PWD_HASH__', $demoPasswordHash)

$tmp = [IO.Path]::Combine([IO.Path]::GetTempPath(), "ugtm_rename_demo_$([Guid]::NewGuid().ToString('N')).sql")
try {
    [IO.File]::WriteAllText($tmp, $seedSql, [System.Text.UTF8Encoding]::new($false))
    & $psql -v ON_ERROR_STOP=1 -f $tmp
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed rename request demo data (exit $LASTEXITCODE)." }
}
finally {
    if (Test-Path -LiteralPath $tmp) {
        Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ''
Write-Host 'Rename Request Demo seeded successfully!' -ForegroundColor Green
Write-Host '  22520307@gm.uit.edu.vn / UgtmDemo@2026 -> Nhom D1 (San sang gui don + 2 don bi tu choi de Xem / Dung lai noi dung)'
Write-Host '  22520308@gm.uit.edu.vn / UgtmDemo@2026 -> Nhom D1 (Thanh vien cung nhom voi 22520307)'
Write-Host '  22520309@gm.uit.edu.vn / UgtmDemo@2026 -> Nhom D2 (Dang co don cho Khoa duyet cap 2)'
Write-Host '  22520310@gm.uit.edu.vn / UgtmDemo@2026 -> Nhom D3 (Da hoan tat 3/3 cap + Mo khoa nop lai De cuong)'
Write-Host '  gv.test.khang@ugtm.edu.vn / UgtmDemo@2026 -> GVHD Nhom D1 (nhan & duyet don cap 1 khi 22520307 gui)'
