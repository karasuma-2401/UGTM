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
    $tmp = [IO.Path]::Combine([IO.Path]::GetTempPath(), "ugtm_archival_review_seed_$([Guid]::NewGuid().ToString('N')).sql")
    try {
        [IO.File]::WriteAllText($tmp, $SqlText, [System.Text.UTF8Encoding]::new($false))
        $output = & $psql -v ON_ERROR_STOP=1 -f $tmp 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "psql failed (exit $LASTEXITCODE):`n$($output -join "`n")"
        }
        return $output
    }
    finally {
        if (Test-Path -LiteralPath $tmp) {
            Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
        }
    }
}

function Invoke-PsqlScalar([string]$Query) {
    $output = & $psql -t -A -v ON_ERROR_STOP=1 -c $Query 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "psql query failed (exit $LASTEXITCODE):`n$($output -join "`n")"
    }
    return ($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

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
WHERE response_payload::text LIKE '%e5555555-5555-5555-5555-%'
   OR id::text LIKE 'e5555555-5555-5555-5555-%';

DELETE FROM thesis.final_submission_upload_attempt
WHERE group_id::text LIKE 'e5555555-5555-5555-5555-%'
   OR id::text LIKE 'e5555555-5555-5555-5555-%';

DELETE FROM thesis.final_submission_review
WHERE submission_id IN (SELECT id FROM thesis.final_submission WHERE id::text LIKE 'e5555555-5555-5555-5555-%')
   OR id::text LIKE 'e5555555-5555-5555-5555-%';

DELETE FROM thesis.final_submission_package_file
WHERE submission_id IN (SELECT id FROM thesis.final_submission WHERE id::text LIKE 'e5555555-5555-5555-5555-%')
   OR package_id::text LIKE 'e5555555-5555-5555-5555-%';

UPDATE thesis.final_submission
SET current_package_id = NULL,
    is_legacy = true
WHERE id::text LIKE 'e5555555-5555-5555-5555-%';

DELETE FROM thesis.final_submission_package
WHERE submission_id IN (SELECT id FROM thesis.final_submission WHERE id::text LIKE 'e5555555-5555-5555-5555-%')
   OR id::text LIKE 'e5555555-5555-5555-5555-%';

DELETE FROM thesis.final_submission
WHERE id::text LIKE 'e5555555-5555-5555-5555-%';

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

DELETE FROM councils.council_minute
WHERE id::text LIKE 'e5555555-5555-5555-5555-%'
   OR council_assignment_id::text LIKE 'e5555555-5555-5555-5555-%';
DELETE FROM councils.council_assignment WHERE id::text LIKE 'e5555555-5555-5555-5555-%';
DELETE FROM councils.council_member WHERE id::text LIKE 'e5555555-5555-5555-5555-%';
DELETE FROM councils.council WHERE id::text LIKE 'e5555555-5555-5555-5555-%';

DELETE FROM thesis.submission_file WHERE id::text LIKE 'e5555555-5555-5555-5555-%';
DELETE FROM thesis.final_report WHERE id::text LIKE 'e5555555-5555-5555-5555-%';
DELETE FROM thesis.student_group_member WHERE id::text LIKE 'e5555555-5555-5555-5555-%';
DELETE FROM thesis.student_group WHERE id::text LIKE 'e5555555-5555-5555-5555-%';
DELETE FROM topics.topic WHERE id::text LIKE 'e5555555-5555-5555-5555-%';
DELETE FROM identity.user_role WHERE user_id::text LIKE 'e5555555-5555-5555-5555-%';
DELETE FROM identity."user" WHERE id::text LIKE 'e5555555-5555-5555-5555-%';

COMMIT;
'@
    $null = Invoke-PsqlFile $resetSql
    Write-Host 'Reset completed: removed archival review demo records (e5555555-5555-5555-5555-*).' -ForegroundColor Yellow
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

if (-not ('ArchivalReviewSeedHelper' -as [type])) {
    Add-Type -Language CSharp -TypeDefinition @'
#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public enum ReviewSeedLockedDefenseOutcome { Succeeded = 0, Missing = 1, NotLocked = 2, ContextInvalid = 3 }

public sealed record ReviewSeedLockedDefenseProofContractDto(
    ReviewSeedLockedDefenseOutcome Outcome,
    Guid GroupId,
    Guid BatchId,
    Guid? CouncilId,
    Guid? AssignmentId,
    Guid? MinuteId,
    uint? MinuteVersion,
    Guid? LockedBy,
    DateTime? LockedAtUtc,
    string? ProofSha256);

public sealed record ReviewSeedFinalSubmissionDefenseSnapshot(
    Guid TopicId,
    Guid BatchId,
    Guid AssignedAdvisorId,
    ReviewSeedLockedDefenseProofContractDto Defense);

public sealed class ReviewSeedFileSpec
{
    public string Role { get; set; } = "";
    public Guid FileId { get; set; }
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "";
    public string ChecksumSha256 { get; set; } = "";
}

public static class ArchivalReviewSeedHelper
{
    public static string Sha256Hex(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public static string ComputeManifest(
        string abstractVi,
        string abstractEn,
        string keywordsVi,
        string keywordsEn,
        ReviewSeedFileSpec[] files)
    {
        var ordered = files
            .OrderBy(f => f.Role, StringComparer.Ordinal)
            .Select(f => new object[] { f.Role, f.FileId.ToString("N"), f.FileSize, f.ContentType, f.ChecksumSha256 })
            .ToArray();
        var payload = new object[] { abstractVi, abstractEn, keywordsVi, keywordsEn, ordered };
        return Sha256Hex(JsonSerializer.Serialize(payload));
    }

    public static string ComputeDefenseSnapshotJson(
        Guid topicId,
        Guid batchId,
        Guid advisorId,
        Guid groupId,
        Guid councilId,
        Guid assignmentId,
        Guid minuteId,
        uint minuteVersion,
        Guid lockedBy,
        string lockedAtIsoUtc)
    {
        var lockedAt = DateTime.Parse(lockedAtIsoUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        var canonical = JsonSerializer.Serialize(new
        {
            councilId,
            assignmentId,
            Id = minuteId,
            Version = minuteVersion,
            LockedBy = (Guid?)lockedBy,
            LockedAt = (DateTime?)lockedAt
        });
        var proofSha256 = Sha256Hex(canonical);
        var defense = new ReviewSeedLockedDefenseProofContractDto(
            ReviewSeedLockedDefenseOutcome.Succeeded,
            groupId,
            batchId,
            councilId,
            assignmentId,
            minuteId,
            minuteVersion,
            lockedBy,
            lockedAt,
            proofSha256);
        var snapshot = new ReviewSeedFinalSubmissionDefenseSnapshot(topicId, batchId, advisorId, defense);
        return JsonSerializer.Serialize(snapshot);
    }
}
'@
}

$demoPasswordPlain = 'UgtmDemo@2026'
$demoPasswordHash = Get-AspNetIdentityV3Hash $demoPasswordPlain

$setupSql = @'
DO $$
DECLARE
    v_pwd_hash   text := '__DEMO_PWD_HASH__';
    v_batch_open uuid := 'ad3bc9b1-0163-4a99-a711-ecb8bdc0400b';

    v_admin_id   uuid := '11111111-1111-1111-1111-111111111111';
    v_gv_a_id    uuid := '22222222-2222-2222-2222-222222222222'; -- gv.nguyenvana@ugtm.edu.vn
    v_gv_c_id    uuid := '44444444-4444-4444-4444-444444444444'; -- gv.lequangc@ugtm.edu.vn
    v_gv_khang   uuid := '77777777-7777-7777-7777-777777777701'; -- gv.test.khang@ugtm.edu.vn
    v_gv_d_id    uuid := 'a1111111-1111-1111-1111-000000000093'; -- gv.tranminhduc@ugtm.edu.vn
    v_gv_e_id    uuid := 'a1111111-1111-1111-1111-000000000094'; -- gv.danghoangminh@ugtm.edu.vn
    v_faculty_id uuid := 'b2222222-2222-2222-2222-000000000099'; -- truongkhoa@ugtm.edu.vn

    v_role_student  integer;
    v_role_lecturer integer;
    v_role_council  integer;
    v_role_faculty  integer;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM academic.kltn_batch WHERE id = v_batch_open) THEN
        RAISE EXCEPTION 'Expected Open batch % not found.', v_batch_open;
    END IF;

    SELECT id INTO v_role_student  FROM identity.role WHERE role_name = 'Student' LIMIT 1;
    SELECT id INTO v_role_lecturer FROM identity.role WHERE role_name = 'Lecturer' LIMIT 1;
    SELECT id INTO v_role_council  FROM identity.role WHERE role_name = 'CouncilMember' LIMIT 1;
    SELECT id INTO v_role_faculty  FROM identity.role WHERE role_name = 'Faculty' LIMIT 1;

    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    (v_gv_d_id,    'GV0003',    'TS. Trần Minh Đức',        'gv.tranminhduc@ugtm.edu.vn',   'GV.TRANMINHDUC@UGTM.EDU.VN',   v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_gv_e_id,    'GV0004',    'PGS.TS. Đặng Hoàng Minh',   'gv.danghoangminh@ugtm.edu.vn', 'GV.DANGHOANGMINH@UGTM.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    (v_faculty_id, 'FACULTY01', 'PGS.TS. Nguyễn Minh Triết', 'truongkhoa@ugtm.edu.vn',       'TRUONGKHOA@UGTM.EDU.VN',       v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO identity.user_role (user_id, role_id, assigned_at)
    SELECT u_id, r_id, NOW()
    FROM (VALUES
        (v_gv_khang,   v_role_lecturer),
        (v_gv_khang,   v_role_council),
        (v_gv_a_id,    v_role_lecturer),
        (v_gv_a_id,    v_role_council),
        (v_gv_c_id,    v_role_lecturer),
        (v_gv_c_id,    v_role_council),
        (v_gv_d_id,    v_role_lecturer),
        (v_gv_e_id,    v_role_lecturer),
        (v_faculty_id, v_role_faculty)
    ) AS v(u_id, r_id)
    WHERE NOT EXISTS (
        SELECT 1 FROM identity.user_role ur WHERE ur.user_id = v.u_id AND ur.role_id = v.r_id
    );

    -- 10 additional archival review demo students (22520801..22520810)
    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    ('e5555555-5555-5555-5555-000000000301', '22520801', 'Nguyễn Tấn Phát',    '22520801@gm.uit.edu.vn', '22520801@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('e5555555-5555-5555-5555-000000000302', '22520802', 'Trần Ngọc Hân',      '22520802@gm.uit.edu.vn', '22520802@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('e5555555-5555-5555-5555-000000000303', '22520803', 'Lê Hoàng Gia Kiệt',  '22520803@gm.uit.edu.vn', '22520803@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('e5555555-5555-5555-5555-000000000304', '22520804', 'Phạm Quỳnh Anh',     '22520804@gm.uit.edu.vn', '22520804@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('e5555555-5555-5555-5555-000000000305', '22520805', 'Võ Đức Thịnh',       '22520805@gm.uit.edu.vn', '22520805@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('e5555555-5555-5555-5555-000000000306', '22520806', 'Đặng Thu Phương',    '22520806@gm.uit.edu.vn', '22520806@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('e5555555-5555-5555-5555-000000000307', '22520807', 'Hoàng Minh Nhật',    '22520807@gm.uit.edu.vn', '22520807@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('e5555555-5555-5555-5555-000000000308', '22520808', 'Bùi Yến Nhi',        '22520808@gm.uit.edu.vn', '22520808@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('e5555555-5555-5555-5555-000000000309', '22520809', 'Ngô Thanh Tùng',     '22520809@gm.uit.edu.vn', '22520809@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('e5555555-5555-5555-5555-000000000310', '22520810', 'Lý Khánh Vy',        '22520810@gm.uit.edu.vn', '22520810@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO identity.user_role (user_id, role_id, assigned_at)
    SELECT s_id, v_role_student, NOW()
    FROM (VALUES
        ('e5555555-5555-5555-5555-000000000301'::uuid),
        ('e5555555-5555-5555-5555-000000000302'::uuid),
        ('e5555555-5555-5555-5555-000000000303'::uuid),
        ('e5555555-5555-5555-5555-000000000304'::uuid),
        ('e5555555-5555-5555-5555-000000000305'::uuid),
        ('e5555555-5555-5555-5555-000000000306'::uuid),
        ('e5555555-5555-5555-5555-000000000307'::uuid),
        ('e5555555-5555-5555-5555-000000000308'::uuid),
        ('e5555555-5555-5555-5555-000000000309'::uuid),
        ('e5555555-5555-5555-5555-000000000310'::uuid)
    ) AS t(s_id)
    WHERE NOT EXISTS (
        SELECT 1 FROM identity.user_role ur WHERE ur.user_id = t.s_id AND ur.role_id = v_role_student
    );

    -- 5 Topics across different advisors
    INSERT INTO topics.topic (
        id, batch_id, advisor_id, title_vi, title_en, description, requirements, status, student_number, created_at
    ) VALUES
    ('e5555555-5555-5555-5555-000000000101', v_batch_open, v_gv_khang,
        'Hệ thống gợi ý lộ trình học tập cá nhân hóa dựa trên đồ thị tri thức và LLM',
        'Personalized Learning Path Recommendation System Using Knowledge Graphs and LLMs',
        'Xây dựng đồ thị tri thức môn học CNTT và kết hợp truy vấn lai để gợi ý lộ trình học tập cá nhân hóa.',
        'Neo4j, Python, FastAPI, React, LangGraph', 'Approved', 2, NOW() - INTERVAL '58 days'),
    ('e5555555-5555-5555-5555-000000000102', v_batch_open, v_gv_d_id,
        'Tối ưu hóa suy luận mô hình ngôn ngữ lớn đa phương thức bằng lượng tử hóa AWQ trên GPU biên',
        'Optimizing Multimodal LLM Inference via AWQ Quantization on Edge GPUs',
        'Đánh giá sự đánh đổi giữa tốc độ suy luận, bộ nhớ VRAM và độ chính xác khi triển khai VLM 4-bit trên Jetson Orin.',
        'PyTorch, TensorRT-LLM, AWQ, Jetson Orin', 'Approved', 2, NOW() - INTERVAL '58 days'),
    ('e5555555-5555-5555-5555-000000000103', v_batch_open, v_gv_e_id,
        'Nền tảng phát hiện bất thường giao dịch tài chính thời gian thực với Apache Flink và Graph Neural Networks',
        'Real-Time Financial Transaction Anomaly Detection with Apache Flink and GNNs',
        'Phân tích luồng giao dịch độ trễ thấp kết hợp mạng nơ-ron đồ thị động để cảnh báo gian lận.',
        'Apache Flink, Kafka, PyTorch Geometric, PostgreSQL', 'Approved', 2, NOW() - INTERVAL '58 days'),
    ('e5555555-5555-5555-5555-000000000104', v_batch_open, v_gv_a_id,
        'Xây dựng bộ công cụ kiểm thử tải và phân tích nút cổ chai tự động cho hệ thống gRPC phân tán',
        'Automated Load Testing and Bottleneck Analysis Toolkit for Distributed gRPC Systems',
        'Mô phỏng lưu lượng có kiểm soát và tương quan trace OpenTelemetry để khoanh vùng điểm nghẽn dịch vụ.',
        'Go, gRPC, OpenTelemetry, Prometheus, k6', 'Approved', 2, NOW() - INTERVAL '58 days'),
    ('e5555555-5555-5555-5555-000000000105', v_batch_open, v_gv_d_id,
        'Hệ thống nén và truyền phát video thích ứng băng thông sử dụng WebRTC và SVC',
        'Bandwidth-Adaptive Video Streaming System Using WebRTC and Scalable Video Coding',
        'Tự động điều chỉnh lớp mã hóa không gian/thời gian trong hội nghị truyền hình khi mạng biến động.',
        'WebRTC, Pion Go, VP9 SVC, TypeScript', 'Approved', 2, NOW() - INTERVAL '58 days')
    ON CONFLICT (id) DO NOTHING;

    -- 5 Student Groups
    INSERT INTO thesis.student_group (
        id, topic_id, status, created_at, allow_syllabus_resubmission
    ) VALUES
    ('e5555555-5555-5555-5555-000000000201', 'e5555555-5555-5555-5555-000000000101', 'FinalReportSubmitted', NOW() - INTERVAL '52 days', false),
    ('e5555555-5555-5555-5555-000000000202', 'e5555555-5555-5555-5555-000000000102', 'FinalReportSubmitted', NOW() - INTERVAL '52 days', false),
    ('e5555555-5555-5555-5555-000000000203', 'e5555555-5555-5555-5555-000000000103', 'FinalReportSubmitted', NOW() - INTERVAL '52 days', false),
    ('e5555555-5555-5555-5555-000000000204', 'e5555555-5555-5555-5555-000000000104', 'FinalReportSubmitted', NOW() - INTERVAL '52 days', false),
    ('e5555555-5555-5555-5555-000000000205', 'e5555555-5555-5555-5555-000000000105', 'FinalReportSubmitted', NOW() - INTERVAL '52 days', false)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.student_group_member (
        id, group_id, student_id, batch_id, joined_at, confirmation_status, confirmed_at_utc
    ) VALUES
    ('e5555555-5555-5555-5555-000000000321', 'e5555555-5555-5555-5555-000000000201', 'e5555555-5555-5555-5555-000000000301', v_batch_open, NOW() - INTERVAL '50 days', 'Confirmed', NOW() - INTERVAL '50 days'),
    ('e5555555-5555-5555-5555-000000000322', 'e5555555-5555-5555-5555-000000000201', 'e5555555-5555-5555-5555-000000000302', v_batch_open, NOW() - INTERVAL '50 days', 'Confirmed', NOW() - INTERVAL '50 days'),
    ('e5555555-5555-5555-5555-000000000323', 'e5555555-5555-5555-5555-000000000202', 'e5555555-5555-5555-5555-000000000303', v_batch_open, NOW() - INTERVAL '50 days', 'Confirmed', NOW() - INTERVAL '50 days'),
    ('e5555555-5555-5555-5555-000000000324', 'e5555555-5555-5555-5555-000000000202', 'e5555555-5555-5555-5555-000000000304', v_batch_open, NOW() - INTERVAL '50 days', 'Confirmed', NOW() - INTERVAL '50 days'),
    ('e5555555-5555-5555-5555-000000000325', 'e5555555-5555-5555-5555-000000000203', 'e5555555-5555-5555-5555-000000000305', v_batch_open, NOW() - INTERVAL '50 days', 'Confirmed', NOW() - INTERVAL '50 days'),
    ('e5555555-5555-5555-5555-000000000326', 'e5555555-5555-5555-5555-000000000203', 'e5555555-5555-5555-5555-000000000306', v_batch_open, NOW() - INTERVAL '50 days', 'Confirmed', NOW() - INTERVAL '50 days'),
    ('e5555555-5555-5555-5555-000000000327', 'e5555555-5555-5555-5555-000000000204', 'e5555555-5555-5555-5555-000000000307', v_batch_open, NOW() - INTERVAL '50 days', 'Confirmed', NOW() - INTERVAL '50 days'),
    ('e5555555-5555-5555-5555-000000000328', 'e5555555-5555-5555-5555-000000000204', 'e5555555-5555-5555-5555-000000000308', v_batch_open, NOW() - INTERVAL '50 days', 'Confirmed', NOW() - INTERVAL '50 days'),
    ('e5555555-5555-5555-5555-000000000329', 'e5555555-5555-5555-5555-000000000205', 'e5555555-5555-5555-5555-000000000309', v_batch_open, NOW() - INTERVAL '50 days', 'Confirmed', NOW() - INTERVAL '50 days'),
    ('e5555555-5555-5555-5555-000000000330', 'e5555555-5555-5555-5555-000000000205', 'e5555555-5555-5555-5555-000000000310', v_batch_open, NOW() - INTERVAL '50 days', 'Confirmed', NOW() - INTERVAL '50 days')
    ON CONFLICT (id) DO NOTHING;

    -- Councils with zero advisor overlap
    INSERT INTO councils.council (
        id, batch_id, council_code, defense_date, time_slot, room, status, decision_number, signed_date, approved_by, approved_at, is_submitted_to_academic_affairs
    ) VALUES
    ('e5555555-5555-5555-5555-000000000705', v_batch_open, 'HĐ-UIT-05', CURRENT_DATE - 5, '08:00 - 11:30 (Sáng)',  'E3.5', 'Active', '153/QĐ-ĐHCNTT', CURRENT_DATE - 8, v_admin_id, NOW() - INTERVAL '8 days', true),
    ('e5555555-5555-5555-5555-000000000706', v_batch_open, 'HĐ-UIT-06', CURRENT_DATE - 5, '13:30 - 17:00 (Chiều)', 'E3.6', 'Active', '154/QĐ-ĐHCNTT', CURRENT_DATE - 8, v_admin_id, NOW() - INTERVAL '8 days', true)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.council_member (id, council_id, user_id, role_in_council) VALUES
    ('e5555555-5555-5555-5555-000000000711', 'e5555555-5555-5555-5555-000000000705', v_gv_e_id,  'Chairman'),
    ('e5555555-5555-5555-5555-000000000712', 'e5555555-5555-5555-5555-000000000705', v_gv_a_id,  'Secretary'),
    ('e5555555-5555-5555-5555-000000000713', 'e5555555-5555-5555-5555-000000000705', v_gv_c_id,  'Member'),
    ('e5555555-5555-5555-5555-000000000714', 'e5555555-5555-5555-5555-000000000706', v_gv_khang, 'Chairman'),
    ('e5555555-5555-5555-5555-000000000715', 'e5555555-5555-5555-5555-000000000706', v_gv_d_id,  'Secretary'),
    ('e5555555-5555-5555-5555-000000000716', 'e5555555-5555-5555-5555-000000000706', v_gv_c_id,  'Member')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.council_assignment (id, council_id, group_id, order_number) VALUES
    ('e5555555-5555-5555-5555-000000000721', 'e5555555-5555-5555-5555-000000000705', 'e5555555-5555-5555-5555-000000000201', 6),
    ('e5555555-5555-5555-5555-000000000722', 'e5555555-5555-5555-5555-000000000705', 'e5555555-5555-5555-5555-000000000202', 7),
    ('e5555555-5555-5555-5555-000000000723', 'e5555555-5555-5555-5555-000000000706', 'e5555555-5555-5555-5555-000000000203', 8),
    ('e5555555-5555-5555-5555-000000000724', 'e5555555-5555-5555-5555-000000000706', 'e5555555-5555-5555-5555-000000000204', 9),
    ('e5555555-5555-5555-5555-000000000725', 'e5555555-5555-5555-5555-000000000705', 'e5555555-5555-5555-5555-000000000205', 10)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.council_minute (
        id, council_assignment_id, general_feedback, lock_status, locked_at, locked_by
    ) VALUES
    ('e5555555-5555-5555-5555-000000000741', 'e5555555-5555-5555-5555-000000000721', 'Hội đồng đánh giá cao mô hình đồ thị tri thức, hoàn thiện phụ lục prompt trước khi lưu chiểu.', 'Locked', TIMESTAMPTZ '2026-11-11T08:30:00Z', v_gv_a_id),
    ('e5555555-5555-5555-5555-000000000742', 'e5555555-5555-5555-5555-000000000722', 'Thực nghiệm trên Jetson Orin thuyết phục, bổ sung thêm bảng so sánh độ trễ FP16 vs INT4.', 'Locked', TIMESTAMPTZ '2026-11-11T09:30:00Z', v_gv_a_id),
    ('e5555555-5555-5555-5555-000000000743', 'e5555555-5555-5555-5555-000000000723', 'Luồng xử lý Flink ổn định ở 50k sự kiện/giây, đồng ý thông qua nộp lưu chiểu.', 'Locked', TIMESTAMPTZ '2026-11-11T14:00:00Z', v_gv_d_id),
    ('e5555555-5555-5555-5555-000000000744', 'e5555555-5555-5555-5555-000000000724', 'Công cụ đo đạc trực quan, chỉnh lại phần trích dẫn RFC gRPC theo chuẩn IEEE.', 'Locked', TIMESTAMPTZ '2026-11-11T15:00:00Z', v_gv_d_id),
    ('e5555555-5555-5555-5555-000000000725', 'e5555555-5555-5555-5555-000000000725', 'Demo truyền phát đa độ phân giải tốt, cần kiểm tra lại lỗi chính tả trang bìa.', 'Locked', TIMESTAMPTZ '2026-11-11T10:30:00Z', v_gv_a_id)
    ON CONFLICT (id) DO NOTHING;
END $$;
'@

$null = Invoke-PsqlFile ($setupSql.Replace('__DEMO_PWD_HASH__', $demoPasswordHash))

$existingCount = [int]((Invoke-PsqlScalar "SELECT count(*) FROM thesis.final_submission WHERE id::text LIKE 'e5555555-5555-5555-5555-%';") | Select-Object -First 1)
if ($existingCount -gt 0) {
    Write-Host "Archival review demo records already present ($existingCount rows). Run with -Reset first if you want to recreate them." -ForegroundColor Yellow
    return
}

$minuteRows = Invoke-PsqlScalar @"
SELECT ca.group_id::text || '|' || c.batch_id::text || '|' || ca.council_id::text || '|' ||
       ca.id::text || '|' || cm.id::text || '|' || cm.xmin::text || '|' ||
       cm.locked_by::text || '|' || to_char(cm.locked_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"')
FROM councils.council_minute cm
JOIN councils.council_assignment ca ON ca.id = cm.council_assignment_id
JOIN councils.council c ON c.id = ca.council_id
WHERE cm.id::text LIKE 'e5555555-5555-5555-5555-%';
"@

$minuteByGroup = @{}
foreach ($line in $minuteRows) {
    $parts = $line.Split('|')
    if ($parts.Count -eq 8) {
        $minuteByGroup[$parts[0]] = @{
            BatchId      = [Guid]$parts[1]
            CouncilId    = [Guid]$parts[2]
            AssignmentId = [Guid]$parts[3]
            MinuteId     = [Guid]$parts[4]
            MinuteXmin   = [uint32]$parts[5]
            LockedBy     = [Guid]$parts[6]
            LockedAtIso  = $parts[7]
        }
    }
}

$vBatch   = [Guid]'ad3bc9b1-0163-4a99-a711-ecb8bdc0400b'
$vKhang   = [Guid]'77777777-7777-7777-7777-777777777701'
$vGvA     = [Guid]'22222222-2222-2222-2222-222222222222'
$vGvD     = [Guid]'a1111111-1111-1111-1111-000000000093'
$vGvE     = [Guid]'a1111111-1111-1111-1111-000000000094'
$vFaculty = [Guid]'b2222222-2222-2222-2222-000000000099'

function SqlStr([string]$Value) {
    if ($null -eq $Value) { return 'NULL' }
    return "'" + $Value.Replace("'", "''") + "'"
}

$specs = @(
    @{
        SubmissionId        = [Guid]'e5555555-5555-5555-5555-000000000801'
        GroupId             = [Guid]'e5555555-5555-5555-5555-000000000201'
        TopicId             = [Guid]'e5555555-5555-5555-5555-000000000101'
        AdvisorId           = $vKhang
        Status              = 'AdvisorConfirmed'
        AdvisorConfirmedBy  = $vKhang
        AdvisorConfirmedAt  = '2026-11-16T08:15:00Z'
        Packages            = @(
            @{
                PackageId    = [Guid]'e5555555-5555-5555-5555-000000000811'
                VersionNo    = 1
                SubmittedBy  = [Guid]'e5555555-5555-5555-5555-000000000301'
                SubmittedAt  = '2026-11-13T10:00:00Z'
                AbstractVi   = 'Khóa luận xây dựng hệ thống gợi ý lộ trình học tập dựa trên đồ thị tri thức Neo4j và mô hình ngôn ngữ lớn cho sinh viên ngành Công nghệ thông tin.'
                AbstractEn   = 'This thesis presents a personalized learning path recommendation system combining Neo4j knowledge graphs and large language models for IT undergraduate students.'
                KeywordsVi   = 'Đồ thị tri thức, Gợi ý lộ trình học, LLM, Neo4j'
                KeywordsEn   = 'Knowledge Graph, Learning Path Recommendation, LLM, Neo4j'
                PdfFileId    = [Guid]'e5555555-5555-5555-5555-000000000911'
                PdfName      = 'KLTN-KnowledgeGraph-LearningPath-v1.pdf'
                PdfSize      = 3240810L
                DocxFileId   = [Guid]'e5555555-5555-5555-5555-000000000912'
                DocxName     = 'KLTN-KnowledgeGraph-LearningPath-v1.docx'
                DocxSize     = 1984220L
                Reviews      = @(
                    @{
                        ReviewId       = [Guid]'e5555555-5555-5555-5555-000000000851'
                        Action         = 'AdvisorConfirm'
                        Stage          = 'Advisor'
                        ActorId        = $vKhang
                        DecidedAt      = '2026-11-13T15:00:00Z'
                        Reason         = $null
                        PreviousStatus = 'Submitted'
                        ResultStatus   = 'AdvisorConfirmed'
                    },
                    @{
                        ReviewId       = [Guid]'e5555555-5555-5555-5555-000000000852'
                        Action         = 'RequestRevision'
                        Stage          = 'Faculty'
                        ActorId        = $vFaculty
                        DecidedAt      = '2026-11-14T09:20:00Z'
                        Reason         = 'Bổ sung tóm tắt số liệu đánh giá định lượng trên 120 sinh viên thử nghiệm và từ khóa Hybrid Retrieval.'
                        PreviousStatus = 'AdvisorConfirmed'
                        ResultStatus   = 'NeedRevision'
                    }
                )
            },
            @{
                PackageId    = [Guid]'e5555555-5555-5555-5555-000000000812'
                VersionNo    = 2
                SubmittedBy  = [Guid]'e5555555-5555-5555-5555-000000000301'
                SubmittedAt  = '2026-11-15T16:40:00Z'
                AbstractVi   = 'Khóa luận đề xuất và triển khai hệ thống gợi ý lộ trình học tập cá nhân hóa kết hợp đồ thị tri thức môn học trên Neo4j với kiến trúc truy xuất lai (Hybrid Retrieval) và mô hình ngôn ngữ lớn. Thực nghiệm trên 120 sinh viên tình nguyện ghi nhận mức độ hài lòng đạt 89,4% và giảm 31% thời gian tra cứu môn tiên quyết.'
                AbstractEn   = 'This thesis proposes and implements a personalized learning path recommendation platform integrating a Neo4j curriculum knowledge graph with Hybrid Retrieval and Large Language Models. User evaluation across 120 undergraduate students demonstrates an 89.4% satisfaction rate while reducing prerequisite lookup latency by 31%.'
                KeywordsVi   = 'Đồ thị tri thức, Truy xuất lai, Gợi ý lộ trình học, LLM, Neo4j, Giáo dục đại học'
                KeywordsEn   = 'Knowledge Graph, Hybrid Retrieval, Learning Path Recommendation, LLM, Neo4j, Higher Education'
                PdfFileId    = [Guid]'e5555555-5555-5555-5555-000000000913'
                PdfName      = 'KLTN-KnowledgeGraph-LearningPath-Final-v2.pdf'
                PdfSize      = 3612940L
                DocxFileId   = [Guid]'e5555555-5555-5555-5555-000000000914'
                DocxName     = 'KLTN-KnowledgeGraph-LearningPath-Final-v2.docx'
                DocxSize     = 2145600L
                Reviews      = @(
                    @{
                        ReviewId       = [Guid]'e5555555-5555-5555-5555-000000000853'
                        Action         = 'AdvisorConfirm'
                        Stage          = 'Advisor'
                        ActorId        = $vKhang
                        DecidedAt      = '2026-11-16T08:15:00Z'
                        Reason         = $null
                        PreviousStatus = 'Submitted'
                        ResultStatus   = 'AdvisorConfirmed'
                    }
                )
            }
        )
    },
    @{
        SubmissionId        = [Guid]'e5555555-5555-5555-5555-000000000802'
        GroupId             = [Guid]'e5555555-5555-5555-5555-000000000202'
        TopicId             = [Guid]'e5555555-5555-5555-5555-000000000102'
        AdvisorId           = $vGvD
        Status              = 'AdvisorConfirmed'
        AdvisorConfirmedBy  = $vGvD
        AdvisorConfirmedAt  = '2026-11-16T07:20:00Z'
        Packages            = @(
            @{
                PackageId    = [Guid]'e5555555-5555-5555-5555-000000000813'
                VersionNo    = 1
                SubmittedBy  = [Guid]'e5555555-5555-5555-5555-000000000303'
                SubmittedAt  = '2026-11-15T11:15:00Z'
                AbstractVi   = 'Khóa luận nghiên cứu và tối ưu hóa quá trình suy luận của các mô hình ngôn ngữ lớn đa phương thức trên thiết bị GPU biên có bộ nhớ hạn chế như NVIDIA Jetson Orin. Nhóm tác giả áp dụng kỹ thuật lượng tử hóa Activation-Aware Weight Quantization (AWQ) 4-bit kết hợp biên dịch nhân TensorRT-LLM và lập lịch KV-Cache phân trang nhằm giảm thiểu dung lượng bộ nhớ chiếm dụng mà vẫn bảo toàn chất lượng trả lời. Kết quả đánh giá trên bộ tiêu chuẩn MMBench và ScienceQA cho thấy phương pháp đạt mức tiết kiệm 58% bộ nhớ VRAM, tăng thông lượng sinh token gấp 2,4 lần so với mô hình FP16 chuẩn, đồng thời độ chính xác suy giảm không quá 1,3 điểm phần trăm trong điều kiện vận hành thực tế liên tục.'
                AbstractEn   = 'This thesis investigates inference optimization for Multimodal Large Language Models deployed on memory-constrained edge GPUs such as NVIDIA Jetson Orin. We apply 4-bit Activation-Aware Weight Quantization (AWQ) combined with TensorRT-LLM kernel fusion and paged KV-cache scheduling to reduce runtime memory footprint without compromising visual reasoning quality. Extensive benchmarks on MMBench and ScienceQA demonstrate a 58% VRAM reduction and a 2.4x token generation throughput speedup compared to the FP16 baseline while limiting accuracy degradation to less than 1.3 percentage points under sustained workloads.'
                KeywordsVi   = 'Mô hình ngôn ngữ đa phương thức, Lượng tử hóa AWQ, TensorRT-LLM, Điện toán biên, Jetson Orin, Tối ưu suy luận'
                KeywordsEn   = 'Multimodal LLM, AWQ Quantization, TensorRT-LLM, Edge Computing, Jetson Orin, Inference Optimization'
                PdfFileId    = [Guid]'e5555555-5555-5555-5555-000000000915'
                PdfName      = 'KLTN-Multimodal-LLM-AWQ-JetsonOrin.pdf'
                PdfSize      = 4582400L
                DocxFileId   = [Guid]'e5555555-5555-5555-5555-000000000916'
                DocxName     = 'KLTN-Multimodal-LLM-AWQ-JetsonOrin.docx'
                DocxSize     = 2641920L
                Reviews      = @(
                    @{
                        ReviewId       = [Guid]'e5555555-5555-5555-5555-000000000854'
                        Action         = 'AdvisorConfirm'
                        Stage          = 'Advisor'
                        ActorId        = $vGvD
                        DecidedAt      = '2026-11-16T07:20:00Z'
                        Reason         = $null
                        PreviousStatus = 'Submitted'
                        ResultStatus   = 'AdvisorConfirmed'
                    }
                )
            }
        )
    },
    @{
        SubmissionId        = [Guid]'e5555555-5555-5555-5555-000000000803'
        GroupId             = [Guid]'e5555555-5555-5555-5555-000000000203'
        TopicId             = [Guid]'e5555555-5555-5555-5555-000000000103'
        AdvisorId           = $vGvE
        Status              = 'AdvisorConfirmed'
        AdvisorConfirmedBy  = $vGvE
        AdvisorConfirmedAt  = '2026-11-15T19:05:00Z'
        Packages            = @(
            @{
                PackageId    = [Guid]'e5555555-5555-5555-5555-000000000814'
                VersionNo    = 1
                SubmittedBy  = [Guid]'e5555555-5555-5555-5555-000000000305'
                SubmittedAt  = '2026-11-14T20:10:00Z'
                AbstractVi   = 'Đề tài xây dựng hệ thống phát hiện giao dịch tài chính bất thường theo thời gian thực dựa trên nền tảng xử lý luồng Apache Flink kết hợp mạng nơ-ron đồ thị động (Dynamic GNN). Hệ thống duy trì độ trễ đầu cuối dưới 45 mili-giây và đạt chỉ số F1-score 0,942 trên tập dữ liệu mô phỏng thanh toán liên ngân hàng.'
                AbstractEn   = 'This thesis implements a real-time financial anomaly detection pipeline powered by Apache Flink streaming and Dynamic Graph Neural Networks, maintaining end-to-end latency under 45 ms and achieving an F1-score of 0.942 on simulated interbank transaction streams.'
                KeywordsVi   = 'Phát hiện gian lận, Apache Flink, Mạng nơ-ron đồ thị, Xử lý luồng, Tài chính thời gian thực'
                KeywordsEn   = 'Fraud Detection, Apache Flink, Graph Neural Networks, Stream Processing, Real-Time Finance'
                PdfFileId    = [Guid]'e5555555-5555-5555-5555-000000000917'
                PdfName      = 'KLTN-Flink-GNN-FraudDetection.pdf'
                PdfSize      = 3948200L
                DocxFileId   = [Guid]'e5555555-5555-5555-5555-000000000918'
                DocxName     = 'KLTN-Flink-GNN-FraudDetection.docx'
                DocxSize     = 2295100L
                Reviews      = @(
                    @{
                        ReviewId       = [Guid]'e5555555-5555-5555-5555-000000000855'
                        Action         = 'AdvisorConfirm'
                        Stage          = 'Advisor'
                        ActorId        = $vGvE
                        DecidedAt      = '2026-11-15T19:05:00Z'
                        Reason         = $null
                        PreviousStatus = 'Submitted'
                        ResultStatus   = 'AdvisorConfirmed'
                    }
                )
            }
        )
    },
    @{
        SubmissionId        = [Guid]'e5555555-5555-5555-5555-000000000804'
        GroupId             = [Guid]'e5555555-5555-5555-5555-000000000204'
        TopicId             = [Guid]'e5555555-5555-5555-5555-000000000104'
        AdvisorId           = $vGvA
        Status              = 'ArchiveConfirmed'
        AdvisorConfirmedBy  = $vGvA
        AdvisorConfirmedAt  = '2026-11-13T16:40:00Z'
        ArchivedConfirmedBy = $vFaculty
        ArchivedConfirmedAt = '2026-11-15T10:15:00Z'
        Packages            = @(
            @{
                PackageId    = [Guid]'e5555555-5555-5555-5555-000000000815'
                VersionNo    = 1
                SubmittedBy  = [Guid]'e5555555-5555-5555-5555-000000000307'
                SubmittedAt  = '2026-11-13T08:50:00Z'
                AbstractVi   = 'Đề tài phát triển bộ công cụ kiểm thử tải viết bằng Go giúp tự động phát sinh lưu lượng gRPC song song và tương quan dấu vết OpenTelemetry nhằm phát hiện nút cổ chai hiệu năng trong kiến trúc microservices.'
                AbstractEn   = 'This thesis builds a Go-based load testing and OpenTelemetry trace-correlation toolkit to automatically identify tail-latency bottlenecks in distributed gRPC microservice architectures.'
                KeywordsVi   = 'Kiểm thử tải, gRPC, OpenTelemetry, Phân tích nút cổ chai, Microservices'
                KeywordsEn   = 'Load Testing, gRPC, OpenTelemetry, Bottleneck Analysis, Microservices'
                PdfFileId    = [Guid]'e5555555-5555-5555-5555-000000000919'
                PdfName      = 'KLTN-gRPC-LoadTest-OpenTelemetry.pdf'
                PdfSize      = 3310400L
                DocxFileId   = [Guid]'e5555555-5555-5555-5555-000000000920'
                DocxName     = 'KLTN-gRPC-LoadTest-OpenTelemetry.docx'
                DocxSize     = 1912500L
                Reviews      = @(
                    @{
                        ReviewId       = [Guid]'e5555555-5555-5555-5555-000000000856'
                        Action         = 'AdvisorConfirm'
                        Stage          = 'Advisor'
                        ActorId        = $vGvA
                        DecidedAt      = '2026-11-13T16:40:00Z'
                        Reason         = $null
                        PreviousStatus = 'Submitted'
                        ResultStatus   = 'AdvisorConfirmed'
                    },
                    @{
                        ReviewId       = [Guid]'e5555555-5555-5555-5555-000000000857'
                        Action         = 'FacultyConfirm'
                        Stage          = 'Faculty'
                        ActorId        = $vFaculty
                        DecidedAt      = '2026-11-15T10:15:00Z'
                        Reason         = $null
                        PreviousStatus = 'AdvisorConfirmed'
                        ResultStatus   = 'ArchiveConfirmed'
                    }
                )
            }
        )
    },
    @{
        SubmissionId        = [Guid]'e5555555-5555-5555-5555-000000000805'
        GroupId             = [Guid]'e5555555-5555-5555-5555-000000000205'
        TopicId             = [Guid]'e5555555-5555-5555-5555-000000000105'
        AdvisorId           = $vGvD
        Status              = 'NeedRevision'
        AdvisorConfirmedBy  = $vGvD
        AdvisorConfirmedAt  = '2026-11-13T18:00:00Z'
        FacultyFeedback     = 'Trang bìa tệp DOCX còn thiếu tên đồng hướng dẫn và phần Abstract tiếng Anh cần đồng bộ thuật ngữ Scalable Video Coding (SVC) với tiêu đề đề tài.'
        Packages            = @(
            @{
                PackageId    = [Guid]'e5555555-5555-5555-5555-000000000816'
                VersionNo    = 1
                SubmittedBy  = [Guid]'e5555555-5555-5555-5555-000000000309'
                SubmittedAt  = '2026-11-13T11:25:00Z'
                AbstractVi   = 'Khóa luận xây dựng hệ thống truyền phát video hội nghị trực tuyến qua WebRTC tích hợp mã hóa video khả mở VP9 SVC giúp duy trì chất lượng hình ảnh ổn định khi băng thông người dùng biến động mạnh.'
                AbstractEn   = 'This thesis implements a WebRTC video conferencing system leveraging VP9 Scalable Video Coding to adapt spatial and temporal layers dynamically under fluctuating network bandwidth.'
                KeywordsVi   = 'WebRTC, Mã hóa video khả mở, VP9 SVC, Truyền phát thích ứng'
                KeywordsEn   = 'WebRTC, Video Streaming, VP9, Adaptive Bitrate'
                PdfFileId    = [Guid]'e5555555-5555-5555-5555-000000000921'
                PdfName      = 'KLTN-WebRTC-VP9-SVC.pdf'
                PdfSize      = 3488700L
                DocxFileId   = [Guid]'e5555555-5555-5555-5555-000000000922'
                DocxName     = 'KLTN-WebRTC-VP9-SVC.docx'
                DocxSize     = 2015400L
                Reviews      = @(
                    @{
                        ReviewId       = [Guid]'e5555555-5555-5555-5555-000000000858'
                        Action         = 'AdvisorConfirm'
                        Stage          = 'Advisor'
                        ActorId        = $vGvD
                        DecidedAt      = '2026-11-13T18:00:00Z'
                        Reason         = $null
                        PreviousStatus = 'Submitted'
                        ResultStatus   = 'AdvisorConfirmed'
                    },
                    @{
                        ReviewId       = [Guid]'e5555555-5555-5555-5555-000000000859'
                        Action         = 'RequestRevision'
                        Stage          = 'Faculty'
                        ActorId        = $vFaculty
                        DecidedAt      = '2026-11-14T16:10:00Z'
                        Reason         = 'Trang bìa tệp DOCX còn thiếu tên đồng hướng dẫn và phần Abstract tiếng Anh cần đồng bộ thuật ngữ Scalable Video Coding (SVC) với tiêu đề đề tài.'
                        PreviousStatus = 'AdvisorConfirmed'
                        ResultStatus   = 'NeedRevision'
                    }
                )
            }
        )
    }
)

$sb = [System.Text.StringBuilder]::new()
$null = $sb.AppendLine('BEGIN;')

foreach ($spec in $specs) {
    $gKey = $spec.GroupId.ToString()
    if (-not $minuteByGroup.ContainsKey($gKey)) {
        throw "Locked council minute metadata not found for group $gKey."
    }
    $m = $minuteByGroup[$gKey]
    $defenseSnapshotJson = [ArchivalReviewSeedHelper]::ComputeDefenseSnapshotJson(
        $spec.TopicId,
        $vBatch,
        $spec.AdvisorId,
        $spec.GroupId,
        $m.CouncilId,
        $m.AssignmentId,
        $m.MinuteId,
        $m.MinuteXmin,
        $m.LockedBy,
        $m.LockedAtIso
    )

    $currentPkg = $spec.Packages[-1]
    $advBySql  = if ($spec.ContainsKey('AdvisorConfirmedBy') -and $spec.AdvisorConfirmedBy) { "'$($spec.AdvisorConfirmedBy)'::uuid" } else { 'NULL' }
    $advAtSql  = if ($spec.ContainsKey('AdvisorConfirmedAt') -and $spec.AdvisorConfirmedAt) { "TIMESTAMPTZ '$($spec.AdvisorConfirmedAt)'" } else { 'NULL' }
    $archBySql = if ($spec.ContainsKey('ArchivedConfirmedBy') -and $spec.ArchivedConfirmedBy) { "'$($spec.ArchivedConfirmedBy)'::uuid" } else { 'NULL' }
    $archAtSql = if ($spec.ContainsKey('ArchivedConfirmedAt') -and $spec.ArchivedConfirmedAt) { "TIMESTAMPTZ '$($spec.ArchivedConfirmedAt)'" } else { 'NULL' }
    $fbSql     = if ($spec.ContainsKey('FacultyFeedback') -and $spec.FacultyFeedback) { SqlStr $spec.FacultyFeedback } else { 'NULL' }

    $null = $sb.AppendLine(@"
INSERT INTO thesis.final_submission (
    id, group_id, status, current_version_no,
    abstract_vi, abstract_en, keywords_vi, keywords_en,
    advisor_confirmed_by, advisor_confirmed_at, faculty_feedback,
    archived_confirmed_by, archived_confirmed_at,
    is_legacy, current_package_id, submitted_by, submitted_at_utc, legacy_snapshot
) VALUES (
    '$($spec.SubmissionId)'::uuid, '$($spec.GroupId)'::uuid, $(SqlStr $spec.Status), $($currentPkg.VersionNo),
    $(SqlStr $currentPkg.AbstractVi), $(SqlStr $currentPkg.AbstractEn), $(SqlStr $currentPkg.KeywordsVi), $(SqlStr $currentPkg.KeywordsEn),
    $advBySql, $advAtSql, $fbSql,
    $archBySql, $archAtSql,
    false, '$($currentPkg.PackageId)'::uuid, '$($currentPkg.SubmittedBy)'::uuid, TIMESTAMPTZ '$($currentPkg.SubmittedAt)', NULL
);
"@)

    foreach ($pkg in $spec.Packages) {
        $pdfSha  = [ArchivalReviewSeedHelper]::Sha256Hex("demo-pdf:$($pkg.PdfFileId):$($pkg.PdfName)")
        $docxSha = [ArchivalReviewSeedHelper]::Sha256Hex("demo-docx:$($pkg.DocxFileId):$($pkg.DocxName)")

        $fileSpecs = @(
            [ReviewSeedFileSpec]@{
                Role           = 'ThesisPdf'
                FileId         = $pkg.PdfFileId
                FileSize       = $pkg.PdfSize
                ContentType    = 'application/pdf'
                ChecksumSha256 = $pdfSha
            },
            [ReviewSeedFileSpec]@{
                Role           = 'ThesisDocx'
                FileId         = $pkg.DocxFileId
                FileSize       = $pkg.DocxSize
                ContentType    = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
                ChecksumSha256 = $docxSha
            }
        )
        $manifestSha = [ArchivalReviewSeedHelper]::ComputeManifest(
            $pkg.AbstractVi,
            $pkg.AbstractEn,
            $pkg.KeywordsVi,
            $pkg.KeywordsEn,
            $fileSpecs
        )

        $pdfObjectKey  = "demo/archival-review/$($spec.GroupId)/v$($pkg.VersionNo)/$($pkg.PdfName)"
        $docxObjectKey = "demo/archival-review/$($spec.GroupId)/v$($pkg.VersionNo)/$($pkg.DocxName)"

        $null = $sb.AppendLine(@"
INSERT INTO thesis.final_submission_package (
    id, submission_id, version_no, abstract_vi, abstract_en, keywords_vi, keywords_en,
    submitted_by, submitted_at_utc, manifest_sha256
) VALUES (
    '$($pkg.PackageId)'::uuid, '$($spec.SubmissionId)'::uuid, $($pkg.VersionNo),
    $(SqlStr $pkg.AbstractVi), $(SqlStr $pkg.AbstractEn), $(SqlStr $pkg.KeywordsVi), $(SqlStr $pkg.KeywordsEn),
    '$($pkg.SubmittedBy)'::uuid, TIMESTAMPTZ '$($pkg.SubmittedAt)', '$manifestSha'
);

INSERT INTO thesis.submission_file (
    id, entity_type, entity_id, version_no, file_name, object_key, file_size,
    file_extension, uploaded_by, uploaded_at, checksum_sha256, content_type
) VALUES
(
    '$($pkg.PdfFileId)'::uuid, 'FinalSubmission', '$($spec.SubmissionId)'::uuid, $($pkg.VersionNo),
    $(SqlStr $pkg.PdfName), $(SqlStr $pdfObjectKey), $($pkg.PdfSize),
    '.pdf', '$($pkg.SubmittedBy)'::uuid, TIMESTAMPTZ '$($pkg.SubmittedAt)', '$pdfSha', 'application/pdf'
),
(
    '$($pkg.DocxFileId)'::uuid, 'FinalSubmission', '$($spec.SubmissionId)'::uuid, $($pkg.VersionNo),
    $(SqlStr $pkg.DocxName), $(SqlStr $docxObjectKey), $($pkg.DocxSize),
    '.docx', '$($pkg.SubmittedBy)'::uuid, TIMESTAMPTZ '$($pkg.SubmittedAt)', '$docxSha', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
);

INSERT INTO thesis.final_submission_package_file (
    package_id, submission_id, version_no, file_role, submission_file_id, entity_type
) VALUES
(
    '$($pkg.PackageId)'::uuid, '$($spec.SubmissionId)'::uuid, $($pkg.VersionNo), 'ThesisPdf', '$($pkg.PdfFileId)'::uuid, 'FinalSubmission'
),
(
    '$($pkg.PackageId)'::uuid, '$($spec.SubmissionId)'::uuid, $($pkg.VersionNo), 'ThesisDocx', '$($pkg.DocxFileId)'::uuid, 'FinalSubmission'
);
"@)

        foreach ($rev in $pkg.Reviews) {
            $reasonSql = if ($null -ne $rev.Reason) { SqlStr $rev.Reason } else { 'NULL' }
            $idemKey = [Guid]::NewGuid()
            $null = $sb.AppendLine(@"
INSERT INTO thesis.final_submission_review (
    id, submission_id, package_id, package_version_no, action, stage,
    actor_id, assigned_advisor_id, decided_at_utc, reason,
    manifest_sha256, defense_proof_snapshot, idempotency_key, previous_status, result_status
) VALUES (
    '$($rev.ReviewId)'::uuid, '$($spec.SubmissionId)'::uuid, '$($pkg.PackageId)'::uuid, $($pkg.VersionNo),
    $(SqlStr $rev.Action), $(SqlStr $rev.Stage),
    '$($rev.ActorId)'::uuid, '$($spec.AdvisorId)'::uuid, TIMESTAMPTZ '$($rev.DecidedAt)', $reasonSql,
    '$manifestSha', $(SqlStr $defenseSnapshotJson)::jsonb, '$idemKey'::uuid, $(SqlStr $rev.PreviousStatus), $(SqlStr $rev.ResultStatus)
);
"@)
        }
    }
}

$null = $sb.AppendLine('COMMIT;')

$null = Invoke-PsqlFile $sb.ToString()

Write-Host 'Seeded Archival Review Demo data cleanly!' -ForegroundColor Green
Write-Host ' - +3x Chờ nghiệm thu (AdvisorConfirmed, bao gồm 1 hồ sơ nộp lại v2): e5555555-...-0801..0803'
Write-Host ' - +1x Đã nghiệm thu (ArchiveConfirmed): e5555555-...-0804'
Write-Host ' - +1x Cần bổ sung bởi Khoa (NeedRevision): e5555555-...-0805'
Write-Host ' - Tài khoản Khoa (/thesis/review/archival): truongkhoa@ugtm.edu.vn / UgtmDemo@2026'
