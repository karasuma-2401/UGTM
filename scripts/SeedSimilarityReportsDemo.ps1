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

function Invoke-PsqlFile([string]$SqlText) {
    $tmp = [IO.Path]::Combine([IO.Path]::GetTempPath(), "ugtm_similarity_seed_$([Guid]::NewGuid().ToString('N')).sql")
    try {
        [IO.File]::WriteAllText($tmp, $SqlText, [System.Text.UTF8Encoding]::new($false))
        $output = & $psql -v ON_ERROR_STOP=1 -f $tmp 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "psql failed (exit $LASTEXITCODE):`n$($output -join "`n")"
        }
        return $output
    } finally {
        if (Test-Path -LiteralPath $tmp) {
            Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
        }
    }
}

function ConvertTo-SqlLiteral([string]$Value) {
    if ($null -eq $Value) { return 'NULL' }
    return "'" + $Value.Replace("'", "''") + "'"
}

if ($Reset) {
    $resetSql = @'
DELETE FROM thesis.final_report_plagiarism_scan
WHERE id::text LIKE '88888888-8888-8888-8888-%';

UPDATE thesis.final_report_plagiarism_scan
SET raw_report_json = NULL,
    similarity_coverage = 8.6,
    max_reranker_score = 0.64,
    scanned_chunks = 112,
    scanned_characters = 68400,
    matched_characters = 5882,
    cited_findings_count = 4,
    uncited_findings_count = 0,
    suspected_findings_count = 0
WHERE id = '99999999-9999-9999-9999-000000000831';
'@
    Invoke-PsqlFile $resetSql | Out-Null
    Write-Host 'Reset similarity demo scans completed.' -ForegroundColor Yellow
    exit 0
}

function New-SchemaV3Json {
    param(
        [double]$Coverage,
        [double]$MaxScore,
        [int]$ScannedChunks,
        [int]$ScannedChars,
        [int]$MatchedChars,
        [int]$Cited,
        [int]$Uncited,
        [int]$Suspected,
        [array]$Findings,
        [string]$Disclaimer = 'Bằng chứng tương đồng phục vụ tham chiếu thẩm định chuyên môn cho Hội đồng và GVHD. Hệ thống không tự động kết luận vi phạm liêm chính học thuật.'
    )

    $payload = [ordered]@{
        schema_version = 'v3'
        summary        = [ordered]@{
            similarity_coverage  = $Coverage
            max_reranker_score   = $MaxScore
            scanned_chunks       = $ScannedChunks
            scanned_characters   = $ScannedChars
            matched_characters   = $MatchedChars
            cited_findings       = $Cited
            uncited_findings     = $Uncited
            suspected_findings   = $Suspected
        }
        findings       = $Findings
        disclaimer     = $Disclaimer
    }
    return ($payload | ConvertTo-Json -Depth 8 -Compress)
}

# 1) SecGuard CI/CD (99999999-...-0403 v2) - 8.6% low similarity, 4 cited findings
$secGuardJson = New-SchemaV3Json -Coverage 0.086 -MaxScore 0.64 -ScannedChunks 112 -ScannedChars 68400 -MatchedChars 5882 -Cited 4 -Uncited 0 -Suspected 0 -Findings @(
    [ordered]@{
        passage_group_id        = 'pg-sec-01'
        submission_chunk_no     = 14
        submission_page         = 12
        submission_start        = 410
        submission_end          = 760
        submission_text         = 'Mô hình phân tích cây cú pháp trừu tượng (Abstract Syntax Tree - AST) cho phép trích xuất chính xác luồng điều khiển và luồng dữ liệu của hàm mà không bị nhiễu bởi tên biến hay chú thích.'
        source_key              = 'uit_thesis_2024_ast_security.pdf'
        source_version          = 1
        source_filename         = 'KLTN_2024_PhanTichMaNguonAST.pdf'
        source_page             = 18
        source_text             = 'Việc biểu diễn mã nguồn dưới dạng cây cú pháp trừu tượng (AST) giúp bóc tách cấu trúc ngữ nghĩa và luồng dữ liệu độc lập với quy ước đặt tên biến cục bộ.'
        reranker_score          = 0.64
        classifier_score        = 0.92
        citation_status         = 'Cited'
        explanation             = 'Có trích dẫn tài liệu tham khảo [12] ngay cuối đoạn tổng quan kỹ thuật AST.'
        potential_misattribution = $false
        review_required         = $false
    },
    [ordered]@{
        passage_group_id        = 'pg-sec-02'
        submission_chunk_no     = 29
        submission_page         = 24
        submission_start        = 120
        submission_end          = 460
        submission_text         = 'Quy trình tích hợp liên tục (CI/CD pipeline) trên GitHub Actions kích hoạt bộ lọc Semgrep trước khi gửi các đoạn mã nghi ngờ (sink-to-source slice) sang mô hình ngôn ngữ lớn để giảm độ trễ.'
        source_key              = 'ieee_2025_llm_sast.pdf'
        source_version          = 1
        source_filename         = 'IEEE_Access_2025_Hybrid_SAST_LLM.pdf'
        source_page             = 4
        source_text             = 'Combining lightweight static rule filters with targeted LLM slicing significantly reduces token overhead during automated Pull Request security gating.'
        reranker_score          = 0.58
        classifier_score        = 0.89
        citation_status         = 'Cited'
        explanation             = 'Đã dẫn nguồn bài báo IEEE [19] về kiến trúc lọc hai tầng SAST + LLM.'
        potential_misattribution = $false
        review_required         = $false
    }
)

# 2) eBPF Log Anomaly (b2222222-...-0401 v1) - 8.4% low similarity, 2 cited findings
$ebpfJson = New-SchemaV3Json -Coverage 0.084 -MaxScore 0.61 -ScannedChunks 98 -ScannedChars 62100 -MatchedChars 5216 -Cited 2 -Uncited 0 -Suspected 0 -Findings @(
    [ordered]@{
        passage_group_id        = 'pg-ebpf-01'
        submission_chunk_no     = 8
        submission_page         = 9
        submission_start        = 80
        submission_end          = 390
        submission_text         = 'Cơ chế eBPF cho phép nạp chương trình vào không gian nhân Linux một cách an toàn thông qua bộ kiểm chứng (verifier) nhằm thu thập syscall với chi phí ngữ cảnh tối thiểu.'
        source_key              = 'uit_thesis_2024_linux_ebpf.pdf'
        source_version          = 1
        source_filename         = 'KLTN_2024_GiamSatHeThong_eBPF.pdf'
        source_page             = 11
        source_text             = 'Chương trình eBPF được kiểm tra tính dừng và giới hạn bộ nhớ bởi kernel verifier trước khi gắn vào các hook hệ thống như kprobe và tracepoint.'
        reranker_score          = 0.61
        classifier_score        = 0.90
        citation_status         = 'Cited'
        explanation             = 'Sinh viên đã trích dẫn đúng chuẩn tài liệu eBPF Foundation [3] và luận văn tham khảo [7].'
        potential_misattribution = $false
        review_required         = $false
    },
    [ordered]@{
        passage_group_id        = 'pg-ebpf-02'
        submission_chunk_no     = 41
        submission_page         = 31
        submission_start        = 210
        submission_end          = 540
        submission_text         = 'Cửa sổ trượt theo thời gian (tumbling window 5 giây) trên Apache Flink giúp tổng hợp chuỗi tần suất syscall trước khi đưa qua mô hình Isolation Forest phát hiện bất thường.'
        source_key              = 'corpus_stream_anomaly_2023.pdf'
        source_version          = 1
        source_filename         = 'LuanVan_2023_StreamProcessing_Flink.pdf'
        source_page             = 27
        source_text             = 'Việc gom nhóm sự kiện theo cửa sổ trượt cố định trên Flink tạo vector đặc trưng ổn định cho thuật toán phát hiện bất thường thời gian thực.'
        reranker_score          = 0.54
        classifier_score        = 0.87
        citation_status         = 'Cited'
        explanation             = 'Có chú thích trích dẫn phương pháp xử lý luồng tại mục 2.3 [14].'
        potential_misattribution = $false
        review_required         = $false
    }
)

# 3) Hybrid RAG UIT (a1111111-...-0403 v1) - 24.6% warning, 2 cited + 3 uncited
$hybridRagJson = New-SchemaV3Json -Coverage 0.246 -MaxScore 0.86 -ScannedChunks 104 -ScannedChars 64500 -MatchedChars 15867 -Cited 2 -Uncited 3 -Suspected 0 -Findings @(
    [ordered]@{
        passage_group_id        = 'pg-rag-01'
        submission_chunk_no     = 22
        submission_page         = 19
        submission_start        = 150
        submission_end          = 540
        submission_text         = 'Phương pháp Reciprocal Rank Fusion (RRF) kết hợp bảng xếp hạng từ tìm kiếm từ khóa BM25 và tìm kiếm ngữ nghĩa Dense Vector theo công thức 1/(k + rank), giúp cải thiện Recall@5 rõ rệt với câu hỏi chứa mã môn học.'
        source_key              = 'uit_thesis_2025_hybrid_search.pdf'
        source_version          = 1
        source_filename         = 'KLTN_2025_TimKiemHocVu_RAG.pdf'
        source_page             = 22
        source_text             = 'Thuật toán Reciprocal Rank Fusion hợp nhất danh sách kết quả của BM25 và vector embedding qua trọng số nghịch đảo thứ hạng 1/(k + rank), đặc biệt hiệu quả khi truy vấn chứa mã học phần và ký hiệu quy chế.'
        reranker_score          = 0.86
        classifier_score        = 0.94
        citation_status         = 'Uncited'
        explanation             = 'Độ tương đồng ngữ nghĩa và cấu trúc câu rất cao nhưng đoạn văn tại mục 3.2 không ghi trích dẫn nguồn gốc hay tài liệu [15].'
        potential_misattribution = $false
        review_required         = $true
    },
    [ordered]@{
        passage_group_id        = 'pg-rag-02'
        submission_chunk_no     = 37
        submission_page         = 28
        submission_start        = 40
        submission_end          = 410
        submission_text         = 'Bộ chỉ số RAGAS đo lường ba khía cạnh độc lập gồm Faithfulness (độ trung thực theo ngữ cảnh truy xuất), Answer Relevance và Context Precision nhằm hạn chế hiện tượng suy diễn sai quy chế đào tạo.'
        source_key              = 'corpus_ragas_eval_2024.pdf'
        source_version          = 2
        source_filename         = 'TapChiCNTT_2024_DanhGiaHeThongRAG.pdf'
        source_page             = 6
        source_text             = 'Khung đánh giá RAGAS sử dụng bộ ba chỉ số Faithfulness, Answer Relevance và Context Precision để kiểm soát mức độ bám sát tài liệu gốc của mô hình sinh.'
        reranker_score          = 0.79
        classifier_score        = 0.88
        citation_status         = 'Uncited'
        explanation             = 'Thiếu trích dẫn bài báo gốc RAGAS (Es et al.) hoặc bài tổng quan tiếng Việt tại phần đánh giá.'
        potential_misattribution = $false
        review_required         = $true
    },
    [ordered]@{
        passage_group_id        = 'pg-rag-03'
        submission_chunk_no     = 45
        submission_page         = 34
        submission_start        = 300
        submission_end          = 640
        submission_text         = 'Kỹ thuật phân đoạn theo ranh giới điều khoản (clause-aware chunking) giữ nguyên toàn vẹn từng Điều và Khoản trong sổ tay sinh viên, tránh cắt rời điều kiện tiên quyết khỏi nội dung chính.'
        source_key              = 'uit_thesis_2025_hybrid_search.pdf'
        source_version          = 1
        source_filename         = 'KLTN_2025_TimKiemHocVu_RAG.pdf'
        source_page             = 16
        source_text             = 'Phân tách văn bản quy chế theo ranh giới Điều - Khoản đảm bảo các ngoại lệ và điều kiện tiên quyết không bị tách khỏi đoạn chứa tiêu đề điều mục.'
        reranker_score          = 0.74
        classifier_score        = 0.83
        citation_status         = 'Uncited'
        explanation             = 'Chưa có chú thích tham chiếu nguồn ý tưởng thiết kế chunking quy chế.'
        potential_misattribution = $false
        review_required         = $true
    },
    [ordered]@{
        passage_group_id        = 'pg-rag-04'
        submission_chunk_no     = 11
        submission_page         = 10
        submission_start        = 90
        submission_end          = 340
        submission_text         = 'Mô hình Cross-Encoder BGE-Reranker-v2-m3 chấm điểm lại top-20 đoạn ứng viên để chọn 5 đoạn có độ liên quan ngữ cảnh cao nhất đưa vào prompt.'
        source_key              = 'baai_bge_report_2024.pdf'
        source_version          = 1
        source_filename         = 'BGE_M3_Technical_Report.pdf'
        source_page             = 7
        source_text             = 'BGE-Reranker-v2-m3 supports multilingual fine-grained relevance scoring across top-k retrieved passages.'
        reranker_score          = 0.59
        classifier_score        = 0.91
        citation_status         = 'Cited'
        explanation             = 'Đã trích dẫn đúng tài liệu kỹ thuật BAAI [8].'
        potential_misattribution = $false
        review_required         = $false
    }
)

# 4) AWQ Quantization (b2222222-...-0403 v1) - 46.8% high similarity, 1 cited + 3 uncited + 2 suspected misattribution
$awqJson = New-SchemaV3Json -Coverage 0.468 -MaxScore 0.94 -ScannedChunks 118 -ScannedChars 74200 -MatchedChars 34725 -Cited 1 -Uncited 3 -Suspected 2 -Findings @(
    [ordered]@{
        passage_group_id        = 'pg-awq-01'
        submission_chunk_no     = 19
        submission_page         = 17
        submission_start        = 110
        submission_end          = 520
        submission_text         = 'Activation-aware Weight Quantization (AWQ) bảo toàn 1% các kênh trọng số nổi bật (salient weights) được xác định dựa trên phân bố kích hoạt đầu vào thay vì phân bố giá trị trọng số thuần túy, qua đó giảm mạnh sai số lượng tử hóa ở mức 4-bit.'
        source_key              = 'lin2024_awq_mlsys.pdf'
        source_version          = 1
        source_filename         = 'KLTN_2025_LuongTuHoa_SLM_Edge.pdf'
        source_page             = 14
        source_text             = 'Thuật toán AWQ giữ nguyên độ chính xác cao cho khoảng 1% kênh trọng số trọng yếu dựa theo biên độ kích hoạt (activation distribution) thay vì chỉ xét biên độ trọng số, giúp giảm suy giảm perplexity ở INT4.'
        reranker_score          = 0.94
        classifier_score        = 0.97
        citation_status         = 'SuspectedMisattribution'
        explanation             = 'Đoạn văn diễn giải gần như nguyên văn luận văn KLTN_2025_LuongTuHoa_SLM_Edge nhưng lại gắn nhãn trích dẫn sang tài liệu khác về GPTQ [6].'
        potential_misattribution = $true
        review_required         = $true
    },
    [ordered]@{
        passage_group_id        = 'pg-awq-02'
        submission_chunk_no     = 26
        submission_page         = 23
        submission_start        = 60
        submission_end          = 480
        submission_text         = 'Bằng cách nhân hệ số tỷ lệ s cho các kênh trọng số quan trọng trước khi làm tròn INT4 và chia nghịch đảo trong ma trận kích hoạt, phương pháp tránh được chi phí lưu trữ định dạng hỗn hợp phần cứng.'
        source_key              = 'lin2024_awq_mlsys.pdf'
        source_version          = 1
        source_filename         = 'KLTN_2025_LuongTuHoa_SLM_Edge.pdf'
        source_page             = 16
        source_text             = 'Việc áp dụng hệ số scale theo từng kênh trước lượng tử hóa rồi bù trừ vào ma trận kích hoạt cho phép duy trì tính toán thuần INT4 mà không cần hỗ trợ mixed-precision trên nhân CUDA.'
        reranker_score          = 0.91
        classifier_score        = 0.95
        citation_status         = 'Uncited'
        explanation             = 'Nội dung mô tả cơ chế toán học trùng khớp cao (>0,90) và không có trích dẫn nguồn.'
        potential_misattribution = $false
        review_required         = $true
    },
    [ordered]@{
        passage_group_id        = 'pg-awq-03'
        submission_chunk_no     = 44
        submission_page         = 36
        submission_start        = 200
        submission_end          = 610
        submission_text         = 'Trên GPU RTX 3060 12GB VRAM, nhân giải mã GEMV tối ưu cho batch size = 1 giúp tăng tốc độ sinh token của Qwen2.5-7B-INT4 lên 2,8 lần so với FP16 trong khi mức độ perplexity chỉ tăng 0,18.'
        source_key              = 'uit_lab_benchmark_2025.pdf'
        source_version          = 1
        source_filename         = 'BaoCao_LabAI_2025_SLM_Inference.pdf'
        source_page             = 9
        source_text             = 'Thử nghiệm trên RTX 3060 12GB cho thấy kernel GEMV INT4 đạt tốc độ tăng 2,75x ở batch size 1 so với FP16 với chênh lệch perplexity 0,19 trên tập WikiText-2.'
        reranker_score          = 0.88
        classifier_score        = 0.90
        citation_status         = 'SuspectedMisattribution'
        explanation             = 'Số liệu thực nghiệm trình bày như kết quả tự đo nhưng trùng cấu trúc mô tả và phần cứng với báo cáo Lab AI [2025]. Cần Hội đồng xác minh log thực nghiệm.'
        potential_misattribution = $true
        review_required         = $true
    },
    [ordered]@{
        passage_group_id        = 'pg-awq-04'
        submission_chunk_no     = 52
        submission_page         = 41
        submission_start        = 150
        submission_end          = 470
        submission_text         = 'Cơ chế PagedAttention trong vLLM phân bổ KV Cache theo từng khối bộ nhớ không liên tục giúp loại bỏ phân mảnh nội bộ và phục vụ nhiều phiên truy vấn đồng thời.'
        source_key              = 'kwon2023_vllm.pdf'
        source_version          = 1
        source_filename         = 'KLTN_2025_LuongTuHoa_SLM_Edge.pdf'
        source_page             = 29
        source_text             = 'PagedAttention chia KV Cache thành các block kích thước cố định, giảm lãng phí bộ nhớ GPU xuống dưới 4% khi xử lý nhiều request song song.'
        reranker_score          = 0.82
        classifier_score        = 0.85
        citation_status         = 'Uncited'
        explanation             = 'Thiếu trích dẫn bài báo Kwon et al. (SOSP 2023) về PagedAttention.'
        potential_misattribution = $false
        review_required         = $true
    },
    [ordered]@{
        passage_group_id        = 'pg-awq-05'
        submission_chunk_no     = 61
        submission_page         = 47
        submission_start        = 90
        submission_end          = 350
        submission_text         = 'Bộ tập chuẩn MMLU-Vi và VSP dùng để đánh giá độ suy giảm suy luận tiếng Việt sau lượng tử hóa trên miền câu hỏi trắc nghiệm chuyên ngành.'
        source_key              = 'uit_eval_vi_2025.pdf'
        source_version          = 1
        source_filename         = 'TapChi_KhoaHocMayTinh_2025_Benchmark_LLM.pdf'
        source_page             = 12
        source_text             = 'Hai bộ đánh giá MMLU-Vi và VSP phản ánh tương đối chính xác khả năng giữ tri thức tiếng Việt khi nén mô hình xuống 4-bit.'
        reranker_score          = 0.77
        classifier_score        = 0.80
        citation_status         = 'Uncited'
        explanation             = 'Chưa dẫn nguồn bộ benchmark và tài liệu đánh giá liên quan.'
        potential_misattribution = $false
        review_required         = $true
    },
    [ordered]@{
        passage_group_id        = 'pg-awq-06'
        submission_chunk_no     = 9
        submission_page         = 8
        submission_start        = 40
        submission_end          = 290
        submission_text         = 'Lượng tử hóa đồng nhất bất đối xứng ánh xạ khoảng giá trị thực [min, max] về tập số nguyên b-bit thông qua hệ số scale và điểm zero-point.'
        source_key              = 'gholami2021_survey.pdf'
        source_version          = 1
        source_filename         = 'Survey_Quantization_NeuralNetworks.pdf'
        source_page             = 5
        source_text             = 'Asymmetric uniform quantization maps real tensors to b-bit integers using scale factor S and zero-point Z.'
        reranker_score          = 0.63
        classifier_score        = 0.93
        citation_status         = 'Cited'
        explanation             = 'Có trích dẫn đúng bài khảo sát của Gholami et al. [2].'
        potential_misattribution = $false
        review_required         = $false
    }
)

# 5) PostgreSQL Distributed Query (33527fd8-... v1) - 14.2% moderate, 2 cited + 1 uncited
$pgQueryJson = New-SchemaV3Json -Coverage 0.142 -MaxScore 0.78 -ScannedChunks 86 -ScannedChars 51200 -MatchedChars 7270 -Cited 2 -Uncited 1 -Suspected 0 -Findings @(
    [ordered]@{
        passage_group_id        = 'pg-pg-01'
        submission_chunk_no     = 17
        submission_page         = 15
        submission_start        = 130
        submission_end          = 460
        submission_text         = 'Chiến lược phân mảnh theo khóa băm (hash partitioning) trên khóa ngoại tenant_id giúp Citus đẩy toàn bộ phép nối hai bảng lớn xuống thực thi cục bộ trên từng worker node mà không phát sinh truyền dữ liệu qua mạng.'
        source_key              = 'citus_arch_2024.pdf'
        source_version          = 1
        source_filename         = 'LuanVan_2024_PhanTanDuLieu_Citus.pdf'
        source_page             = 21
        source_text             = 'Khi hai bảng cùng phân mảnh theo cùng cột khóa băm (co-located tables), bộ lập kế hoạch Citus thực thi phép JOIN cục bộ trên từng shard mà không cần shuffle dữ liệu giữa các node.'
        reranker_score          = 0.78
        classifier_score        = 0.87
        citation_status         = 'Uncited'
        explanation             = 'Thiếu trích dẫn tài liệu Citus co-located join tại chương 2.'
        potential_misattribution = $false
        review_required         = $true
    },
    [ordered]@{
        passage_group_id        = 'pg-pg-02'
        submission_chunk_no     = 28
        submission_page         = 22
        submission_start        = 95
        submission_end          = 380
        submission_text         = 'Chỉ mục GIN kết hợp toán tử JSONB hỗ trợ truy vấn bán cấu trúc nhanh hơn đáng kể so với quét tuần tự B-Tree truyền thống.'
        source_key              = 'pg17_docs_index.pdf'
        source_version          = 1
        source_filename         = 'PostgreSQL_Indexing_Internals.pdf'
        source_page             = 44
        source_text             = 'Generalized Inverted Indexes (GIN) accelerate containment queries over composite JSONB columns.'
        reranker_score          = 0.60
        classifier_score        = 0.91
        citation_status         = 'Cited'
        explanation             = 'Đã trích dẫn tài liệu PostgreSQL Internals [5].'
        potential_misattribution = $false
        review_required         = $false
    }
)

$sqlBuilder = [System.Text.StringBuilder]::new()
[void]$sqlBuilder.AppendLine('BEGIN;')

$seedTargets = @(
    @{
        ScanId     = '99999999-9999-9999-9999-000000000831'
        ReportId   = '99999999-9999-9999-9999-000000000403'
        Status     = 'Completed'
        Coverage   = '0.086'
        MaxScore   = '0.64'
        Chunks     = '112'
        Chars      = '68400'
        Matched    = '5882'
        Cited      = '4'
        Uncited    = '0'
        Suspected  = '0'
        RawJson    = $secGuardJson
        ErrCode    = $null
        ErrMsg     = $null
        Completed  = "NOW() - INTERVAL '2 days'"
    },
    @{
        ScanId     = '88888888-8888-8888-8888-000000000101'
        ReportId   = 'b2222222-2222-2222-2222-000000000401'
        Status     = 'Completed'
        Coverage   = '0.084'
        MaxScore   = '0.61'
        Chunks     = '98'
        Chars      = '62100'
        Matched    = '5216'
        Cited      = '2'
        Uncited    = '0'
        Suspected  = '0'
        RawJson    = $ebpfJson
        ErrCode    = $null
        ErrMsg     = $null
        Completed  = "NOW() - INTERVAL '30 hours'"
    },
    @{
        ScanId     = '88888888-8888-8888-8888-000000000102'
        ReportId   = 'a1111111-1111-1111-1111-000000000403'
        Status     = 'Completed'
        Coverage   = '0.246'
        MaxScore   = '0.86'
        Chunks     = '104'
        Chars      = '64500'
        Matched    = '15867'
        Cited      = '2'
        Uncited    = '3'
        Suspected  = '0'
        RawJson    = $hybridRagJson
        ErrCode    = $null
        ErrMsg     = $null
        Completed  = "NOW() - INTERVAL '18 hours'"
    },
    @{
        ScanId     = '88888888-8888-8888-8888-000000000103'
        ReportId   = 'b2222222-2222-2222-2222-000000000403'
        Status     = 'Completed'
        Coverage   = '0.468'
        MaxScore   = '0.94'
        Chunks     = '118'
        Chars      = '74200'
        Matched    = '34725'
        Cited      = '1'
        Uncited    = '3'
        Suspected  = '2'
        RawJson    = $awqJson
        ErrCode    = $null
        ErrMsg     = $null
        Completed  = "NOW() - INTERVAL '12 hours'"
    },
    @{
        ScanId     = '88888888-8888-8888-8888-000000000104'
        ReportId   = 'b2222222-2222-2222-2222-000000000404'
        Status     = 'Processing'
        Coverage   = 'NULL'
        MaxScore   = 'NULL'
        Chunks     = 'NULL'
        Chars      = 'NULL'
        Matched    = 'NULL'
        Cited      = 'NULL'
        Uncited    = 'NULL'
        Suspected  = 'NULL'
        RawJson    = $null
        ErrCode    = $null
        ErrMsg     = $null
        Completed  = 'NULL'
    },
    @{
        ScanId     = '88888888-8888-8888-8888-000000000105'
        ReportId   = 'ff378a4f-3e31-4ea2-ad14-4273cc5fbf1c'
        Status     = 'WaitingForCompute'
        Coverage   = 'NULL'
        MaxScore   = 'NULL'
        Chunks     = 'NULL'
        Chars      = 'NULL'
        Matched    = 'NULL'
        Cited      = 'NULL'
        Uncited    = 'NULL'
        Suspected  = 'NULL'
        RawJson    = $null
        ErrCode    = $null
        ErrMsg     = $null
        Completed  = 'NULL'
    },
    @{
        ScanId     = '88888888-8888-8888-8888-000000000106'
        ReportId   = 'ccdf69dc-843d-415d-a833-812577688188'
        Status     = 'ComputeUnavailable'
        Coverage   = 'NULL'
        MaxScore   = 'NULL'
        Chunks     = 'NULL'
        Chars      = 'NULL'
        Matched    = 'NULL'
        Cited      = 'NULL'
        Uncited    = 'NULL'
        Suspected  = 'NULL'
        RawJson    = $null
        ErrCode    = 'GPU_WORKER_TIMEOUT'
        ErrMsg     = 'Dịch vụ GPU đối chiếu ngữ nghĩa tạm hết hạn mức xử lý. GVHD hoặc Quản trị viên có thể yêu cầu quét lại.'
        Completed  = 'NULL'
    },
    @{
        ScanId     = '88888888-8888-8888-8888-000000000107'
        ReportId   = '33527fd8-127d-4aae-ad6b-3d95f0028b0f'
        Status     = 'Completed'
        Coverage   = '0.142'
        MaxScore   = '0.78'
        Chunks     = '86'
        Chars      = '51200'
        Matched    = '7270'
        Cited      = '2'
        Uncited    = '1'
        Suspected  = '0'
        RawJson    = $pgQueryJson
        ErrCode    = $null
        ErrMsg     = $null
        Completed  = "NOW() - INTERVAL '8 hours'"
    }
)

foreach ($item in $seedTargets) {
    $rawSql = if ($null -eq $item.RawJson) { 'NULL' } else { (ConvertTo-SqlLiteral $item.RawJson) + '::jsonb' }
    $errCodeSql = ConvertTo-SqlLiteral $item.ErrCode
    $errMsgSql = ConvertTo-SqlLiteral $item.ErrMsg

    [void]$sqlBuilder.AppendLine(@"
INSERT INTO thesis.final_report_plagiarism_scan (
    id, final_report_id, submission_file_id, version_no, status,
    similarity_coverage, max_reranker_score, scanned_chunks, scanned_characters,
    matched_characters, cited_findings_count, uncited_findings_count, suspected_findings_count,
    raw_report_json, error_code, error_message, attempt_count,
    created_at, last_polled_at, completed_at
)
SELECT
    '$($item.ScanId)'::uuid,
    fr.id,
    sf.id,
    fr.current_version_no,
    '$($item.Status)',
    $($item.Coverage),
    $($item.MaxScore),
    $($item.Chunks),
    $($item.Chars),
    $($item.Matched),
    $($item.Cited),
    $($item.Uncited),
    $($item.Suspected),
    $rawSql,
    $errCodeSql,
    $errMsgSql,
    1,
    NOW() - INTERVAL '2 days',
    NOW() - INTERVAL '1 hour',
    $($item.Completed)
FROM thesis.final_report fr
JOIN thesis.submission_file sf
    ON sf.entity_type = 'FinalReport'
   AND sf.entity_id = fr.id
   AND sf.version_no = fr.current_version_no
WHERE fr.id = '$($item.ReportId)'::uuid
ON CONFLICT (final_report_id, version_no) DO UPDATE SET
    status = EXCLUDED.status,
    similarity_coverage = EXCLUDED.similarity_coverage,
    max_reranker_score = EXCLUDED.max_reranker_score,
    scanned_chunks = EXCLUDED.scanned_chunks,
    scanned_characters = EXCLUDED.scanned_characters,
    matched_characters = EXCLUDED.matched_characters,
    cited_findings_count = EXCLUDED.cited_findings_count,
    uncited_findings_count = EXCLUDED.uncited_findings_count,
    suspected_findings_count = EXCLUDED.suspected_findings_count,
    raw_report_json = EXCLUDED.raw_report_json,
    error_code = EXCLUDED.error_code,
    error_message = EXCLUDED.error_message,
    last_polled_at = EXCLUDED.last_polled_at,
    completed_at = EXCLUDED.completed_at;
"@)
}

[void]$sqlBuilder.AppendLine('COMMIT;')

Invoke-PsqlFile $sqlBuilder.ToString() | Out-Null

Write-Host 'Seeded council similarity report scans successfully:' -ForegroundColor Green
Write-Host '  - b2222222-...-0401 (eBPF): Completed 8.4% (2 cited)'
Write-Host '  - 99999999-...-0403 (SecGuard v2): Completed 8.6% (rich v3 findings)'
Write-Host '  - 33527fd8-...      ([TEST] PostgreSQL): Completed 14.2% (2 cited, 1 uncited)'
Write-Host '  - a1111111-...-0403 (Hybrid RAG): Completed 24.6% (2 cited, 3 uncited)'
Write-Host '  - b2222222-...-0403 (Quantization AWQ): Completed 46.8% (1 cited, 3 uncited, 2 suspected)'
Write-Host '  - b2222222-...-0404 (Android Malware GNN): Processing'
Write-Host '  - ff378a4f-...      ([TEST] Cheating Detection): WaitingForCompute'
Write-Host '  - ccdf69dc-...      ([TEST] Internship): ComputeUnavailable'
Write-Host '  - b2222222-...-0402 (Federated Medical): Unscanned (no scan row)'
