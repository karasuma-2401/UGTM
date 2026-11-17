[CmdletBinding()]
param(
    [ValidateSet('All', 'BaseUsers')]
    [string]$Scope = 'All',
    [string]$Password = 'UgtmDemo@2026',
    [switch]$Reset
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$envFile = Join-Path $repoRoot '.env'

function Get-DatabaseConnectionString {
    $fromEnv = [Environment]::GetEnvironmentVariable('DATABASE_CONNECTION_STRING', 'Process')
    if (-not [string]::IsNullOrWhiteSpace($fromEnv)) {
        return $fromEnv.Trim().Trim('"').Trim("'")
    }

    if (-not (Test-Path -LiteralPath $envFile)) {
        throw "Missing .env file at $envFile and DATABASE_CONNECTION_STRING is not set."
    }

    foreach ($line in Get-Content -LiteralPath $envFile) {
        if ($line -match '^\s*#' -or $line -notmatch '^\s*DATABASE_CONNECTION_STRING\s*=(.*)$') {
            continue
        }
        $value = $Matches[1].Trim().Trim('"').Trim("'")
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    throw 'DATABASE_CONNECTION_STRING is not configured in environment or .env.'
}

function Set-PgEnvFromConnectionString([string]$ConnectionString) {
    $map = @{}
    foreach ($segment in $ConnectionString.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $kv = $segment.Split('=', 2)
        if ($kv.Count -eq 2) {
            $map[$kv[0].Trim().ToLowerInvariant()] = $kv[1].Trim()
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

function Clear-PgEnv {
    Remove-Item Env:PGHOST, Env:PGPORT, Env:PGDATABASE, Env:PGUSER, Env:PGPASSWORD, Env:PGSSLMODE, Env:PGCLIENTENCODING -ErrorAction SilentlyContinue
}

function Get-PsqlPath {
    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = Get-ChildItem -Path 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending
    if ($candidates) { return $candidates[0].FullName }

    throw 'psql.exe not found on PATH or under C:\Program Files\PostgreSQL\*\bin.'
}

function Invoke-PsqlFile([string]$PsqlPath, [string]$SqlText) {
    $tmp = [IO.Path]::Combine([IO.Path]::GetTempPath(), "ugtm_seed_demo_$([Guid]::NewGuid().ToString('N')).sql")
    $prevEncoding = [Console]::OutputEncoding
    try {
        [IO.File]::WriteAllText($tmp, $SqlText, [System.Text.UTF8Encoding]::new($false))
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $output = & $PsqlPath -X -v ON_ERROR_STOP=1 -f $tmp 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "psql failed (exit $LASTEXITCODE):`n$($output -join "`n")"
        }
        return $output
    }
    finally {
        [Console]::OutputEncoding = $prevEncoding
        if (Test-Path -LiteralPath $tmp) {
            Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
        }
    }
}

function Invoke-PsqlScalar([string]$PsqlPath, [string]$Query) {
    $output = & $PsqlPath -X -t -A -v ON_ERROR_STOP=1 -c $Query 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "psql query failed (exit $LASTEXITCODE):`n$($output -join "`n")"
    }
    return ($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function ConvertTo-SqlLiteral([string]$Value) {
    if ($null -eq $Value) { return 'NULL' }
    return "'" + $Value.Replace("'", "''") + "'"
}

function Get-AspNetIdentityV3Hash([string]$PlainPassword) {
    $salt = New-Object byte[] 16
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($salt)
    }
    finally {
        $rng.Dispose()
    }

    $iterations = 100000
    $derive = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
        $PlainPassword,
        $salt,
        $iterations,
        [System.Security.Cryptography.HashAlgorithmName]::SHA512
    )
    try {
        $subkey = $derive.GetBytes(32)
    }
    finally {
        $derive.Dispose()
    }

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
    [Buffer]::BlockCopy($prfBytes, 0, $output, 1, 4)
    [Buffer]::BlockCopy($iterBytes, 0, $output, 5, 4)
    [Buffer]::BlockCopy($saltLenBytes, 0, $output, 9, 4)
    [Buffer]::BlockCopy($salt, 0, $output, 13, $salt.Length)
    [Buffer]::BlockCopy($subkey, 0, $output, 13 + $salt.Length, $subkey.Length)
    return [Convert]::ToBase64String($output)
}

function Get-Sha256Hex([string]$Text) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Text))
        return (($bytes | ForEach-Object { $_.ToString('x2') }) -join '')
    }
    finally {
        $sha.Dispose()
    }
}

function ConvertTo-StjJsonString([string]$Text) {
    # Matches System.Text.Json JsonSerializer.Serialize default JavaScriptEncoder escaping
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    foreach ($ch in $Text.ToCharArray()) {
        $code = [int]$ch
        switch ($code) {
            0x22 { [void]$sb.Append('\"') }
            0x5C { [void]$sb.Append('\\') }
            0x08 { [void]$sb.Append('\b') }
            0x09 { [void]$sb.Append('\t') }
            0x0A { [void]$sb.Append('\n') }
            0x0C { [void]$sb.Append('\f') }
            0x0D { [void]$sb.Append('\r') }
            default {
                if ($code -lt 0x20 -or $code -ge 0x7F -or $code -in 0x26, 0x27, 0x2B, 0x3C, 0x3E) {
                    [void]$sb.AppendFormat('\u{0:X4}', $code)
                }
                else {
                    [void]$sb.Append($ch)
                }
            }
        }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}

function Get-ArchivalManifestSha256(
    [string]$AbstractVi,
    [string]$AbstractEn,
    [string]$KeywordsVi,
    [string]$KeywordsEn,
    [array]$Files
) {
    $fileArrays = $Files | Sort-Object Role | ForEach-Object {
        "[{0},{1},{2},{3},{4}]" -f (ConvertTo-StjJsonString $_.Role), (ConvertTo-StjJsonString $_.FileId.ToString('N')), $_.FileSize, (ConvertTo-StjJsonString $_.ContentType), (ConvertTo-StjJsonString $_.ChecksumSha256)
    }
    $payload = "[{0},{1},{2},{3},[{4}]]" -f (ConvertTo-StjJsonString $AbstractVi), (ConvertTo-StjJsonString $AbstractEn), (ConvertTo-StjJsonString $KeywordsVi), (ConvertTo-StjJsonString $KeywordsEn), ($fileArrays -join ',')
    return (Get-Sha256Hex $payload)
}

function New-DefenseSnapshotJson(
    [Guid]$TopicId,
    [Guid]$BatchId,
    [Guid]$AdvisorId,
    [Guid]$GroupId,
    [Guid]$CouncilId,
    [Guid]$AssignmentId,
    [Guid]$MinuteId,
    [uint32]$MinuteVersion,
    [Guid]$LockedBy,
    [string]$LockedAtIsoUtc
) {
    $canonical = [ordered]@{
        councilId    = $CouncilId.ToString('D')
        assignmentId = $AssignmentId.ToString('D')
        Id           = $MinuteId.ToString('D')
        Version      = $MinuteVersion
        LockedBy     = $LockedBy.ToString('D')
        LockedAt     = $LockedAtIsoUtc
    } | ConvertTo-Json -Compress
    $proofSha256 = Get-Sha256Hex $canonical

    $snapshot = [ordered]@{
        TopicId           = $TopicId.ToString('D')
        BatchId           = $BatchId.ToString('D')
        AssignedAdvisorId = $AdvisorId.ToString('D')
        Defense           = [ordered]@{
            Outcome       = 0
            GroupId       = $GroupId.ToString('D')
            BatchId       = $BatchId.ToString('D')
            CouncilId     = $CouncilId.ToString('D')
            AssignmentId  = $AssignmentId.ToString('D')
            MinuteId      = $MinuteId.ToString('D')
            MinuteVersion = $MinuteVersion
            LockedBy      = $LockedBy.ToString('D')
            LockedAtUtc   = $LockedAtIsoUtc
            ProofSha256   = $proofSha256
        }
    }
    return ($snapshot | ConvertTo-Json -Depth 5 -Compress)
}

$connectionString = Get-DatabaseConnectionString
$psql = Get-PsqlPath

try {
    Set-PgEnvFromConnectionString -ConnectionString $connectionString

    if ($Reset) {
        $resetSql = @'
BEGIN;

ALTER TABLE thesis.final_submission_review DISABLE TRIGGER archival_review_immutable;
ALTER TABLE thesis.final_submission_package_file DISABLE TRIGGER archival_link_immutable;
ALTER TABLE thesis.final_submission_package DISABLE TRIGGER archival_package_immutable;
ALTER TABLE thesis.submission_file DISABLE TRIGGER archival_file_immutable;
ALTER TABLE thesis.final_submission DISABLE TRIGGER archival_freeze;
ALTER TABLE thesis.final_submission DISABLE TRIGGER archival_package_complete;
ALTER TABLE thesis.final_submission_package DISABLE TRIGGER archival_package_complete;
ALTER TABLE thesis.final_submission_package_file DISABLE TRIGGER archival_package_complete;
ALTER TABLE thesis.final_submission_review DISABLE TRIGGER archival_package_complete;

DELETE FROM thesis.final_submission_idempotency
WHERE response_payload::text LIKE '%d0000000-0000-0000-0000-%'
   OR id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.final_submission_upload_attempt
WHERE group_id::text LIKE 'd0000000-0000-0000-0000-%'
   OR id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.final_submission_review
WHERE submission_id IN (SELECT id FROM thesis.final_submission WHERE id::text LIKE 'd0000000-0000-0000-0000-%')
   OR id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.final_submission_package_file
WHERE submission_id IN (SELECT id FROM thesis.final_submission WHERE id::text LIKE 'd0000000-0000-0000-0000-%')
   OR package_id::text LIKE 'd0000000-0000-0000-0000-%';
UPDATE thesis.final_submission
SET current_package_id = NULL,
    is_legacy = true
WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.final_submission_package
WHERE submission_id IN (SELECT id FROM thesis.final_submission WHERE id::text LIKE 'd0000000-0000-0000-0000-%')
   OR id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.final_submission
WHERE id::text LIKE 'd0000000-0000-0000-0000-%';

SET CONSTRAINTS ALL IMMEDIATE;

ALTER TABLE thesis.final_submission_review ENABLE TRIGGER archival_review_immutable;
ALTER TABLE thesis.final_submission_package_file ENABLE TRIGGER archival_link_immutable;
ALTER TABLE thesis.final_submission_package ENABLE TRIGGER archival_package_immutable;
ALTER TABLE thesis.submission_file ENABLE TRIGGER archival_file_immutable;
ALTER TABLE thesis.final_submission ENABLE TRIGGER archival_freeze;
ALTER TABLE thesis.final_submission ENABLE TRIGGER archival_package_complete;
ALTER TABLE thesis.final_submission_package ENABLE TRIGGER archival_package_complete;
ALTER TABLE thesis.final_submission_package_file ENABLE TRIGGER archival_package_complete;
ALTER TABLE thesis.final_submission_review ENABLE TRIGGER archival_package_complete;

DELETE FROM councils.score_summary_component WHERE summary_id IN (
    SELECT id FROM councils.score_summary WHERE group_id::text LIKE 'd0000000-0000-0000-0000-%'
);
DELETE FROM councils.score_summary_revision WHERE group_id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM councils.score_summary WHERE group_id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM councils.evaluation_detail
WHERE evaluation_id::text LIKE 'd0000000-0000-0000-0000-%'
   OR criteria_id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM councils.council_minute
WHERE id::text LIKE 'd0000000-0000-0000-0000-%'
   OR council_assignment_id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM councils.council_score
WHERE id::text LIKE 'd0000000-0000-0000-0000-%'
   OR council_assignment_id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM councils.council_assignment WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM councils.council_member WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM councils.council WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM councils.reviewer_evaluation
WHERE id::text LIKE 'd0000000-0000-0000-0000-%'
   OR assignment_id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM councils.reviewer_assignment WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM councils.advisor_evaluation
WHERE id::text LIKE 'd0000000-0000-0000-0000-%'
   OR group_id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM councils.grading_criteria WHERE id::text LIKE 'd0000000-0000-0000-0000-%';

DELETE FROM thesis.final_report_plagiarism_scan WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.submission_review WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.submission_file WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.final_report WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.midterm_report WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.progress_report_version WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.progress_report WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.progress_milestone WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.syllabus WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.rename_request WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.student_group_member WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM thesis.student_group WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM topics.topic WHERE id::text LIKE 'd0000000-0000-0000-0000-%';
DELETE FROM academic.stage WHERE batch_id = 'd0000000-0000-0000-0000-000000000001'::uuid;
DELETE FROM academic.kltn_batch WHERE id = 'd0000000-0000-0000-0000-000000000001'::uuid;

DELETE FROM identity.user_role
WHERE user_id::text LIKE 'd0000000-0000-0000-0000-%'
   OR user_id::text LIKE 'e0000000-0000-0000-0000-0000000000%';
DELETE FROM identity."user"
WHERE id::text LIKE 'd0000000-0000-0000-0000-%'
   OR id::text LIKE 'e0000000-0000-0000-0000-0000000000%';

COMMIT;
'@
        $null = Invoke-PsqlFile -PsqlPath $psql -SqlText $resetSql
        Write-Host 'Reset completed: removed UGTM demo records (d0000000-* and e0000000-0000-0000-0000-0000000000*).' -ForegroundColor Yellow
        return
    }

    $passwordHash = Get-AspNetIdentityV3Hash -PlainPassword $Password

    # 1. Seed the 6 canonical base role accounts
    $baseUsersSql = @'
BEGIN;

DO $$
DECLARE
    v_pwd_hash text := '__PWD_HASH__';
    v_item record;
    v_user_id uuid;
    v_role_id integer;
BEGIN
    FOR v_item IN
        SELECT *
        FROM (VALUES
            ('11111111-1111-1111-1111-111111111111'::uuid, 'ADMIN001',   'System Administrator',      'admin@ugtm.edu.vn',      'Admin'),
            ('b2222222-2222-2222-2222-000000000099'::uuid, 'FACULTY01',  'PGS.TS. Nguyễn Minh Triết', 'truongkhoa@ugtm.edu.vn', 'Faculty'),
            ('d4444444-4444-4444-4444-000000000098'::uuid, 'PDT01',      'ThS. Lê Minh Tâm',          'pdt@ugtm.edu.vn',        'AcademicAffairs'),
            ('e0000000-0000-0000-0000-000000000004'::uuid, 'GV_BASE_01', 'TS. Phạm Minh Khang',       'lecturer@ugtm.edu.vn',   'Lecturer'),
            ('e0000000-0000-0000-0000-000000000005'::uuid, 'SV_BASE_01', 'Nguyễn Văn Sinh Viên',      'student@ugtm.edu.vn',    'Student'),
            ('e0000000-0000-0000-0000-000000000006'::uuid, 'HD_BASE_01', 'TS. Lê Quang Dũng',         'council@ugtm.edu.vn',    'CouncilMember')
        ) AS t(default_id, user_code, full_name, email, role_name)
    LOOP
        SELECT id INTO v_role_id
        FROM identity.role
        WHERE role_name = v_item.role_name
        LIMIT 1;

        IF v_role_id IS NULL THEN
            RAISE EXCEPTION 'Missing role % in identity.role. Run migrations first.', v_item.role_name;
        END IF;

        INSERT INTO identity."user" (
            id, user_code, full_name, email, normalized_email, password_hash,
            status, security_stamp, concurrency_stamp, access_failed_count,
            lockout_end_at, created_at, updated_at, activated_at, email_verified_at, password_changed_at
        )
        VALUES (
            v_item.default_id, v_item.user_code, v_item.full_name, v_item.email, UPPER(v_item.email),
            v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0,
            NULL, NOW(), NOW(), NOW(), NOW(), NOW()
        )
        ON CONFLICT (normalized_email) DO UPDATE
        SET full_name = EXCLUDED.full_name,
            password_hash = EXCLUDED.password_hash,
            status = 'Active',
            access_failed_count = 0,
            lockout_end_at = NULL,
            security_stamp = gen_random_uuid(),
            concurrency_stamp = gen_random_uuid(),
            activated_at = COALESCE(identity."user".activated_at, NOW()),
            email_verified_at = COALESCE(identity."user".email_verified_at, NOW()),
            password_changed_at = NOW(),
            updated_at = NOW()
        RETURNING id INTO v_user_id;

        DELETE FROM identity.user_role
        WHERE user_id = v_user_id AND role_id <> v_role_id;

        INSERT INTO identity.user_role (user_id, role_id, assigned_at)
        SELECT v_user_id, v_role_id, NOW()
        WHERE NOT EXISTS (
            SELECT 1 FROM identity.user_role
            WHERE user_id = v_user_id AND role_id = v_role_id
        );
    END LOOP;
END $$;

COMMIT;
'@
    $null = Invoke-PsqlFile -PsqlPath $psql -SqlText ($baseUsersSql.Replace('__PWD_HASH__', $passwordHash))

    if ($Scope -eq 'BaseUsers') {
        Write-Host "Seeded 6 base role users successfully (password: $Password)." -ForegroundColor Green
        Write-Host '  admin@ugtm.edu.vn / truongkhoa@ugtm.edu.vn / pdt@ugtm.edu.vn / lecturer@ugtm.edu.vn / student@ugtm.edu.vn / council@ugtm.edu.vn'
        return
    }

    # 2. Build sample v3 plagiarism report JSON (realistic warning case 24.6%)
    $similarityJson = [ordered]@{
        schema_version = 'v3'
        summary        = [ordered]@{
            similarity_coverage = 0.246
            max_reranker_score  = 0.86
            scanned_chunks      = 104
            scanned_characters  = 64500
            matched_characters  = 15867
            cited_findings      = 1
            uncited_findings    = 1
            suspected_findings  = 0
        }
        findings       = @(
            [ordered]@{
                passage_group_id         = 'pg-demo-01'
                submission_chunk_no      = 22
                submission_page          = 19
                submission_start         = 150
                submission_end           = 540
                submission_text          = 'Phương pháp Reciprocal Rank Fusion (RRF) kết hợp bảng xếp hạng BM25 và Dense Vector theo công thức 1/(k + rank) nhằm nâng cao độ phủ truy xuất câu hỏi chứa mã học phần.'
                source_key               = 'uit_thesis_2025_hybrid_search.pdf'
                source_version           = 1
                source_filename          = 'KLTN_2025_TimKiemHocVu_RAG.pdf'
                source_page              = 22
                source_text              = 'Thuật toán Reciprocal Rank Fusion hợp nhất danh sách kết quả của BM25 và vector embedding qua trọng số nghịch đảo thứ hạng 1/(k + rank).'
                reranker_score           = 0.86
                classifier_score         = 0.94
                citation_status          = 'Uncited'
                explanation              = 'Độ tương đồng ngữ nghĩa cao nhưng đoạn văn tại mục 3.2 chưa ghi trích dẫn nguồn gốc.'
                potential_misattribution = $false
                review_required          = $true
            },
            [ordered]@{
                passage_group_id         = 'pg-demo-02'
                submission_chunk_no      = 11
                submission_page          = 10
                submission_start         = 90
                submission_end           = 340
                submission_text          = 'Mô hình Cross-Encoder BGE-Reranker-v2-m3 chấm điểm lại top-20 đoạn ứng viên trước khi đưa vào prompt sinh câu trả lời.'
                source_key               = 'baai_bge_report_2024.pdf'
                source_version           = 1
                source_filename          = 'BGE_M3_Technical_Report.pdf'
                source_page              = 7
                source_text              = 'BGE-Reranker-v2-m3 supports multilingual fine-grained relevance scoring across top-k retrieved passages.'
                reranker_score           = 0.59
                classifier_score         = 0.91
                citation_status          = 'Cited'
                explanation              = 'Đã trích dẫn đúng tài liệu kỹ thuật BAAI.'
                potential_misattribution = $false
                review_required          = $false
            }
        )
        disclaimer     = 'Bằng chứng tương đồng phục vụ tham chiếu thẩm định chuyên môn cho Hội đồng và GVHD.'
    } | ConvertTo-Json -Depth 6 -Compress

    # 3. Seed fallback batch + stages + grading criteria + unified minimal demo workflow
    $demoWorkflowSql = @'
BEGIN;

DO $$
DECLARE
    v_pwd_hash text := '__PWD_HASH__';
    v_similarity_json jsonb := __SIMILARITY_JSON__::jsonb;

    v_admin_id    uuid;
    v_faculty_id  uuid;
    v_gv1_id      uuid; -- lecturer@ugtm.edu.vn (GV_BASE_01)
    v_council1_id uuid; -- council@ugtm.edu.vn  (HD_BASE_01)
    v_gv2_id      uuid := 'd0000000-0000-0000-0000-000000000011';
    v_gv3_id      uuid := 'd0000000-0000-0000-0000-000000000012';

    v_role_student  integer;
    v_role_lecturer integer;
    v_role_council  integer;

    v_batch_id        uuid;
    v_template_id     uuid;
    v_next_semester   integer;

    -- 6 demo students across 3 groups (d0000000-...)
    v_s1 uuid := 'd0000000-0000-0000-0000-000000000301';
    v_s2 uuid := 'd0000000-0000-0000-0000-000000000302';
    v_s3 uuid := 'd0000000-0000-0000-0000-000000000303';
    v_s4 uuid := 'd0000000-0000-0000-0000-000000000304';
    v_s5 uuid := 'd0000000-0000-0000-0000-000000000305';
    v_s6 uuid := 'd0000000-0000-0000-0000-000000000306';

    -- Topics (2 review states + 3 published topics for the 3 groups)
    v_t_sub  uuid := 'd0000000-0000-0000-0000-000000000101';
    v_t_rev  uuid := 'd0000000-0000-0000-0000-000000000102';
    v_t1     uuid := 'd0000000-0000-0000-0000-000000000103';
    v_t2     uuid := 'd0000000-0000-0000-0000-000000000104';
    v_t3     uuid := 'd0000000-0000-0000-0000-000000000105';

    -- Groups
    v_g1 uuid := 'd0000000-0000-0000-0000-000000000201'; -- InProgress (Milestones + Rename)
    v_g2 uuid := 'd0000000-0000-0000-0000-000000000202'; -- MidtermSubmitted + FinalReport + Similarity
    v_g3 uuid := 'd0000000-0000-0000-0000-000000000203'; -- Evaluated + Council Active + Locked Minute + Archival

    -- Final Reports & Submission Files
    v_fr2 uuid := 'd0000000-0000-0000-0000-000000000412';
    v_fr3 uuid := 'd0000000-0000-0000-0000-000000000413';
    v_sf_fr2 uuid := 'd0000000-0000-0000-0000-000000000422';
    v_sf_fr3 uuid := 'd0000000-0000-0000-0000-000000000423';
    v_sha_fr2 text := repeat('2', 64);
    v_sha_fr3 text := repeat('3', 64);

    -- Evaluations & Council
    v_adv2 uuid := 'd0000000-0000-0000-0000-000000000602';
    v_ra2  uuid := 'd0000000-0000-0000-0000-000000000612';
    v_re2  uuid := 'd0000000-0000-0000-0000-000000000622';

    v_adv3 uuid := 'd0000000-0000-0000-0000-000000000603';
    v_ra3  uuid := 'd0000000-0000-0000-0000-000000000613';
    v_re3  uuid := 'd0000000-0000-0000-0000-000000000623';

    v_council1 uuid := 'd0000000-0000-0000-0000-000000000701';
    v_cm1_ch   uuid := 'd0000000-0000-0000-0000-000000000711';
    v_cm1_sec  uuid := 'd0000000-0000-0000-0000-000000000712';
    v_cm1_mem  uuid := 'd0000000-0000-0000-0000-000000000713';
    v_ca3      uuid := 'd0000000-0000-0000-0000-000000000721';
    v_cs3_ch   uuid := 'd0000000-0000-0000-0000-000000000731';
    v_cs3_sec  uuid := 'd0000000-0000-0000-0000-000000000732';
    v_cs3_mem  uuid := 'd0000000-0000-0000-0000-000000000733';
    v_min3     uuid := 'd0000000-0000-0000-0000-000000000741';

    -- Criteria IDs for per-student evaluation details
    v_crit_ids uuid[];
    v_crit_scores_s5 numeric[] := ARRAY[1.50, 1.25, 1.00, 1.00, 0.75, 0.75, 0.75, 1.00, 0.50, 0.50];
    v_crit_scores_s6 numeric[] := ARRAY[1.25, 1.25, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.50, 0.50];
    v_idx integer;
BEGIN
    SELECT id INTO v_admin_id    FROM identity."user" WHERE normalized_email = 'ADMIN@UGTM.EDU.VN' LIMIT 1;
    SELECT id INTO v_faculty_id  FROM identity."user" WHERE normalized_email = 'TRUONGKHOA@UGTM.EDU.VN' LIMIT 1;
    SELECT id INTO v_gv1_id      FROM identity."user" WHERE normalized_email = 'LECTURER@UGTM.EDU.VN' LIMIT 1;
    SELECT id INTO v_council1_id FROM identity."user" WHERE normalized_email = 'COUNCIL@UGTM.EDU.VN' LIMIT 1;

    SELECT id INTO v_role_student  FROM identity.role WHERE role_name = 'Student' LIMIT 1;
    SELECT id INTO v_role_lecturer FROM identity.role WHERE role_name = 'Lecturer' LIMIT 1;
    SELECT id INTO v_role_council  FROM identity.role WHERE role_name = 'CouncilMember' LIMIT 1;

    -- Extra demo lecturers & 6 demo students
    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    (v_gv2_id, 'GV_DEMO_02', 'TS. Trần Minh Đức',       'gv2@ugtm.edu.vn',       'GV2@UGTM.EDU.VN',       v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_gv3_id, 'GV_DEMO_03', 'PGS.TS. Đặng Hoàng Minh', 'gv3@ugtm.edu.vn',       'GV3@UGTM.EDU.VN',       v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s1,     '22520101',   'Nguyễn Minh Khôi',        '22520101@gm.uit.edu.vn', '22520101@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s2,     '22520102',   'Trần Ngọc Thảo',          '22520102@gm.uit.edu.vn', '22520102@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s3,     '22520103',   'Lê Hoàng Phúc',           '22520103@gm.uit.edu.vn', '22520103@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s4,     '22520104',   'Phạm Gia Hân',            '22520104@gm.uit.edu.vn', '22520104@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s5,     '22520105',   'Võ Tấn Phát',             '22520105@gm.uit.edu.vn', '22520105@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_s6,     '22520106',   'Đinh Khánh Linh',         '22520106@gm.uit.edu.vn', '22520106@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO UPDATE SET
        password_hash = EXCLUDED.password_hash,
        status = 'Active',
        access_failed_count = 0,
        updated_at = NOW();

    INSERT INTO identity.user_role (user_id, role_id, assigned_at)
    SELECT u_id, r_id, NOW()
    FROM (VALUES
        (v_gv1_id, v_role_council),
        (v_gv2_id, v_role_lecturer),
        (v_gv2_id, v_role_council),
        (v_gv3_id, v_role_lecturer),
        (v_gv3_id, v_role_council),
        (v_s1,     v_role_student),
        (v_s2,     v_role_student),
        (v_s3,     v_role_student),
        (v_s4,     v_role_student),
        (v_s5,     v_role_student),
        (v_s6,     v_role_student)
    ) AS v(u_id, r_id)
    WHERE NOT EXISTS (
        SELECT 1 FROM identity.user_role ur WHERE ur.user_id = v.u_id AND ur.role_id = v.r_id
    );

    -- Fallback: ensure an Open batch + 7 stages exist
    SELECT id INTO v_batch_id
    FROM academic.kltn_batch
    WHERE status = 'Open'
    ORDER BY created_at DESC
    LIMIT 1;

    IF v_batch_id IS NULL THEN
        SELECT id INTO v_template_id
        FROM academic.schedule_template_version
        ORDER BY (id = '4e035a70-f7d5-49ef-b977-d818eff26896') DESC, created_at DESC
        LIMIT 1;

        IF v_template_id IS NULL THEN
            v_template_id := '4e035a70-f7d5-49ef-b977-d818eff26896'::uuid;
            INSERT INTO academic.schedule_template_version (
                id, template_code, version_no, name, time_zone_id, status, created_at, published_at
            ) VALUES (
                v_template_id, 'UIT_KLTN_STANDARD', 1, 'Quy trình KLTN chuẩn 21 tuần', 'Asia/Ho_Chi_Minh', 'Active', NOW(), NOW()
            )
            ON CONFLICT (id) DO NOTHING;
        END IF;

        SELECT COALESCE(MAX(semester), 0) + 1 INTO v_next_semester
        FROM academic.kltn_batch
        WHERE academic_year = '2026-2027';

        v_batch_id := 'd0000000-0000-0000-0000-000000000001'::uuid;
        INSERT INTO academic.kltn_batch (
            id, batch_name, academic_year, semester, start_date, end_date, status,
            created_at, created_by, schedule_template_version_id, time_zone_id, updated_at, updated_by
        ) VALUES (
            v_batch_id,
            'Đợt KLTN Demo 2026-2027',
            '2026-2027',
            v_next_semester,
            CURRENT_DATE - 35,
            CURRENT_DATE + 95,
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
        ('d0000000-0000-0000-0000-000000000021', v_batch_id, 1, 'TOPIC_PREPARATION',         'Chuẩn bị và công bố đề tài',      CURRENT_DATE - 35, CURRENT_DATE - 28, ((CURRENT_DATE - 28)::text || ' 23:59:00+07')::timestamptz, false),
        ('d0000000-0000-0000-0000-000000000022', v_batch_id, 2, 'REGISTRATION_AND_SYLLABUS', 'Đăng ký và đề cương',             CURRENT_DATE - 27, CURRENT_DATE - 21, ((CURRENT_DATE - 21)::text || ' 23:59:00+07')::timestamptz, false),
        ('d0000000-0000-0000-0000-000000000023', v_batch_id, 3, 'PROGRESS_EXECUTION',        'Thực hiện và theo dõi tiến độ',   CURRENT_DATE - 20, CURRENT_DATE + 15, ((CURRENT_DATE + 15)::text || ' 23:59:00+07')::timestamptz, false),
        ('d0000000-0000-0000-0000-000000000024', v_batch_id, 4, 'MIDTERM_GATE',              'Đánh giá giữa kỳ',                CURRENT_DATE + 16, CURRENT_DATE + 30, ((CURRENT_DATE + 30)::text || ' 23:59:00+07')::timestamptz, false),
        ('d0000000-0000-0000-0000-000000000025', v_batch_id, 5, 'FINAL_EVALUATION',          'Báo cáo cuối kỳ và phản biện',    CURRENT_DATE + 31, CURRENT_DATE + 65, ((CURRENT_DATE + 65)::text || ' 23:59:00+07')::timestamptz, false),
        ('d0000000-0000-0000-0000-000000000026', v_batch_id, 6, 'DEFENSE',                   'Hội đồng và bảo vệ',              CURRENT_DATE + 66, CURRENT_DATE + 80, ((CURRENT_DATE + 80)::text || ' 23:59:00+07')::timestamptz, false),
        ('d0000000-0000-0000-0000-000000000027', v_batch_id, 7, 'ARCHIVAL_AND_PUBLICATION',  'Lưu chiểu và công bố kết quả',    CURRENT_DATE + 81, CURRENT_DATE + 95, ((CURRENT_DATE + 95)::text || ' 23:59:00+07')::timestamptz, false)
        ON CONFLICT (id) DO UPDATE SET
            start_date = EXCLUDED.start_date,
            end_date = EXCLUDED.end_date,
            deadline_at_utc = EXCLUDED.deadline_at_utc;
    END IF;

    -- Fallback: ensure the active Open batch has standard 10 UIT grading criteria
    IF NOT EXISTS (SELECT 1 FROM councils.grading_criteria WHERE batch_id = v_batch_id) THEN
        INSERT INTO councils.grading_criteria (
            id, batch_id, criteria_code, criteria_name, max_score, order_index, elo, section_code, section_name, section_order
        ) VALUES
        ('d0000000-0000-0000-0000-000000000501', v_batch_id, '1.1', 'Tính mới và độ phức tạp', 1.5, 1, 'LO2', '1', 'Ý nghĩa khoa học, giá trị thực tiễn', 1),
        ('d0000000-0000-0000-0000-000000000502', v_batch_id, '1.2', 'Tính thực tiễn và đóng góp', 1.5, 2, 'LO3', '1', 'Ý nghĩa khoa học, giá trị thực tiễn', 1),
        ('d0000000-0000-0000-0000-000000000503', v_batch_id, '2.1', 'Khả năng tìm hiểu, phân tích yêu cầu và thiết kế hệ thống', 1.0, 3, 'LO3', '2', 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM', 2),
        ('d0000000-0000-0000-0000-000000000504', v_batch_id, '2.2', 'Khả năng hiện thực hóa, kiểm thử và vận hành hệ thống, đánh giá giải pháp', 1.0, 4, 'LO4', '2', 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM', 2),
        ('d0000000-0000-0000-0000-000000000505', v_batch_id, '2.3', 'Học hỏi và cập nhật kiến thức, công nghệ mới, khả năng học tập suốt đời', 1.0, 5, 'LO3', '2', 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM', 2),
        ('d0000000-0000-0000-0000-000000000506', v_batch_id, '3.1', 'Nội dung trình bày cuốn báo và sản phẩm (viết)', 1.0, 6, 'LO6, LO8', '3', 'Trình bày kết quả', 3),
        ('d0000000-0000-0000-0000-000000000507', v_batch_id, '3.2', 'Kỹ năng trình bày (nói)', 1.0, 7, 'LO6', '3', 'Trình bày kết quả', 3),
        ('d0000000-0000-0000-0000-000000000508', v_batch_id, '3.3', 'Trả lời các câu hỏi', 1.0, 8, 'LO8', '3', 'Trình bày kết quả', 3),
        ('d0000000-0000-0000-0000-000000000509', v_batch_id, '4.1', 'Lập kế hoạch và quản lý thời gian, công việc của cá nhân hoặc đội nhóm', 0.5, 9, 'LO5, LO7', '4', 'Kỹ năng mềm', 4),
        ('d0000000-0000-0000-0000-000000000510', v_batch_id, '4.2', 'Kỹ năng tìm hiểu, đọc hiểu tài liệu ngoại ngữ và khả năng tư duy, giải quyết vấn đề', 0.5, 10, 'LO4, LO6', '4', 'Kỹ năng mềm', 4)
        ON CONFLICT (id) DO NOTHING;
    END IF;

    SELECT ARRAY(
        SELECT id FROM councils.grading_criteria WHERE batch_id = v_batch_id ORDER BY order_index LIMIT 10
    ) INTO v_crit_ids;

    -- Topics (Submitted + NeedRevision + 3 Published topics for 3 demo groups)
    INSERT INTO topics.topic (
        id, batch_id, advisor_id, title_vi, title_en, description, requirements, status, student_number, created_at, review_note
    ) VALUES
    (
        v_t_sub, v_batch_id, v_gv1_id,
        'Nền tảng kiểm thử hợp đồng OpenAPI tự động trong CI/CD',
        'Automated OpenAPI Contract Drift Testing Platform for CI/CD',
        E'So sánh đặc tả OpenAPI qua các commit Git và tự động sinh ca kiểm thử hồi quy trên Pull Request.',
        E'- Biết REST/OpenAPI 3.1, GitHub Actions\n- Lập trình C# hoặc TypeScript',
        'Submitted', 2, NOW() - INTERVAL '1 day', NULL
    ),
    (
        v_t_rev, v_batch_id, v_gv2_id,
        'Chatbot hỗ trợ tra cứu học vụ UIT',
        'UIT Academic Policy Q&A Chatbot',
        E'Phát triển chatbot hỏi đáp quy chế đào tạo cho sinh viên.',
        E'- Python, FastAPI, React',
        'NeedRevision', 2, NOW() - INTERVAL '3 days',
        'Đề nghị làm rõ phương pháp đánh giá độ chính xác (RAGAS) và phạm vi dữ liệu cho 2 sinh viên.'
    ),
    (
        v_t1, v_batch_id, v_gv1_id,
        'Trợ lý hỏi đáp học vụ đa phương thức dựa trên GraphRAG',
        'Multimodal Academic Advising Assistant Using GraphRAG',
        E'Kết hợp Neo4j Knowledge Graph và mô hình ngôn ngữ lớn để truy xuất điều kiện tiên quyết kèm trích dẫn điều khoản.',
        E'- Python, FastAPI, Neo4j\n- React TypeScript',
        'Published', 2, NOW() - INTERVAL '40 days', NULL
    ),
    (
        v_t2, v_batch_id, v_gv1_id,
        'Hệ thống phát hiện lỗ hổng bảo mật CI/CD kết hợp phân tích tĩnh và LLM',
        'Static Analysis and LLM-Assisted Vulnerability Detection in CI/CD',
        E'Kết hợp Tree-sitter AST và LLM để phát hiện lỗi logic và gợi ý bản vá trên Pull Request.',
        E'- .NET Core / Go, React\n- Tree-sitter, Docker',
        'Published', 2, NOW() - INTERVAL '42 days', NULL
    ),
    (
        v_t3, v_batch_id, v_gv3_id,
        'Nền tảng phát hiện bất thường log phân tán với eBPF và Flink',
        'Distributed Log Anomaly Detection with eBPF and Apache Flink',
        E'Thu thập syscall qua eBPF và xử lý luồng thời gian thực phát hiện bất thường hệ thống.',
        E'- Linux eBPF, Apache Flink, Kafka',
        'Published', 2, NOW() - INTERVAL '45 days', NULL
    )
    ON CONFLICT (id) DO UPDATE SET
        batch_id = EXCLUDED.batch_id,
        advisor_id = EXCLUDED.advisor_id,
        status = EXCLUDED.status;

    -- 3 Student Groups
    INSERT INTO thesis.student_group (id, topic_id, status, allow_syllabus_resubmission, created_at) VALUES
        (v_g1, v_t1, 'InProgress',                 false, NOW() - INTERVAL '30 days'),
        (v_g2, v_t2, 'MidtermSubmitted',           false, NOW() - INTERVAL '34 days'),
        (v_g3, v_t3, 'ReadyForReviewerAssignment', false, NOW() - INTERVAL '38 days')
    ON CONFLICT (id) DO UPDATE SET status = EXCLUDED.status;

    INSERT INTO thesis.student_group_member (
        id, group_id, student_id, batch_id, confirmation_status, joined_at, confirmed_at_utc
    ) VALUES
        ('d0000000-0000-0000-0000-000000000311', v_g1, v_s1, v_batch_id, 'Confirmed', NOW() - INTERVAL '29 days', NOW() - INTERVAL '29 days'),
        ('d0000000-0000-0000-0000-000000000312', v_g1, v_s2, v_batch_id, 'Confirmed', NOW() - INTERVAL '29 days', NOW() - INTERVAL '29 days'),
        ('d0000000-0000-0000-0000-000000000313', v_g2, v_s3, v_batch_id, 'Confirmed', NOW() - INTERVAL '33 days', NOW() - INTERVAL '33 days'),
        ('d0000000-0000-0000-0000-000000000314', v_g2, v_s4, v_batch_id, 'Confirmed', NOW() - INTERVAL '33 days', NOW() - INTERVAL '33 days'),
        ('d0000000-0000-0000-0000-000000000315', v_g3, v_s5, v_batch_id, 'Confirmed', NOW() - INTERVAL '37 days', NOW() - INTERVAL '37 days'),
        ('d0000000-0000-0000-0000-000000000316', v_g3, v_s6, v_batch_id, 'Confirmed', NOW() - INTERVAL '37 days', NOW() - INTERVAL '37 days')
    ON CONFLICT (id) DO NOTHING;

    -- Syllabi & Midterm Gate
    INSERT INTO thesis.syllabus (id, group_id, current_version_no, status, reviewed_by, reviewed_at, faculty_feedback) VALUES
        ('d0000000-0000-0000-0000-000000000401', v_g1, 1, 'Approved', v_faculty_id, NOW() - INTERVAL '25 days', 'Đề cương đạt chuẩn Cổng 1.'),
        ('d0000000-0000-0000-0000-000000000402', v_g2, 1, 'Approved', v_faculty_id, NOW() - INTERVAL '28 days', 'Đề cương đạt chuẩn Cổng 1.')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
        ('d0000000-0000-0000-0000-000000000403', 'Syllabus', 'd0000000-0000-0000-0000-000000000401', 1, 'demo/g1/syllabus-v1.pdf', 'De-cuong-GraphRAG-v1.pdf', 'application/pdf', 1420000, '.pdf', repeat('a', 64), v_s1, NOW() - INTERVAL '26 days'),
        ('d0000000-0000-0000-0000-000000000404', 'Syllabus', 'd0000000-0000-0000-0000-000000000402', 1, 'demo/g2/syllabus-v1.pdf', 'De-cuong-SecGuard-v1.pdf', 'application/pdf', 1580000, '.pdf', repeat('b', 64), v_s3, NOW() - INTERVAL '29 days')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.midterm_report (id, group_id, current_version_no, status) VALUES
        ('d0000000-0000-0000-0000-000000000405', v_g2, 1, 'Submitted')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
        ('d0000000-0000-0000-0000-000000000406', 'MidtermReport', 'd0000000-0000-0000-0000-000000000405', 1, 'demo/g2/midterm-v1.pdf', 'Bao-cao-giua-ky-SecGuard-v1.pdf', 'application/pdf', 2740000, '.pdf', repeat('c', 64), v_s3, NOW() - INTERVAL '1 day')
    ON CONFLICT (id) DO NOTHING;

    -- Progress Milestones & Multi-Version Reports for Group 1
    INSERT INTO thesis.progress_milestone (id, group_id, title, description, deadline, created_by, created_at) VALUES
        ('d0000000-0000-0000-0000-000000000521', v_g1, 'Mốc 1 — Thiết kế Ontology & Schema Neo4j', E'- Hoàn thiện schema môn học.\n- Nộp tài liệu đặc tả ERD.', NOW() - INTERVAL '14 days', v_gv1_id, NOW() - INTERVAL '22 days'),
        ('d0000000-0000-0000-0000-000000000522', v_g1, 'Mốc 2 — Pipeline GraphRAG & Benchmark',     E'- Xây dựng ingestion pipeline.\n- Benchmark tối thiểu 30 câu hỏi.', NOW() + INTERVAL '4 days',  v_gv1_id, NOW() - INTERVAL '12 days'),
        ('d0000000-0000-0000-0000-000000000523', v_g1, 'Mốc 3 — Giao diện hội thoại & RAGAS',       E'- Hiển thị citation điều khoản.\n- Đánh giá Faithfulness.', NOW() + INTERVAL '14 days', v_gv1_id, NOW() - INTERVAL '3 days')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.progress_report (
        id, milestone_id, group_id, current_version_no, status, submitted_at, reviewed_at, student_note, advisor_feedback
    ) VALUES
        ('d0000000-0000-0000-0000-000000000531', 'd0000000-0000-0000-0000-000000000521', v_g1, 1, 'Accepted', NOW() - INTERVAL '15 days', NOW() - INTERVAL '14 days', 'Đã hoàn thiện schema Neo4j và import dữ liệu mẫu.', 'Đạt yêu cầu Mốc 1.'),
        ('d0000000-0000-0000-0000-000000000532', 'd0000000-0000-0000-0000-000000000522', v_g1, 2, 'Submitted', NOW() - INTERVAL '4 hours', NULL, 'Đã bổ sung benchmark 45 câu hỏi thực tế ở bản v2.', NULL)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.progress_report_version (
        id, progress_report_id, version_no, git_url, student_note, submitted_by, submitted_at, is_late, status, reviewed_by, reviewed_at, advisor_feedback
    ) VALUES
        ('d0000000-0000-0000-0000-000000000541', 'd0000000-0000-0000-0000-000000000531', 1, 'https://github.com/uit-kltn/graphrag/tree/m1', 'Đã hoàn thiện schema Neo4j và import dữ liệu mẫu.', v_s1, NOW() - INTERVAL '15 days', false, 'Accepted', v_gv1_id, NOW() - INTERVAL '14 days', 'Đạt yêu cầu Mốc 1.'),
        ('d0000000-0000-0000-0000-000000000542', 'd0000000-0000-0000-0000-000000000532', 1, 'https://github.com/uit-kltn/graphrag/tree/m2-v1', 'Bản nháp pipeline ingestion.', v_s2, NOW() - INTERVAL '5 days', false, 'NeedRevision', v_gv1_id, NOW() - INTERVAL '3 days', 'Cần mở rộng bộ test tối thiểu 30 câu hỏi.'),
        ('d0000000-0000-0000-0000-000000000543', 'd0000000-0000-0000-0000-000000000532', 2, 'https://github.com/uit-kltn/graphrag/tree/m2-v2', 'Đã bổ sung benchmark 45 câu hỏi thực tế ở bản v2.', v_s2, NOW() - INTERVAL '4 hours', false, 'Submitted', NULL, NULL, NULL)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
        ('d0000000-0000-0000-0000-000000000551', 'ProgressReport', 'd0000000-0000-0000-0000-000000000531', 1, 'demo/g1/m1-v1.pdf', 'Bao-cao-Moc-1-v1.pdf', 'application/pdf', 1820000, '.pdf', repeat('d', 64), v_s1, NOW() - INTERVAL '15 days'),
        ('d0000000-0000-0000-0000-000000000552', 'ProgressReport', 'd0000000-0000-0000-0000-000000000532', 2, 'demo/g1/m2-v2.docx', 'Bao-cao-Moc-2-v2.docx', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 2350000, '.docx', repeat('e', 64), v_s2, NOW() - INTERVAL '4 hours')
    ON CONFLICT (id) DO NOTHING;

    -- Rename Requests for Group 1 (1 Rejected history + 1 AdvisorConfirmed pending Faculty)
    INSERT INTO thesis.rename_request (
        id, group_id, requested_by_student_id, old_title_vi, new_title_vi, old_title_en, new_title_en,
        reason, status, rejection_reason, rejected_by, rejected_at, advisor_confirmed_at, created_at
    ) VALUES
        (
            'd0000000-0000-0000-0000-000000000561', v_g1, v_s1,
            'Trợ lý hỏi đáp học vụ đa phương thức dựa trên GraphRAG',
            'Ứng dụng AI hỗ trợ sinh viên',
            'Multimodal Academic Advising Assistant Using GraphRAG',
            'AI Student Support App',
            'Đề xuất rút ngắn tên đề tài.',
            'RejectedByAdvisor',
            'Tên đề tài mới quá chung chung, làm mất kỹ thuật cốt lõi GraphRAG.',
            v_gv1_id, NOW() - INTERVAL '6 days', NULL, NOW() - INTERVAL '7 days'
        ),
        (
            'd0000000-0000-0000-0000-000000000562', v_g1, v_s1,
            'Trợ lý hỏi đáp học vụ đa phương thức dựa trên GraphRAG',
            'Trợ lý hỏi đáp học vụ đa phương thức dựa trên GraphRAG và Hybrid Search',
            'Multimodal Academic Advising Assistant Using GraphRAG',
            'Multimodal Academic Advising Assistant Using GraphRAG and Hybrid Search',
            'Bổ sung cơ chế Hybrid Search kết hợp BM25 và đồ thị tri thức theo góp ý GVHD.',
            'AdvisorConfirmed',
            NULL, NULL, NULL, NOW() - INTERVAL '1 day', NOW() - INTERVAL '2 days'
        )
    ON CONFLICT (id) DO NOTHING;

    -- Final Reports & Plagiarism Scans (Group 2: 24.6% warning, Group 3: 8.6% low)
    INSERT INTO thesis.final_report (id, group_id, version_type, current_version_no, status, submitted_at) VALUES
        (v_fr2, v_g2, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '5 days'),
        (v_fr3, v_g3, 'InitialFinal', 1, 'UnderEvaluation', NOW() - INTERVAL '7 days')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.submission_file (
        id, entity_type, entity_id, version_no, object_key, file_name,
        content_type, file_size, file_extension, checksum_sha256, uploaded_by, uploaded_at
    ) VALUES
        (v_sf_fr2, 'FinalReport', v_fr2, 1, 'demo/g2/final-v1.pdf', 'KLTN-SecGuard-ToanVan.pdf', 'application/pdf', 4120000, '.pdf', v_sha_fr2, v_s3, NOW() - INTERVAL '5 days'),
        (v_sf_fr3, 'FinalReport', v_fr3, 1, 'demo/g3/final-v1.pdf', 'KLTN-eBPF-Flink-ToanVan.pdf', 'application/pdf', 3890000, '.pdf', v_sha_fr3, v_s5, NOW() - INTERVAL '7 days')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.final_report_plagiarism_scan (
        id, final_report_id, submission_file_id, version_no, status,
        similarity_coverage, max_reranker_score, scanned_chunks, scanned_characters,
        matched_characters, cited_findings_count, uncited_findings_count, suspected_findings_count,
        raw_report_json, attempt_count, created_at, last_polled_at, completed_at
    ) VALUES
        (
            'd0000000-0000-0000-0000-000000000432', v_fr2, v_sf_fr2, 1, 'Completed',
            0.246, 0.86, 104, 64500, 15867, 1, 1, 0,
            v_similarity_json, 1, NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days'
        ),
        (
            'd0000000-0000-0000-0000-000000000433', v_fr3, v_sf_fr3, 1, 'Completed',
            0.086, 0.62, 98, 61200, 5263, 2, 0, 0,
            NULL, 1, NOW() - INTERVAL '6 days', NOW() - INTERVAL '6 days', NOW() - INTERVAL '6 days'
        )
    ON CONFLICT (final_report_id, version_no) DO UPDATE SET
        status = EXCLUDED.status,
        similarity_coverage = EXCLUDED.similarity_coverage,
        raw_report_json = EXCLUDED.raw_report_json;

    -- Advisor & Reviewer Evaluations (Groups 2 & 3)
    INSERT INTO councils.advisor_evaluation (
        id, group_id, score, comment, evaluated_at, allows_reviewer_assignment,
        decision_at, decision_by, evaluated_by, final_report_id, is_legacy,
        report_version_no, submission_checksum_sha256, submission_file_id
    ) VALUES
        (v_adv2, v_g2, 8.50, 'Hệ thống AST + LLM phát hiện lỗ hổng hiệu quả, đủ điều kiện phản biện.', NOW() - INTERVAL '4 days', true, NOW() - INTERVAL '4 days', v_gv1_id, v_gv1_id, v_fr2, false, 1, v_sha_fr2, v_sf_fr2),
        (v_adv3, v_g3, 8.75, 'Võ Tấn Phát thiết kế lõi eBPF tốt (9.0); Đinh Khánh Linh hoàn thiện Flink pipeline (8.0).', NOW() - INTERVAL '6 days', true, NOW() - INTERVAL '6 days', v_gv3_id, v_gv3_id, v_fr3, false, 1, v_sha_fr3, v_sf_fr3)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.reviewer_assignment (
        id, group_id, reviewer_id, assigned_by, assigned_at, assignment_order, is_active,
        advisor_evaluation_id, advisor_id_at_assignment, final_report_id, is_legacy,
        report_version_no, submission_checksum_sha256, submission_file_id
    ) VALUES
        (v_ra2, v_g2, v_gv2_id, v_admin_id, NOW() - INTERVAL '3 days', 1, true, v_adv2, v_gv1_id, v_fr2, false, 1, v_sha_fr2, v_sf_fr2),
        (v_ra3, v_g3, v_gv2_id, v_admin_id, NOW() - INTERVAL '5 days', 1, true, v_adv3, v_gv3_id, v_fr3, false, 1, v_sha_fr3, v_sf_fr3)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.reviewer_evaluation (
        id, assignment_id, score, comment, defense_approval, is_visible_to_student,
        evaluated_at, created_at_utc, created_by, evaluated_by, is_legacy,
        status, submitted_at_utc, submitted_by
    ) VALUES
        (v_re2, v_ra2, 8.25, 'Cần giải trình rõ thêm các đoạn có tỷ lệ tương đồng ở Chương 3.', 'Approved', true, NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days', v_gv2_id, v_gv2_id, false, 'Submitted', NOW() - INTERVAL '2 days', v_gv2_id),
        (v_re3, v_ra3, 8.50, 'Kiến trúc collector eBPF gọn nhẹ, thực nghiệm thuyết phục, đồng ý bảo vệ.', 'Approved', true, NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days', v_gv2_id, v_gv2_id, false, 'Submitted', NOW() - INTERVAL '4 days', v_gv2_id)
    ON CONFLICT (id) DO NOTHING;

    -- Active Council HĐ-DEMO-01 for Group 3 (zero advisor conflict: advisor is gv3; members are gv1, council, gv2)
    INSERT INTO councils.council (
        id, batch_id, council_code, defense_date, time_slot, room, status,
        decision_number, signed_date, approved_by, approved_at, is_submitted_to_academic_affairs
    ) VALUES (
        v_council1, v_batch_id, 'HĐ-DEMO-01', CURRENT_DATE - 3, '08:00 - 11:30 (Sáng)', 'E3.1', 'Active',
        '101/QĐ-ĐHCNTT', CURRENT_DATE - 5, v_admin_id, NOW() - INTERVAL '5 days', true
    ) ON CONFLICT (id) DO UPDATE SET status = 'Active';

    INSERT INTO councils.council_member (id, council_id, user_id, role_in_council) VALUES
        (v_cm1_ch,  v_council1, v_gv1_id,      'Chairman'),
        (v_cm1_sec, v_council1, v_council1_id, 'Secretary'),
        (v_cm1_mem, v_council1, v_gv2_id,      'Member')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.council_assignment (id, council_id, group_id, order_number) VALUES
        (v_ca3, v_council1, v_g3, 1)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.council_score (id, council_assignment_id, council_member_id, score, feedback, evaluated_at, is_ignored_in_calculation) VALUES
        (v_cs3_ch,  v_ca3, v_cm1_ch,  8.75, 'Trình bày mạch lạc, trả lời phản biện tốt.', NOW() - INTERVAL '3 days', false),
        (v_cs3_sec, v_ca3, v_cm1_sec, 8.50, 'Demo ổn định, số liệu đầy đủ.', NOW() - INTERVAL '3 days', false),
        (v_cs3_mem, v_ca3, v_cm1_mem, 8.25, 'Báo cáo rõ ràng, cần chuẩn hóa tài liệu tham khảo.', NOW() - INTERVAL '3 days', false)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.council_minute (
        id, council_assignment_id, general_feedback, lock_status, locked_at, locked_by
    ) VALUES (
        v_min3, v_ca3,
        'Hội đồng thống nhất thông qua. Nhóm hoàn thiện phụ lục Docker và nộp lưu chiểu đúng hạn.',
        'Locked', TIMESTAMPTZ '2026-11-10T08:00:00Z', v_council1_id
    ) ON CONFLICT (id) DO NOTHING;

    -- Per-student evaluation details for Group 3 (SV5 = 9.00, SV6 = 8.00)
    IF array_length(v_crit_ids, 1) = 10 THEN
        FOR v_idx IN 1..10 LOOP
            INSERT INTO councils.evaluation_detail (id, criteria_id, evaluation_id, student_id, score) VALUES
                (gen_random_uuid(), v_crit_ids[v_idx], v_adv3,    v_s5, v_crit_scores_s5[v_idx]),
                (gen_random_uuid(), v_crit_ids[v_idx], v_adv3,    v_s6, v_crit_scores_s6[v_idx]),
                (gen_random_uuid(), v_crit_ids[v_idx], v_cs3_ch,  v_s5, v_crit_scores_s5[v_idx]),
                (gen_random_uuid(), v_crit_ids[v_idx], v_cs3_ch,  v_s6, v_crit_scores_s6[v_idx])
            ON CONFLICT (criteria_id, evaluation_id, student_id) DO NOTHING;
        END LOOP;
    END IF;
END $$;

COMMIT;
'@

    $renderedWorkflowSql = $demoWorkflowSql.
        Replace('__PWD_HASH__', $passwordHash).
        Replace('__SIMILARITY_JSON__', (ConvertTo-SqlLiteral $similarityJson))

    $null = Invoke-PsqlFile -PsqlPath $psql -SqlText $renderedWorkflowSql

    # 4. Seed 1 AdvisorConfirmed Archival package (idempotent) using real council_minute.xmin
    $existingArchival = [int]((Invoke-PsqlScalar -PsqlPath $psql -Query "SELECT count(*) FROM thesis.final_submission WHERE id = 'd0000000-0000-0000-0000-000000000801'::uuid;") | Select-Object -First 1)
    if ($existingArchival -eq 0) {
        $minuteLine = Invoke-PsqlScalar -PsqlPath $psql -Query @"
SELECT t.id::text || '|' || c.batch_id::text || '|' || t.advisor_id::text || '|' ||
       ca.group_id::text || '|' || ca.council_id::text || '|' || ca.id::text || '|' ||
       cm.id::text || '|' || cm.xmin::text || '|' || cm.locked_by::text || '|' ||
       to_char(cm.locked_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"')
FROM councils.council_minute cm
JOIN councils.council_assignment ca ON ca.id = cm.council_assignment_id
JOIN councils.council c ON c.id = ca.council_id
JOIN thesis.student_group g ON g.id = ca.group_id
JOIN topics.topic t ON t.id = g.topic_id
WHERE cm.id = 'd0000000-0000-0000-0000-000000000741'::uuid;
"@ | Select-Object -First 1

        if (-not $minuteLine) {
            throw 'Locked council minute metadata not found for Group 3.'
        }

        $p = $minuteLine.Split('|')
        $defenseSnapshotJson = New-DefenseSnapshotJson `
            -TopicId ([Guid]$p[0]) -BatchId ([Guid]$p[1]) -AdvisorId ([Guid]$p[2]) `
            -GroupId ([Guid]$p[3]) -CouncilId ([Guid]$p[4]) -AssignmentId ([Guid]$p[5]) `
            -MinuteId ([Guid]$p[6]) -MinuteVersion ([uint32]$p[7]) -LockedBy ([Guid]$p[8]) -LockedAtIsoUtc $p[9]

        $subId    = [Guid]'d0000000-0000-0000-0000-000000000801'
        $pkgId    = [Guid]'d0000000-0000-0000-0000-000000000811'
        $revId    = [Guid]'d0000000-0000-0000-0000-000000000851'
        $pdfId    = [Guid]'d0000000-0000-0000-0000-000000000911'
        $docxId   = [Guid]'d0000000-0000-0000-0000-000000000912'
        $groupId  = [Guid]$p[3]
        $advisorId = [Guid]$p[2]
        $studentId = [Guid]'d0000000-0000-0000-0000-000000000305'

        $abstractVi = 'Đề tài xây dựng nền tảng thu thập syscall Linux bằng eBPF và phát hiện bất thường luồng log theo thời gian thực trên Apache Flink với độ trễ đầu cuối dưới 15ms.'
        $abstractEn = 'This thesis builds a Linux syscall telemetry collector using eBPF and a real-time distributed log anomaly detection pipeline on Apache Flink under 15ms latency.'
        $keywordsVi = 'eBPF, Apache Flink, Phát hiện bất thường, Nhật ký phân tán'
        $keywordsEn = 'eBPF, Apache Flink, Anomaly Detection, Distributed Logging'
        $pdfName  = 'KLTN-eBPF-Flink-LuuChieu-v1.pdf'
        $docxName = 'KLTN-eBPF-Flink-LuuChieu-v1.docx'
        $pdfSha   = Get-Sha256Hex "demo-pdf:$pdfId`:$pdfName"
        $docxSha  = Get-Sha256Hex "demo-docx:$docxId`:$docxName"
        $manifestSha = Get-ArchivalManifestSha256 `
            -AbstractVi $abstractVi -AbstractEn $abstractEn `
            -KeywordsVi $keywordsVi -KeywordsEn $keywordsEn `
            -Files @(
                [PSCustomObject]@{ Role = 'ThesisPdf';  FileId = $pdfId;  FileSize = 3650000L; ContentType = 'application/pdf'; ChecksumSha256 = $pdfSha },
                [PSCustomObject]@{ Role = 'ThesisDocx'; FileId = $docxId; FileSize = 2120000L; ContentType = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'; ChecksumSha256 = $docxSha }
            )

        $archivalSql = @"
BEGIN;

INSERT INTO thesis.final_submission (
    id, group_id, status, current_version_no,
    abstract_vi, abstract_en, keywords_vi, keywords_en,
    advisor_confirmed_by, advisor_confirmed_at,
    is_legacy, current_package_id, submitted_by, submitted_at_utc, legacy_snapshot
) VALUES (
    '$subId'::uuid, '$groupId'::uuid, 'AdvisorConfirmed', 1,
    $(ConvertTo-SqlLiteral $abstractVi), $(ConvertTo-SqlLiteral $abstractEn), $(ConvertTo-SqlLiteral $keywordsVi), $(ConvertTo-SqlLiteral $keywordsEn),
    '$advisorId'::uuid, TIMESTAMPTZ '2026-11-12T09:00:00Z',
    false, '$pkgId'::uuid, '$studentId'::uuid, TIMESTAMPTZ '2026-11-11T14:00:00Z', NULL
);

INSERT INTO thesis.final_submission_package (
    id, submission_id, version_no, abstract_vi, abstract_en, keywords_vi, keywords_en,
    submitted_by, submitted_at_utc, manifest_sha256
) VALUES (
    '$pkgId'::uuid, '$subId'::uuid, 1,
    $(ConvertTo-SqlLiteral $abstractVi), $(ConvertTo-SqlLiteral $abstractEn), $(ConvertTo-SqlLiteral $keywordsVi), $(ConvertTo-SqlLiteral $keywordsEn),
    '$studentId'::uuid, TIMESTAMPTZ '2026-11-11T14:00:00Z', '$manifestSha'
);

INSERT INTO thesis.submission_file (
    id, entity_type, entity_id, version_no, file_name, object_key, file_size,
    file_extension, uploaded_by, uploaded_at, checksum_sha256, content_type
) VALUES
(
    '$pdfId'::uuid, 'FinalSubmission', '$subId'::uuid, 1,
    $(ConvertTo-SqlLiteral $pdfName), 'demo/archival/$groupId/v1/$pdfName', 3650000,
    '.pdf', '$studentId'::uuid, TIMESTAMPTZ '2026-11-11T14:00:00Z', '$pdfSha', 'application/pdf'
),
(
    '$docxId'::uuid, 'FinalSubmission', '$subId'::uuid, 1,
    $(ConvertTo-SqlLiteral $docxName), 'demo/archival/$groupId/v1/$docxName', 2120000,
    '.docx', '$studentId'::uuid, TIMESTAMPTZ '2026-11-11T14:00:00Z', '$docxSha', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
);

INSERT INTO thesis.final_submission_package_file (
    package_id, submission_id, version_no, file_role, submission_file_id, entity_type
) VALUES
    ('$pkgId'::uuid, '$subId'::uuid, 1, 'ThesisPdf',  '$pdfId'::uuid,  'FinalSubmission'),
    ('$pkgId'::uuid, '$subId'::uuid, 1, 'ThesisDocx', '$docxId'::uuid, 'FinalSubmission');

INSERT INTO thesis.final_submission_review (
    id, submission_id, package_id, package_version_no, action, stage,
    actor_id, assigned_advisor_id, decided_at_utc, reason,
    manifest_sha256, defense_proof_snapshot, idempotency_key, previous_status, result_status
) VALUES (
    '$revId'::uuid, '$subId'::uuid, '$pkgId'::uuid, 1,
    'AdvisorConfirm', 'Advisor',
    '$advisorId'::uuid, '$advisorId'::uuid, TIMESTAMPTZ '2026-11-12T09:00:00Z', NULL,
    '$manifestSha', $(ConvertTo-SqlLiteral $defenseSnapshotJson)::jsonb, '$([Guid]::NewGuid())'::uuid, 'Submitted', 'AdvisorConfirmed'
);

COMMIT;
"@
        $null = Invoke-PsqlFile -PsqlPath $psql -SqlText $archivalSql
    }

    Write-Host ''
    Write-Host "Seeded UGTM demo environment cleanly (password for all accounts: $Password)" -ForegroundColor Green
    Write-Host '  - Admin:            admin@ugtm.edu.vn'
    Write-Host '  - Faculty:          truongkhoa@ugtm.edu.vn (duyệt đề tài, đổi tên cấp 2, giữa kỳ, lưu chiểu)'
    Write-Host '  - AcademicAffairs:  pdt@ugtm.edu.vn        (duyệt đổi tên cấp 3, hội đồng)'
    Write-Host '  - Lecturer (GVHD):  lecturer@ugtm.edu.vn   (GVHD Nhóm 1 & 2, Chủ tịch HĐ-DEMO-01)'
    Write-Host '  - Lecturers (phụ):  gv2@ugtm.edu.vn (GVPB), gv3@ugtm.edu.vn (GVHD Nhóm 3)'
    Write-Host '  - CouncilMember:    council@ugtm.edu.vn    (Thư ký HĐ-DEMO-01)'
    Write-Host '  - Students:         student@ugtm.edu.vn (chưa có nhóm), 22520101..22520106@gm.uit.edu.vn (3 nhóm xuyên suốt quy trình)'
}
finally {
    Clear-PgEnv
}
