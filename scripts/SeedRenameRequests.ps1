[CmdletBinding()]
param(
    [switch]$Reset
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot '.env'

function Get-PsqlPath {
    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = Get-ChildItem -Path 'C:\Program Files\PostgreSQL' -Filter psql.exe -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if ($candidates) { return $candidates }

    throw 'Could not locate psql.exe. Ensure PostgreSQL client tools are installed or on PATH.'
}

function Set-PgEnvFromConnectionString {
    param([string]$ConnectionString)

    $map = @{}
    foreach ($part in ($ConnectionString -split ';')) {
        if ($part -match '^\s*([^=]+)=(.*)$') {
            $map[$Matches[1].Trim().ToLowerInvariant()] = $Matches[2].Trim()
        }
    }

    $env:PGHOST = if ($map['host']) { $map['host'] } elseif ($map['server']) { $map['server'] } else { 'localhost' }
    $env:PGPORT = if ($map['port']) { $map['port'] } else { '5432' }
    $env:PGDATABASE = $map['database']
    $env:PGUSER = if ($map['username']) { $map['username'] } elseif ($map['user id']) { $map['user id'] } elseif ($map['uid']) { $map['uid'] } else { 'postgres' }
    $env:PGPASSWORD = $map['password']
    $env:PGCLIENTENCODING = 'UTF8'
}

if (-not [Environment]::GetEnvironmentVariable('DATABASE_CONNECTION_STRING', 'Process') -and (Test-Path -LiteralPath $environmentFile)) {
    foreach ($line in Get-Content -LiteralPath $environmentFile) {
        if ($line -match '^\s*DATABASE_CONNECTION_STRING\s*=(.*)$') {
            $variableValue = $Matches[1].Trim().Trim('"').Trim("'")
            if ($variableValue) {
                [Environment]::SetEnvironmentVariable('DATABASE_CONNECTION_STRING', $variableValue, 'Process')
            }
        }
    }
}

$connectionString = [Environment]::GetEnvironmentVariable('DATABASE_CONNECTION_STRING', 'Process')
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw 'DATABASE_CONNECTION_STRING is not configured in environment or .env.'
}

$psql = Get-PsqlPath
$tempSqlFile = [IO.Path]::Combine([IO.Path]::GetTempPath(), "ugtm_seed_rename_$([Guid]::NewGuid().ToString('N')).sql")

$sqlContent = @"
BEGIN;

$(if ($Reset) { "DELETE FROM thesis.rename_request WHERE id::text LIKE 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa00%';" } else { '' })

