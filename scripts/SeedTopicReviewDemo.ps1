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
    if ($map['ssl mode'] -match 'require|verify') {
        $env:PGSSLMODE = 'require'
    }
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
if (-not $connectionString) {
    throw 'DATABASE_CONNECTION_STRING is not configured (env var or .env).'
}

Set-PgEnvFromConnectionString -ConnectionString $connectionString
$psql = Get-PsqlPath

if ($Reset) {
    $resetSql = @"
DELETE FROM topics.topic
WHERE id::text LIKE '99999999-9999-9999-9999-%';
"@
    $resetSql | & $psql -v ON_ERROR_STOP=1 -f -
    if ($LASTEXITCODE -ne 0) { throw "Failed to reset demo topic review records (exit $LASTEXITCODE)." }
    Write-Host 'Reset completed: removed demo topics (99999999-9999-9999-9999-*).' -ForegroundColor Yellow
    return
}

$seedSql = @'
DO $$
DECLARE
    v_batch_hk2 uuid;
    v_batch_test uuid;
    v_gv1 uuid;
    v_gv2 uuid;
    v_gv3 uuid;
BEGIN
    SELECT id INTO v_batch_hk2
    FROM academic.kltn_batch
    WHERE id = 'e1a10001-0000-0000-0000-000000000002'
       OR status = 'Open'
    ORDER BY (id = 'e1a10001-0000-0000-0000-000000000002') DESC, created_at DESC
    LIMIT 1;

    SELECT id INTO v_batch_test
    FROM academic.kltn_batch
    WHERE id = 'ad3bc9b1-0163-4a99-a711-ecb8bdc0400b'
       OR (status = 'Open' AND id <> v_batch_hk2)
    ORDER BY (id = 'ad3bc9b1-0163-4a99-a711-ecb8bdc0400b') DESC, created_at DESC
    LIMIT 1;

    IF v_batch_test IS NULL THEN
        v_batch_test := v_batch_hk2;
    END IF;

    IF v_batch_hk2 IS NULL THEN
        RAISE EXCEPTION 'No Open batch found in academic.kltn_batch for seeding demo topics.';
    END IF;

    SELECT u.id INTO v_gv1
    FROM identity."user" u
    JOIN identity.user_role ur ON ur.user_id = u.id
    JOIN identity.role r ON r.id = ur.role_id
    WHERE r.role_name = 'Lecturer' AND u.status = 'Active'
    ORDER BY (u.user_code = 'GV_TEST_01') DESC, u.user_code
    LIMIT 1;

    SELECT u.id INTO v_gv2
    FROM identity."user" u
    JOIN identity.user_role ur ON ur.user_id = u.id
    JOIN identity.role r ON r.id = ur.role_id
    WHERE r.role_name = 'Lecturer' AND u.status = 'Active' AND u.id <> v_gv1
    ORDER BY u.user_code
    LIMIT 1;

    SELECT u.id INTO v_gv3
    FROM identity."user" u
    JOIN identity.user_role ur ON ur.user_id = u.id
    JOIN identity.role r ON r.id = ur.role_id
    WHERE r.role_name = 'Lecturer' AND u.status = 'Active' AND u.id NOT IN (v_gv1, COALESCE(v_gv2, v_gv1))
    ORDER BY u.user_code
    LIMIT 1;

    v_gv2 := COALESCE(v_gv2, v_gv1);
    v_gv3 := COALESCE(v_gv3, v_gv2, v_gv1);

    IF v_gv1 IS NULL THEN
        RAISE EXCEPTION 'No Active Lecturer found in identity."user" for seeding demo topics.';
    END IF;

    INSERT INTO topics.topic (
        id, batch_id, advisor_id, title_vi, title_en, description, requirements,
        status, student_number, created_at, review_note
    ) VALUES
    -- 5 Submitted (Chờ duyệt)
    (
        '99999999-9999-9999-9999-000000000101',
        v_batch_hk2,
        v_gv1,
        'Trợ lý hỏi đáp học vụ đa phương thức dựa trên GraphRAG cho sinh viên UIT',
        'Multimodal Academic Advising Assistant Using GraphRAG',
        E'Nghiên cứu kết hợp đồ thị tri thức quy chế đào tạo (GraphRAG) và mô hình ngôn ngữ lớn để truy xuất chính xác điều kiện tiên quyết, chuẩn đầu ra và kế hoạch học tập.\nHệ thống hỗ trợ trích dẫn điều khoản gốc nhằm hạn chế ảo giác thông tin và cung cấp giao diện trò chuyện trực quan.',
        E'- Thành thạo Python, FastAPI hoặc .NET Core.\n- Có kiến thức về Vector Database, Neo4j/GraphRAG và React/TypeScript.\n- Có khả năng thiết kế bộ câu hỏi đánh giá độ chính xác (RAGAS).',
        'Submitted',
        2,
        NOW() - INTERVAL '3 hours',
        NULL
    ),
    (
        '99999999-9999-9999-9999-000000000102',
        v_batch_hk2,
        v_gv2,
        'Phát hiện bất thường và cảnh báo sớm xâm nhập mạng nội bộ bằng học sâu trên đồ thị',
        'Network Intrusion Anomaly Detection Using Graph Neural Networks',
        E'Xây dựng pipeline thu thập luồng mạng (NetFlow/Zeek), mô hình hóa quan hệ giao tiếp giữa các node bằng GraphSAGE/GAT để phát hiện hành vi di chuyển ngang (lateral movement) trong mạng campus.',
        E'- Nắm vững mạng máy tính, Wireshark/Zeek.\n- Biết PyTorch Geometric hoặc DGL và Docker.',
        'Submitted',
        2,
        NOW() - INTERVAL '9 hours',
        NULL
    ),
    (
        '99999999-9999-9999-9999-000000000103',
        v_batch_hk2,
        v_gv3,
        'Nền tảng kiểm thử tự động API và phát hiện sai lệch hợp đồng OpenAPI trong CI/CD',
        'Automated API Contract Drift Detection and Testing Platform for CI/CD',
        E'Phát triển công cụ so sánh đặc tả OpenAPI qua các commit Git, tự động sinh ca kiểm thử biên (contract & fuzz tests) và gửi báo cáo hồi quy ngay trên Pull Request.',
        E'- Hiểu REST/OpenAPI 3.1, GitHub Actions.\n- Lập trình tốt TypeScript hoặc Go/C#.',
        'Submitted',
        1,
        NOW() - INTERVAL '1 day',
        NULL
    ),
    (
        '99999999-9999-9999-9999-000000000104',
        v_batch_test,
        v_gv1,
        'Hệ thống nhận dạng chữ viết tay tiếng Việt trên phiếu chấm điểm bảo vệ khóa luận',
        'Vietnamese Handwritten Recognition System for Thesis Evaluation Sheets',
        E'Ứng dụng mô hình Transformer-OCR kết hợp hậu xử lý theo từ điển danh mục để số hóa tự động điểm số, MSSV và nhận xét viết tay từ ảnh chụp biên bản hội đồng.',
        E'- Có nền tảng Computer Vision (PyTorch, OpenCV).\n- Biết gán nhãn và tinh chỉnh mô hình OCR.',
        'Submitted',
        2,
        NOW() - INTERVAL '2 days',
        NULL
    ),
    (
        '99999999-9999-9999-9999-000000000105',
        v_batch_test,
        v_gv2,
        'Ứng dụng quản lý tiêu thụ năng lượng phòng lab thông minh dựa trên IoT và dự báo chuỗi thời gian',
        'Smart Lab Energy Management Using IoT Sensors and Time-Series Forecasting',
        E'Thu thập dữ liệu cảm biến điện năng qua MQTT/TimescaleDB, dự báo phụ tải theo khung giờ học thực tế và đề xuất lịch bật tắt thiết bị tự động tiết kiệm điện.',
        E'- Biết MQTT, ESP32 hoặc mô phỏng cảm biến.\n- Có kinh nghiệm React/Next.js và Python forecasting.',
        'Submitted',
        2,
        NOW() - INTERVAL '3 days',
        NULL
    ),
    -- 4 Approved (Chờ công bố - sẵn sàng tick chọn nhiều đề tài cùng đợt)
    (
        '99999999-9999-9999-9999-000000000106',
        v_batch_test,
        v_gv1,
        'Tối ưu hóa truy vấn và phân mảnh dữ liệu phân tán cho hệ thống quản lý đào tạo',
        'Distributed Query Optimization and Sharding for Academic Management Systems',
        E'Đánh giá hiệu năng phân mảnh ngang (Citus/PostgreSQL) và chiến lược index hỗn hợp trên tập dữ liệu hàng triệu bản ghi điểm và đăng ký học phần.',
        E'- Hiểu sâu PostgreSQL execution plan, partitioning.\n- Có kỹ năng benchmark k6/JMeter.',
        'Approved',
        2,
        NOW() - INTERVAL '4 days',
        'Đề cương rõ ràng, phạm vi thực nghiệm và chỉ số đo lường hiệu năng hợp lý.'
    ),
    (
        '99999999-9999-9999-9999-000000000107',
        v_batch_test,
        v_gv3,
        'Hệ thống đối soát tương đồng ngữ nghĩa báo cáo khóa luận tốt nghiệp',
        'Semantic Plagiarism Checking System for Graduation Theses',
        E'Xây dựng kiến trúc tách câu theo cấu trúc học thuật, tạo embedding đa ngôn ngữ Việt-Anh và hiển thị trực quan đoạn văn bản nghi vấn kèm nguồn đối chiếu.',
        E'- Nắm vững NLP embedding (BGE-M3/SBERT) và pgvector/Qdrant.\n- Kinh nghiệm xử lý PDF/DOCX.',
        'Approved',
        2,
        NOW() - INTERVAL '5 days',
        NULL
    ),
    (
        '99999999-9999-9999-9999-000000000108',
        v_batch_test,
        v_gv2,
        'Trình trực quan hóa lộ trình kiến thức và gợi ý môn tự chọn theo năng lực sinh viên',
        'Interactive Curriculum Knowledge Map and Personalized Elective Recommender',
        E'Mô hình hóa mạng lưới phụ thuộc môn học và kỹ năng nghề nghiệp để gợi ý môn tự chọn phù hợp định hướng AI, Kỹ thuật phần mềm hoặc An toàn thông tin.',
        E'- Thành thạo React Flow / D3.js và thuật toán gợi ý.\n- Thiết kế UI/UX thân thiện.',
        'Approved',
        1,
        NOW() - INTERVAL '6 days',
        'Phù hợp định hướng công bố đợt mới cho SV lựa chọn.'
    ),
    (
        '99999999-9999-9999-9999-000000000109',
        v_batch_hk2,
        v_gv1,
        'Nền tảng triển khai và giám sát mô hình AI nội bộ với vLLM và Kubernetes',
        'Internal AI Model Serving and Observability Platform with vLLM on Kubernetes',
        E'Thiết kế control-plane cấp phát GPU quota theo khoa/bộ môn, giám sát độ trễ token/s bằng Prometheus & Grafana và tự động scale worker.',
        E'- Có kiến thức Docker, Kubernetes, Prometheus/Grafana.\n- Hiểu cơ chế serving LLM.',
        'Approved',
        2,
        NOW() - INTERVAL '7 days',
        'Đã thẩm định đạt chuẩn kỹ thuật.'
    ),
    -- 2 NeedRevision (Yêu cầu chỉnh sửa)
    (
        '99999999-9999-9999-9999-000000000110',
        v_batch_hk2,
        v_gv2,
        'Ứng dụng blockchain lưu trữ minh chứng hoạt động ngoại khóa sinh viên',
        'Blockchain-Based Extracurricular Credential Verification App',
        E'Lưu trữ và xác thực giấy chứng nhận tham gia hoạt động phong trào, hội thảo khoa học trên mạng lưới blockchain.',
        E'- Hiểu Smart Contract Solidity.\n- Phát triển Web App React.',
        'NeedRevision',
        2,
        NOW() - INTERVAL '8 days',
        'Đề nghị làm rõ lý do bắt buộc dùng blockchain thay vì chữ ký số truyền thống và bổ sung kịch bản thu hồi chứng chỉ.'
    ),
    (
        '99999999-9999-9999-9999-000000000111',
        v_batch_test,
        v_gv3,
        'Xây dựng chatbot tư vấn tuyển sinh sử dụng AI',
        NULL,
        E'Phát triển chatbot hỗ trợ thí sinh hỏi đáp về điểm chuẩn và ngành học của trường.',
        E'- Biết Python và xây dựng web cơ bản.',
        'NeedRevision',
        2,
        NOW() - INTERVAL '9 days',
        'Cần bổ sung tên đề tài tiếng Anh theo quy định, xác định rõ kỹ thuật RAG/đánh giá chất lượng và phạm vi dữ liệu cụ thể hơn cho 2 SV.'
    ),
    -- 1 Rejected (Không được duyệt)
    (
        '99999999-9999-9999-9999-000000000112',
        v_batch_hk2,
        v_gv3,
        'Website bán hàng thời trang trực tuyến cơ bản',
        'Basic Online Fashion E-Commerce Website',
        E'Xây dựng website giới thiệu sản phẩm, giỏ hàng và trang quản trị đơn hàng.',
        E'- HTML, CSS, JavaScript và Node.js cơ bản.',
        'Rejected',
        2,
        NOW() - INTERVAL '10 days',
        'Phạm vi đề tài mang tính bài tập đồ án môn học phổ thông, chưa đạt mức độ nghiên cứu và độ phức tạp kỹ thuật của Khóa luận tốt nghiệp.'
    )
    ON CONFLICT (id) DO UPDATE SET
        batch_id = EXCLUDED.batch_id,
        advisor_id = EXCLUDED.advisor_id,
        title_vi = EXCLUDED.title_vi,
        title_en = EXCLUDED.title_en,
        description = EXCLUDED.description,
        requirements = EXCLUDED.requirements,
        status = EXCLUDED.status,
        student_number = EXCLUDED.student_number,
        review_note = EXCLUDED.review_note;
END $$;
'@

$seedSql | & $psql -v ON_ERROR_STOP=1 -f -
if ($LASTEXITCODE -ne 0) { throw "Failed to seed topic review demo records (exit $LASTEXITCODE)." }

Write-Host 'Seeded demo topics for /topics/review successfully.' -ForegroundColor Green