WITH seed_specs (
    seed_id,
    group_id,
    old_title_vi,
    new_title_vi,
    old_title_en,
    new_title_en,
    reason,
    status,
    advisor_confirmed_at,
    faculty_approved_at,
    aa_approved_at,
    created_at,
    rejected_at,
    rejection_reason,
    reject_by_advisor
) AS (
    VALUES
    (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001'::uuid,
        '99999999-9999-9999-9999-999999990101'::uuid,
        '[TEST-GVPB] Hệ thống gợi ý đề tài KLTN bằng Semantic Search',
        '[TEST-GVPB] Hệ thống gợi ý đề tài khóa luận đa ngôn ngữ bằng Hybrid Search và LLM',
        'Semantic Search Thesis Topic Recommendation System',
        'Multilingual Thesis Topic Recommendation System Using Hybrid Search and LLM',
        'Mở rộng phạm vi tìm kiếm từ đơn ngữ sang đa ngôn ngữ và kết hợp tái xếp hạng bằng LLM để tăng độ chính xác gợi ý theo góp ý của GVHD.',
        'Created',
        NULL::timestamptz,
        NULL::timestamptz,
        NULL::timestamptz,
        NOW() - INTERVAL '6 hours',
        NULL::timestamptz,
        NULL::text,
        FALSE
    ),
    (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002'::uuid,
        '99999999-9999-9999-9999-999999990103'::uuid,
        '[TEST-GVPB] Phân tích và phát hiện bất thường giao dịch tài chính theo thời gian thực',
        '[TEST-GVPB] Phát hiện gian lận giao dịch tài chính thời gian thực bằng Graph Neural Networks',
        'Real-Time Financial Transaction Anomaly Detection',
        'Real-Time Financial Transaction Fraud Detection Using Graph Neural Networks',
        'Cụ thể hóa mô hình cốt lõi là Graph Neural Networks (GNN) thay vì nêu chung chung "bất thường" để phản ánh đúng trọng tâm đồ án của nhóm.',
        'Created',
        NULL::timestamptz,
        NULL::timestamptz,
        NULL::timestamptz,
        NOW() - INTERVAL '14 hours',
        NULL::timestamptz,
        NULL::text,
        FALSE
    ),
    (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0003'::uuid,
        '99999999-9999-9999-9999-999999990001'::uuid,
        '[TEST] Hệ thống phát hiện gian lận thi trực tuyến',
        '[TEST] Hệ thống giám sát và phát hiện hành vi bất thường thi trực tuyến bằng Computer Vision',
        NULL,
        'Computer Vision-Based Online Exam Proctoring System',
        'Thu hẹp phạm vi từ giám sát tổng quát sang bài toán Computer Vision (theo dõi ánh mắt và tư thế) theo đề nghị sau buổi góp ý đề cương.',
        'AdvisorConfirmed',
        NOW() - INTERVAL '1 day',
        NULL::timestamptz,
        NULL::timestamptz,
        NOW() - INTERVAL '2 days',
        NULL::timestamptz,
        NULL::text,
        FALSE
    ),
    (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0004'::uuid,
        '99999999-9999-9999-9999-999999990102'::uuid,
        '[TEST-GVPB] Xây dựng hệ thống giám sát dịch vụ Microservices với OpenTelemetry',
        '[TEST-GVPB] Nền tảng quan sát phân tán và cảnh báo sự cố Microservices với OpenTelemetry',
        'Microservices Observability Platform Using OpenTelemetry',
        'Distributed Observability and Alerting Platform for Microservices Using OpenTelemetry',
        'Bổ sung phân hệ cảnh báo sự cố dựa trên trace và metric thay vì chỉ dừng ở mức thu thập dữ liệu quan sát.',
        'AdvisorConfirmed',
        NOW() - INTERVAL '18 hours',
        NULL::timestamptz,
        NULL::timestamptz,
        NOW() - INTERVAL '3 days',
        NULL::timestamptz,
        NULL::text,
        FALSE
    ),
    (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0005'::uuid,
        '99999999-9999-9999-9999-999999990002'::uuid,
        '[TEST] Tối ưu truy vấn dữ liệu phân tán PostgreSQL',
        '[TEST] Tối ưu hóa hiệu năng truy vấn phân tán PostgreSQL kết hợp Citus và Partitioning',
        NULL,
        'Distributed Query Optimization on PostgreSQL with Citus and Partitioning',
        'Làm rõ kiến trúc thử nghiệm với Citus và Partitioning theo yêu cầu cụ thể của Hội đồng thẩm định đề cương Khoa.',
        'FacultyApproved',
        NOW() - INTERVAL '3 days',
        NOW() - INTERVAL '12 hours',
        NULL::timestamptz,
        NOW() - INTERVAL '4 days',
        NULL::timestamptz,
        NULL::text,
        FALSE
    ),
    (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0006'::uuid,
        '99999999-9999-9999-9999-999999990003'::uuid,
        '[TEST] Nền tảng quản lý thực tập doanh nghiệp',
        '[TEST] Hệ thống kết nối và đánh giá thực tập sinh doanh nghiệp tích hợp chữ ký số',
        NULL,
        'Enterprise Internship Matching and Evaluation Platform with Digital Signature',
        'Bổ sung luồng xác nhận biên bản đánh giá thực tập bằng chữ ký số theo góp ý từ doanh nghiệp đối tác.',
        'FacultyApproved',
        NOW() - INTERVAL '4 days',
        NOW() - INTERVAL '6 hours',
        NULL::timestamptz,
        NOW() - INTERVAL '5 days',
        NULL::timestamptz,
        NULL::text,
        FALSE
    ),
    (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0007'::uuid,
        '99999999-9999-9999-9999-999999990004'::uuid,
        '[TEST] Nghiên cứu ứng dụng AI hỗ trợ phân tích log hệ thống doanh nghiệp',
        '[TEST] Ứng dụng phân tích log hệ thống bằng AI',
        'Enterprise System Log Analytics with AI',
        'AI-Driven System Log Analytics Application',
        'Rút gọn và chuẩn hóa thuật ngữ tiếng Việt theo quy định đặt tên khóa luận tốt nghiệp của Phòng đào tạo.',
        'Completed',
        NOW() - INTERVAL '8 days',
        NOW() - INTERVAL '6 days',
        NOW() - INTERVAL '5 days',
        NOW() - INTERVAL '9 days',
        NULL::timestamptz,
        NULL::text,
        FALSE
    ),
    (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0008'::uuid,
        '99999999-9999-9999-9999-999999990005'::uuid,
        '[TEST] Hệ thống gợi ý lộ trình học tập cá nhân hóa',
        '[TEST] Hệ thống AI toàn diện hỗ trợ mọi hoạt động học tập đại học',
        NULL,
        'Comprehensive AI Platform for University Learning',
        'Muốn mở rộng tên đề tài để tích hợp thêm chatbot hỏi đáp và lập lịch học cá nhân.',
        'RejectedByFaculty',
        NOW() - INTERVAL '6 days',
        NULL::timestamptz,
        NULL::timestamptz,
        NOW() - INTERVAL '7 days',
        NOW() - INTERVAL '5 days',
        'Tên đề tài mới quá rộng ("hỗ trợ mọi hoạt động"), chưa thể hiện rõ bài toán thuật toán và phạm vi đánh giá thực nghiệm.',
        FALSE
    ),
    (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0009'::uuid,
        '99999999-9999-9999-9999-999999990006'::uuid,
        '[TEST] Cổng thanh toán học phí nội bộ',
        '[TEST] Xây dựng sàn thương mại điện tử đa ngành hàng',
        NULL,
        'Multi-Vendor E-Commerce Marketplace Platform',
        'Nhóm muốn đổi sang đề tài thương mại điện tử để làm quen với kiến trúc giỏ hàng và khuyến mãi.',
        'RejectedByAdvisor',
        NULL::timestamptz,
        NULL::timestamptz,
        NULL::timestamptz,
        NOW() - INTERVAL '3 days',
        NOW() - INTERVAL '2 days',
        'Tên đề tài mới lệch hoàn toàn khỏi hướng nghiên cứu cổng thanh toán học phí nội bộ đã thống nhất từ đầu kỳ.',
        TRUE
    )
),
resolved_rows AS (
    SELECT
        s.seed_id,
        s.group_id,
        (
            SELECT m.student_id
            FROM thesis.student_group_member m
            WHERE m.group_id = s.group_id AND m.confirmation_status = 'Confirmed'
            ORDER BY m.joined_at ASC
            LIMIT 1
        ) AS requested_by_student_id,
        s.old_title_vi,
        s.new_title_vi,
        s.old_title_en,
        s.new_title_en,
        s.reason,
        s.status,
        s.advisor_confirmed_at,
        s.faculty_approved_at,
        s.aa_approved_at,
        s.created_at,
        s.rejected_at,
        CASE
            WHEN s.rejected_at IS NULL THEN NULL
            WHEN s.reject_by_advisor THEN t.advisor_id
            ELSE COALESCE(
                (
                    SELECT u.id
                    FROM identity."user" u
                    JOIN identity.user_role ur ON ur.user_id = u.id
                    JOIN identity.role r ON r.id = ur.role_id
                    WHERE r.role_name IN ('Faculty', 'Admin')
                    ORDER BY CASE WHEN r.role_name = 'Faculty' THEN 0 ELSE 1 END
                    LIMIT 1
                ),
                t.advisor_id
            )
        END AS rejected_by,
        s.rejection_reason
    FROM seed_specs s
    JOIN thesis.student_group g ON g.id = s.group_id
    JOIN topics.topic t ON t.id = g.topic_id
)
INSERT INTO thesis.rename_request (
    id,
    group_id,
    requested_by_student_id,
    old_title_vi,
    new_title_vi,
    old_title_en,
    new_title_en,
    reason,
    status,
    advisor_confirmed_at,
    faculty_approved_at,
    aa_approved_at,
    created_at,
    rejected_at,
    rejected_by,
    rejection_reason
)
SELECT
    r.seed_id,
    r.group_id,
    r.requested_by_student_id,
    r.old_title_vi,
    r.new_title_vi,
    r.old_title_en,
    r.new_title_en,
    r.reason,
    r.status,
    r.advisor_confirmed_at,
    r.faculty_approved_at,
    r.aa_approved_at,
    r.created_at,
    r.rejected_at,
    r.rejected_by,
    r.rejection_reason
FROM resolved_rows r
WHERE r.requested_by_student_id IS NOT NULL
  AND NOT (
      r.status IN ('Created', 'AdvisorConfirmed', 'FacultyApproved')
      AND EXISTS (
          SELECT 1
          FROM thesis.rename_request existing
          WHERE existing.group_id = r.group_id
            AND existing.status IN ('Created', 'AdvisorConfirmed', 'FacultyApproved')
            AND existing.id <> r.seed_id
      )
  )
ON CONFLICT (id) DO NOTHING;

COMMIT;

SELECT status, COUNT(*) AS count
FROM thesis.rename_request
GROUP BY status
ORDER BY status;
"@

try {
    [IO.File]::WriteAllText($tempSqlFile, $sqlContent, [System.Text.UTF8Encoding]::new($false))
    Set-PgEnvFromConnectionString -ConnectionString $connectionString
    & $psql -X -v ON_ERROR_STOP=1 -f $tempSqlFile
    if ($LASTEXITCODE -ne 0) {
        throw "psql failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item Env:PGHOST, Env:PGPORT, Env:PGDATABASE, Env:PGUSER, Env:PGPASSWORD, Env:PGCLIENTENCODING -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $tempSqlFile) {
        Remove-Item -LiteralPath $tempSqlFile -Force -ErrorAction SilentlyContinue
    }
}
