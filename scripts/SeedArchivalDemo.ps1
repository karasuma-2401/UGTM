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
    $tmp = [IO.Path]::Combine([IO.Path]::GetTempPath(), "ugtm_archival_seed_$([Guid]::NewGuid().ToString('N')).sql")
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
WHERE response_payload::text LIKE '%c3333333-3333-3333-3333-%'
   OR id::text LIKE 'c3333333-3333-3333-3333-%';

DELETE FROM thesis.final_submission_upload_attempt
WHERE group_id::text LIKE 'c3333333-3333-3333-3333-%'
   OR id::text LIKE 'c3333333-3333-3333-3333-%';

DELETE FROM thesis.final_submission_review
WHERE submission_id IN (SELECT id FROM thesis.final_submission WHERE id::text LIKE 'c3333333-3333-3333-3333-%')
   OR id::text LIKE 'c3333333-3333-3333-3333-%';

DELETE FROM thesis.final_submission_package_file
WHERE submission_id IN (SELECT id FROM thesis.final_submission WHERE id::text LIKE 'c3333333-3333-3333-3333-%')
   OR package_id::text LIKE 'c3333333-3333-3333-3333-%';

UPDATE thesis.final_submission
SET current_package_id = NULL,
    is_legacy = true
WHERE id::text LIKE 'c3333333-3333-3333-3333-%';

DELETE FROM thesis.final_submission_package
WHERE submission_id IN (SELECT id FROM thesis.final_submission WHERE id::text LIKE 'c3333333-3333-3333-3333-%')
   OR id::text LIKE 'c3333333-3333-3333-3333-%';

DELETE FROM thesis.final_submission
WHERE id::text LIKE 'c3333333-3333-3333-3333-%';

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
WHERE id::text LIKE 'c3333333-3333-3333-3333-%'
   OR council_assignment_id::text LIKE 'c3333333-3333-3333-3333-%';
DELETE FROM councils.council_assignment WHERE id::text LIKE 'c3333333-3333-3333-3333-%';
DELETE FROM councils.council_member WHERE id::text LIKE 'c3333333-3333-3333-3333-%';
DELETE FROM councils.council WHERE id::text LIKE 'c3333333-3333-3333-3333-%';

DELETE FROM thesis.submission_file WHERE id::text LIKE 'c3333333-3333-3333-3333-%';
DELETE FROM thesis.final_report WHERE id::text LIKE 'c3333333-3333-3333-3333-%';
DELETE FROM thesis.student_group_member WHERE id::text LIKE 'c3333333-3333-3333-3333-%';
DELETE FROM thesis.student_group WHERE id::text LIKE 'c3333333-3333-3333-3333-%';
DELETE FROM topics.topic WHERE id::text LIKE 'c3333333-3333-3333-3333-%';
DELETE FROM identity.user_role WHERE user_id::text LIKE 'c3333333-3333-3333-3333-%';
DELETE FROM identity."user" WHERE id::text LIKE 'c3333333-3333-3333-3333-%';

COMMIT;
'@
    $null = Invoke-PsqlFile $resetSql
    Write-Host 'Reset completed: removed archival demo records (c3333333-3333-3333-3333-*).' -ForegroundColor Yellow
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

if (-not ('ArchivalSeedHelper' -as [type])) {
    Add-Type -Language CSharp -TypeDefinition @'
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public enum SeedLockedDefenseOutcome { Succeeded = 0, Missing = 1, NotLocked = 2, ContextInvalid = 3 }

public sealed record SeedLockedDefenseProofContractDto(
    SeedLockedDefenseOutcome Outcome,
    Guid GroupId,
    Guid BatchId,
    Guid? CouncilId,
    Guid? AssignmentId,
    Guid? MinuteId,
    uint? MinuteVersion,
    Guid? LockedBy,
    DateTime? LockedAtUtc,
    string? ProofSha256);

public sealed record SeedFinalSubmissionDefenseSnapshot(
    Guid TopicId,
    Guid BatchId,
    Guid AssignedAdvisorId,
    SeedLockedDefenseProofContractDto Defense);

public sealed class SeedFileSpec
{
    public string Role { get; set; } = "";
    public Guid FileId { get; set; }
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "";
    public string ChecksumSha256 { get; set; } = "";
}

public static class ArchivalSeedHelper
{
    public static string Sha256Hex(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public static string ComputeManifest(
        string abstractVi,
        string abstractEn,
        string keywordsVi,
        string keywordsEn,
        SeedFileSpec[] files)
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
        var defense = new SeedLockedDefenseProofContractDto(
            SeedLockedDefenseOutcome.Succeeded,
            groupId,
            batchId,
            councilId,
            assignmentId,
            minuteId,
            minuteVersion,
            lockedBy,
            lockedAt,
            proofSha256);
        var snapshot = new SeedFinalSubmissionDefenseSnapshot(topicId, batchId, advisorId, defense);
        return JsonSerializer.Serialize(snapshot);
    }
}
'@
}

$demoPasswordPlain = 'UgtmDemo@2026'
$demoPasswordHash = Get-AspNetIdentityV3Hash $demoPasswordPlain

# Step 1: Seed users, topics, student groups, members, final reports, councils, and locked defense minutes
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

    -- Ensure lecturers & faculty exist and are active
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

    -- 12 archival demo students (22520501..22520512)
    INSERT INTO identity."user" (
        id, user_code, full_name, email, normalized_email, password_hash,
        status, security_stamp, concurrency_stamp, access_failed_count,
        created_at, updated_at, activated_at, email_verified_at
    ) VALUES
    ('c3333333-3333-3333-3333-000000000301', '22520501', 'Nguyễn Hoàng Nam',   '22520501@gm.uit.edu.vn', '22520501@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('c3333333-3333-3333-3333-000000000302', '22520502', 'Lê Thị Minh Anh',    '22520502@gm.uit.edu.vn', '22520502@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('c3333333-3333-3333-3333-000000000303', '22520503', 'Trần Quốc Khánh',    '22520503@gm.uit.edu.vn', '22520503@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('c3333333-3333-3333-3333-000000000304', '22520504', 'Võ Ngọc Trâm',       '22520504@gm.uit.edu.vn', '22520504@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('c3333333-3333-3333-3333-000000000305', '22520505', 'Đỗ Minh Quân',       '22520505@gm.uit.edu.vn', '22520505@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('c3333333-3333-3333-3333-000000000306', '22520506', 'Bùi Thanh Thảo',     '22520506@gm.uit.edu.vn', '22520506@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('c3333333-3333-3333-3333-000000000307', '22520507', 'Huỳnh Tấn Lộc',      '22520507@gm.uit.edu.vn', '22520507@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('c3333333-3333-3333-3333-000000000308', '22520508', 'Ngô Bảo Ngọc',       '22520508@gm.uit.edu.vn', '22520508@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('c3333333-3333-3333-3333-000000000309', '22520509', 'Trương Gia Huy',     '22520509@gm.uit.edu.vn', '22520509@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('c3333333-3333-3333-3333-000000000310', '22520510', 'Phan Khánh Linh',    '22520510@gm.uit.edu.vn', '22520510@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('c3333333-3333-3333-3333-000000000311', '22520511', 'Dương Hải Đăng',     '22520511@gm.uit.edu.vn', '22520511@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW()),
    ('c3333333-3333-3333-3333-000000000312', '22520512', 'Mai Phương Uyên',    '22520512@gm.uit.edu.vn', '22520512@GM.UIT.EDU.VN', v_pwd_hash, 'Active', gen_random_uuid(), gen_random_uuid(), 0, NOW(), NOW(), NOW(), NOW())
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO identity.user_role (user_id, role_id, assigned_at)
    SELECT s_id, v_role_student, NOW()
    FROM (VALUES
        ('c3333333-3333-3333-3333-000000000301'::uuid),
        ('c3333333-3333-3333-3333-000000000302'::uuid),
        ('c3333333-3333-3333-3333-000000000303'::uuid),
        ('c3333333-3333-3333-3333-000000000304'::uuid),
        ('c3333333-3333-3333-3333-000000000305'::uuid),
        ('c3333333-3333-3333-3333-000000000306'::uuid),
        ('c3333333-3333-3333-3333-000000000307'::uuid),
        ('c3333333-3333-3333-3333-000000000308'::uuid),
        ('c3333333-3333-3333-3333-000000000309'::uuid),
        ('c3333333-3333-3333-3333-000000000310'::uuid),
        ('c3333333-3333-3333-3333-000000000311'::uuid),
        ('c3333333-3333-3333-3333-000000000312'::uuid)
    ) AS t(s_id)
    WHERE NOT EXISTS (
        SELECT 1 FROM identity.user_role ur WHERE ur.user_id = t.s_id AND ur.role_id = v_role_student
    );

    -- 6 Topics (5 advised by TS. Phạm Minh Khang, 1 advised by GV0001)
    INSERT INTO topics.topic (
        id, batch_id, advisor_id, title_vi, title_en, description, requirements, status, student_number, created_at
    ) VALUES
    ('c3333333-3333-3333-3333-000000000101', v_batch_open, v_gv_khang,
        'Hệ thống phát hiện xâm nhập mạng IoT theo thời gian thực bằng Federated Learning',
        'Real-Time IoT Network Intrusion Detection Using Federated Learning',
        'Huấn luyện mô hình phát hiện tấn công mạng IoT phân tán trên thiết bị biên mà không tập trung dữ liệu thô.',
        'Python, Flower, MQTT, Raspberry Pi', 'Approved', 2, NOW() - INTERVAL '60 days'),
    ('c3333333-3333-3333-3333-000000000102', v_batch_open, v_gv_khang,
        'Ứng dụng LLM và AST trong sinh ca kiểm thử đơn vị tự động cho mã nguồn Java',
        'Automated Java Unit Test Generation Using LLM and Abstract Syntax Trees',
        'Kết hợp phân tích cây cú pháp trừu tượng AST và mô hình ngôn ngữ lớn để tăng độ phủ kiểm thử JUnit 5.',
        'Java 21, Tree-sitter, JUnit 5, JaCoCo', 'Approved', 2, NOW() - INTERVAL '60 days'),
    ('c3333333-3333-3333-3333-000000000103', v_batch_open, v_gv_khang,
        'Nền tảng phân tích dữ liệu nhật ký phân tán với ClickHouse và OpenTelemetry',
        'Distributed Log Analytics Platform with ClickHouse and OpenTelemetry',
        'Thu thập, chuẩn hóa và truy vấn nhật ký hệ thống quy mô lớn với độ trễ dưới 1 giây.',
        'ClickHouse, OpenTelemetry, Go, Grafana', 'Approved', 2, NOW() - INTERVAL '60 days'),
    ('c3333333-3333-3333-3333-000000000104', v_batch_open, v_gv_khang,
        'Hệ thống nhận diện ngôn ngữ ký hiệu tiếng Việt thời gian thực trên thiết bị di động',
        'Real-Time Vietnamese Sign Language Recognition on Mobile Devices',
        'Xây dựng mô hình nhẹ kết hợp MediaPipe Holistic và Temporal Convolutional Network chạy trên Android.',
        'PyTorch, TFLite, MediaPipe, Kotlin', 'Approved', 2, NOW() - INTERVAL '60 days'),
    ('c3333333-3333-3333-3333-000000000105', v_batch_open, v_gv_khang,
        'Xây dựng công cụ kiểm chứng hợp đồng thông minh EVM bằng phân tích ký hiệu',
        'Symbolic Execution Verification Tool for EVM Smart Contracts',
        'Phát hiện tự động lỗ hổng reentrancy và integer overflow trên bytecode EVM trước khi triển khai.',
        'Rust, Z3 Solver, EVM Bytecode', 'Approved', 2, NOW() - INTERVAL '60 days'),
    ('c3333333-3333-3333-3333-000000000106', v_batch_open, v_gv_a_id,
        'Thiết kế kiến trúc Zero-Trust cho hệ thống Microservices trên Kubernetes',
        'Zero-Trust Security Architecture for Kubernetes Microservices',
        'Áp dụng SPIFFE/SPIRE và Istio mTLS kết hợp OPA để kiểm soát truy cập dịch vụ chi tiết.',
        'Kubernetes, Istio, SPIRE, Open Policy Agent', 'Approved', 2, NOW() - INTERVAL '60 days')
    ON CONFLICT (id) DO NOTHING;

    -- 6 Student Groups
    INSERT INTO thesis.student_group (
        id, topic_id, status, created_at, allow_syllabus_resubmission
    ) VALUES
    ('c3333333-3333-3333-3333-000000000201', 'c3333333-3333-3333-3333-000000000101', 'FinalReportSubmitted', NOW() - INTERVAL '55 days', false),
    ('c3333333-3333-3333-3333-000000000202', 'c3333333-3333-3333-3333-000000000102', 'FinalReportSubmitted', NOW() - INTERVAL '55 days', false),
    ('c3333333-3333-3333-3333-000000000203', 'c3333333-3333-3333-3333-000000000103', 'FinalReportSubmitted', NOW() - INTERVAL '55 days', false),
    ('c3333333-3333-3333-3333-000000000204', 'c3333333-3333-3333-3333-000000000104', 'FinalReportSubmitted', NOW() - INTERVAL '55 days', false),
    ('c3333333-3333-3333-3333-000000000205', 'c3333333-3333-3333-3333-000000000105', 'FinalReportSubmitted', NOW() - INTERVAL '55 days', false),
    ('c3333333-3333-3333-3333-000000000206', 'c3333333-3333-3333-3333-000000000106', 'FinalReportSubmitted', NOW() - INTERVAL '55 days', false)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO thesis.student_group_member (
        id, group_id, student_id, batch_id, joined_at, confirmation_status, confirmed_at_utc
    ) VALUES
    ('c3333333-3333-3333-3333-000000000321', 'c3333333-3333-3333-3333-000000000201', 'c3333333-3333-3333-3333-000000000301', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days'),
    ('c3333333-3333-3333-3333-000000000322', 'c3333333-3333-3333-3333-000000000201', 'c3333333-3333-3333-3333-000000000302', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days'),
    ('c3333333-3333-3333-3333-000000000323', 'c3333333-3333-3333-3333-000000000202', 'c3333333-3333-3333-3333-000000000303', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days'),
    ('c3333333-3333-3333-3333-000000000324', 'c3333333-3333-3333-3333-000000000202', 'c3333333-3333-3333-3333-000000000304', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days'),
    ('c3333333-3333-3333-3333-000000000325', 'c3333333-3333-3333-3333-000000000203', 'c3333333-3333-3333-3333-000000000305', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days'),
    ('c3333333-3333-3333-3333-000000000326', 'c3333333-3333-3333-3333-000000000203', 'c3333333-3333-3333-3333-000000000306', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days'),
    ('c3333333-3333-3333-3333-000000000327', 'c3333333-3333-3333-3333-000000000204', 'c3333333-3333-3333-3333-000000000307', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days'),
    ('c3333333-3333-3333-3333-000000000328', 'c3333333-3333-3333-3333-000000000204', 'c3333333-3333-3333-3333-000000000308', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days'),
    ('c3333333-3333-3333-3333-000000000329', 'c3333333-3333-3333-3333-000000000205', 'c3333333-3333-3333-3333-000000000309', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days'),
    ('c3333333-3333-3333-3333-000000000330', 'c3333333-3333-3333-3333-000000000205', 'c3333333-3333-3333-3333-000000000310', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days'),
    ('c3333333-3333-3333-3333-000000000331', 'c3333333-3333-3333-3333-000000000206', 'c3333333-3333-3333-3333-000000000311', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days'),
    ('c3333333-3333-3333-3333-000000000332', 'c3333333-3333-3333-3333-000000000206', 'c3333333-3333-3333-3333-000000000312', v_batch_open, NOW() - INTERVAL '54 days', 'Confirmed', NOW() - INTERVAL '54 days')
    ON CONFLICT (id) DO NOTHING;

    -- Defense Councils (HĐ-UIT-03 for Khang's groups; HĐ-UIT-04 for GV0001's group -> zero advisor conflict)
    INSERT INTO councils.council (
        id, batch_id, council_code, defense_date, time_slot, room, status, decision_number, signed_date, approved_by, approved_at, is_submitted_to_academic_affairs
    ) VALUES
    ('c3333333-3333-3333-3333-000000000703', v_batch_open, 'HĐ-UIT-03', CURRENT_DATE - 7, '08:00 - 11:30 (Sáng)',  'E3.3', 'Active', '151/QĐ-ĐHCNTT', CURRENT_DATE - 10, v_admin_id, NOW() - INTERVAL '10 days', true),
    ('c3333333-3333-3333-3333-000000000704', v_batch_open, 'HĐ-UIT-04', CURRENT_DATE - 6, '13:30 - 17:00 (Chiều)', 'E3.4', 'Active', '152/QĐ-ĐHCNTT', CURRENT_DATE - 10, v_admin_id, NOW() - INTERVAL '10 days', true)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.council_member (id, council_id, user_id, role_in_council) VALUES
    ('c3333333-3333-3333-3333-000000000711', 'c3333333-3333-3333-3333-000000000703', v_gv_d_id,  'Chairman'),
    ('c3333333-3333-3333-3333-000000000712', 'c3333333-3333-3333-3333-000000000703', v_gv_a_id,  'Secretary'),
    ('c3333333-3333-3333-3333-000000000713', 'c3333333-3333-3333-3333-000000000703', v_gv_e_id,  'Member'),
    ('c3333333-3333-3333-3333-000000000714', 'c3333333-3333-3333-3333-000000000704', v_gv_khang, 'Chairman'),
    ('c3333333-3333-3333-3333-000000000715', 'c3333333-3333-3333-3333-000000000704', v_gv_d_id,  'Secretary'),
    ('c3333333-3333-3333-3333-000000000716', 'c3333333-3333-3333-3333-000000000704', v_gv_c_id,  'Member')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO councils.council_assignment (id, council_id, group_id, order_number) VALUES
    ('c3333333-3333-3333-3333-000000000721', 'c3333333-3333-3333-3333-000000000703', 'c3333333-3333-3333-3333-000000000201', 1),
    ('c3333333-3333-3333-3333-000000000722', 'c3333333-3333-3333-3333-000000000703', 'c3333333-3333-3333-3333-000000000202', 2),
    ('c3333333-3333-3333-3333-000000000723', 'c3333333-3333-3333-3333-000000000703', 'c3333333-3333-3333-3333-000000000203', 3),
    ('c3333333-3333-3333-3333-000000000724', 'c3333333-3333-3333-3333-000000000703', 'c3333333-3333-3333-3333-000000000204', 4),
    ('c3333333-3333-3333-3333-000000000725', 'c3333333-3333-3333-3333-000000000703', 'c3333333-3333-3333-3333-000000000205', 5),
    ('c3333333-3333-3333-3333-000000000726', 'c3333333-3333-3333-3333-000000000704', 'c3333333-3333-3333-3333-000000000206', 1)
    ON CONFLICT (id) DO NOTHING;

    -- Locked Defense Minutes (whole-second UTC timestamps prior to archival submissions)
    INSERT INTO councils.council_minute (
        id, council_assignment_id, general_feedback, lock_status, locked_at, locked_by
    ) VALUES
    ('c3333333-3333-3333-3333-000000000741', 'c3333333-3333-3333-3333-000000000721', 'Hội đồng thống nhất thông qua. Cần hoàn thiện phần đối chiếu chỉ số F1-score trước khi lưu chiểu.', 'Locked', TIMESTAMPTZ '2026-11-10T08:00:00Z', v_gv_a_id),
    ('c3333333-3333-3333-3333-000000000742', 'c3333333-3333-3333-3333-000000000722', 'Kết quả thực nghiệm tốt. Chỉnh sửa lại định dạng tài liệu tham khảo IEEE trước khi lưu chiểu.', 'Locked', TIMESTAMPTZ '2026-11-10T09:00:00Z', v_gv_a_id),
    ('c3333333-3333-3333-3333-000000000743', 'c3333333-3333-3333-3333-000000000723', 'Cần bổ sung bảng benchmark hiệu năng truy vấn ClickHouse ở Chương 4 trong bản lưu chiểu.', 'Locked', TIMESTAMPTZ '2026-11-10T10:00:00Z', v_gv_a_id),
    ('c3333333-3333-3333-3333-000000000744', 'c3333333-3333-3333-3333-000000000724', 'Demo trên thiết bị di động ổn định, đồng ý thông qua nộp lưu chiểu sau khi rà soát chính tả.', 'Locked', TIMESTAMPTZ '2026-11-10T11:00:00Z', v_gv_a_id),
    ('c3333333-3333-3333-3333-000000000745', 'c3333333-3333-3333-3333-000000000725', 'Đề tài có giá trị học thuật cao, đủ điều kiện lưu chiểu và công bố.', 'Locked', TIMESTAMPTZ '2026-11-10T14:00:00Z', v_gv_a_id),
    ('c3333333-3333-3333-3333-000000000746', 'c3333333-3333-3333-3333-000000000726', 'Kiến trúc bảo mật rõ ràng, hoàn thiện sơ đồ triển khai trước khi nộp lưu chiểu.', 'Locked', TIMESTAMPTZ '2026-11-11T08:00:00Z', v_gv_d_id)
    ON CONFLICT (id) DO NOTHING;
END $$;
'@

$null = Invoke-PsqlFile ($setupSql.Replace('__DEMO_PWD_HASH__', $demoPasswordHash))

# Check if archival submissions already exist for c3333333-* so re-running without -Reset is idempotent
$existingCount = [int]((Invoke-PsqlScalar "SELECT count(*) FROM thesis.final_submission WHERE id::text LIKE 'c3333333-3333-3333-3333-%';") | Select-Object -First 1)
if ($existingCount -gt 0) {
    Write-Host "Archival demo records already present ($existingCount rows). Run with -Reset first if you want to recreate them." -ForegroundColor Yellow
    return
}

# Step 2: Read actual xmin/locked_at/locked_by of each locked council_minute to build exact defense_proof_snapshot
$minuteRows = Invoke-PsqlScalar @"
SELECT ca.group_id::text || '|' || c.batch_id::text || '|' || ca.council_id::text || '|' ||
       ca.id::text || '|' || cm.id::text || '|' || cm.xmin::text || '|' ||
       cm.locked_by::text || '|' || to_char(cm.locked_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"')
FROM councils.council_minute cm
JOIN councils.council_assignment ca ON ca.id = cm.council_assignment_id
JOIN councils.council c ON c.id = ca.council_id
WHERE cm.id::text LIKE 'c3333333-3333-3333-3333-%';
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
$vFaculty = [Guid]'b2222222-2222-2222-2222-000000000099'

function SqlStr([string]$Value) {
    if ($null -eq $Value) { return 'NULL' }
    return "'" + $Value.Replace("'", "''") + "'"
}

# Define 6 archival submissions across all workflow states
$specs = @(
    @{
        SubmissionId = [Guid]'c3333333-3333-3333-3333-000000000801'
        GroupId      = [Guid]'c3333333-3333-3333-3333-000000000201'
        TopicId      = [Guid]'c3333333-3333-3333-3333-000000000101'
        AdvisorId    = $vKhang
        Status       = 'Submitted'
        Packages     = @(
            @{
                PackageId    = [Guid]'c3333333-3333-3333-3333-000000000811'
                VersionNo    = 1
                SubmittedBy  = [Guid]'c3333333-3333-3333-3333-000000000301'
                SubmittedAt  = '2026-11-12T08:30:00Z'
                AbstractVi   = 'Đề tài đề xuất kiến trúc Federated Learning kết hợp mô hình nhẹ 1D-CNN nhằm phát hiện xâm nhập mạng IoT theo thời gian thực mà không cần gửi dữ liệu lưu lượng thô về máy chủ trung tâm.'
                AbstractEn   = 'This thesis proposes a Federated Learning architecture integrated with a lightweight 1D-CNN model to detect IoT network intrusions in real time without centralizing raw traffic data.'
                KeywordsVi   = 'Federated Learning, IoT, Phát hiện xâm nhập, 1D-CNN, Bảo mật mạng'
                KeywordsEn   = 'Federated Learning, IoT, Intrusion Detection, 1D-CNN, Network Security'
                PdfFileId    = [Guid]'c3333333-3333-3333-3333-000000000911'
                PdfName      = 'KLTN-IoT-FederatedLearning-v1.pdf'
                PdfSize      = 3421800L
                DocxFileId   = [Guid]'c3333333-3333-3333-3333-000000000912'
                DocxName     = 'KLTN-IoT-FederatedLearning-v1.docx'
                DocxSize     = 1984200L
                Reviews      = @(
                    @{
                        ReviewId       = [Guid]'c3333333-3333-3333-3333-000000000851'
                        Action         = 'RequestRevision'
                        Stage          = 'Advisor'
                        ActorId        = $vKhang
                        DecidedAt      = '2026-11-13T09:15:00Z'
                        Reason         = 'Cần bổ sung bảng đối chiếu F1-score giữa mô hình tập trung và Federated Learning trên tập CICIoT2023 ở Chương 4 và cập nhật lại từ khóa tiếng Anh.'
                        PreviousStatus = 'Submitted'
                        ResultStatus   = 'NeedRevision'
                    }
                )
            },
            @{
                PackageId    = [Guid]'c3333333-3333-3333-3333-000000000812'
                VersionNo    = 2
                SubmittedBy  = [Guid]'c3333333-3333-3333-3333-000000000302'
                SubmittedAt  = '2026-11-15T10:20:00Z'
                AbstractVi   = 'Đề tài xây dựng hệ thống phát hiện xâm nhập mạng IoT theo thời gian thực ứng dụng Federated Learning (FedAvg và FedProx) kết hợp mô hình 1D-CNN tối ưu. Kết quả thực nghiệm trên bộ dữ liệu CICIoT2023 cho thấy độ chính xác đạt 97,8% và F1-score đạt 97,2%, bảo toàn quyền riêng tư dữ liệu biên với độ trễ suy luận dưới 15ms trên Raspberry Pi 4.'
                AbstractEn   = 'This thesis develops a real-time IoT network intrusion detection system utilizing Federated Learning (FedAvg and FedProx) combined with an optimized 1D-CNN model. Experimental evaluation on the CICIoT2023 dataset achieves 97.8% accuracy and a 97.2% F1-score while preserving edge data privacy and maintaining inference latency under 15ms on Raspberry Pi 4.'
                KeywordsVi   = 'Federated Learning, An ninh mạng IoT, Phát hiện xâm nhập, Điện toán biên, 1D-CNN'
                KeywordsEn   = 'Federated Learning, IoT Security, Intrusion Detection, Edge Computing, 1D-CNN, CICIoT2023'
                PdfFileId    = [Guid]'c3333333-3333-3333-3333-000000000913'
                PdfName      = 'KLTN-IoT-FederatedLearning-Final-v2.pdf'
                PdfSize      = 3894512L
                DocxFileId   = [Guid]'c3333333-3333-3333-3333-000000000914'
                DocxName     = 'KLTN-IoT-FederatedLearning-Final-v2.docx'
                DocxSize     = 2210480L
                Reviews      = @()
            }
        )
    },
    @{
        SubmissionId = [Guid]'c3333333-3333-3333-3333-000000000802'
        GroupId      = [Guid]'c3333333-3333-3333-3333-000000000202'
        TopicId      = [Guid]'c3333333-3333-3333-3333-000000000102'
        AdvisorId    = $vKhang
        Status       = 'Submitted'
        Packages     = @(
            @{
                PackageId    = [Guid]'c3333333-3333-3333-3333-000000000813'
                VersionNo    = 1
                SubmittedBy  = [Guid]'c3333333-3333-3333-3333-000000000303'
                SubmittedAt  = '2026-11-15T14:10:00Z'
                AbstractVi   = 'Khóa luận nghiên cứu phương pháp kết hợp phân tích cây cú pháp trừu tượng (AST) với mô hình ngôn ngữ lớn (LLM) để tự động sinh ca kiểm thử đơn vị JUnit 5 cho dự án Java. Hệ thống trích xuất ngữ cảnh phụ thuộc, tự động sửa lỗi biên dịch theo vòng lặp phản hồi từ JaCoCo, giúp nâng tỷ lệ bao phủ nhánh trung bình thêm 24,5% trên tập kiểm thử Defects4J.'
                AbstractEn   = 'This thesis investigates combining Abstract Syntax Tree (AST) analysis with Large Language Models (LLMs) to automatically generate JUnit 5 unit tests for Java repositories. By extracting dependency context and iteratively repairing compilation errors with JaCoCo feedback, the system improves average branch coverage by 24.5% on the Defects4J benchmark.'
                KeywordsVi   = 'Sinh ca kiểm thử tự động, Mô hình ngôn ngữ lớn, Cây cú pháp trừu tượng, Java, JUnit 5'
                KeywordsEn   = 'Automated Unit Test Generation, Large Language Models, Abstract Syntax Tree, Java, JUnit 5'
                PdfFileId    = [Guid]'c3333333-3333-3333-3333-000000000915'
                PdfName      = 'KLTN-LLM-AST-UnitTestGen.pdf'
                PdfSize      = 4120340L
                DocxFileId   = [Guid]'c3333333-3333-3333-3333-000000000916'
                DocxName     = 'KLTN-LLM-AST-UnitTestGen.docx'
                DocxSize     = 2480120L
                Reviews      = @()
            }
        )
    },
    @{
        SubmissionId = [Guid]'c3333333-3333-3333-3333-000000000803'
        GroupId      = [Guid]'c3333333-3333-3333-3333-000000000203'
        TopicId      = [Guid]'c3333333-3333-3333-3333-000000000103'
        AdvisorId    = $vKhang
        Status       = 'NeedRevision'
        FacultyFeedback = 'Bản PDF chưa cập nhật bảng benchmark hiệu năng ClickHouse theo góp ý của Hội đồng ở Chương 4 và phần từ khóa tiếng Anh còn thiếu thuật ngữ OpenTelemetry.'
        Packages     = @(
            @{
                PackageId    = [Guid]'c3333333-3333-3333-3333-000000000814'
                VersionNo    = 1
                SubmittedBy  = [Guid]'c3333333-3333-3333-3333-000000000305'
                SubmittedAt  = '2026-11-13T07:45:00Z'
                AbstractVi   = 'Đề tài thiết kế và triển khai nền tảng thu thập, lưu trữ và phân tích nhật ký phân tán thời gian thực sử dụng OpenTelemetry Collector và cơ sở dữ liệu cột ClickHouse, hỗ trợ truy vấn đa chiều cho hệ thống microservices.'
                AbstractEn   = 'This thesis designs and implements a real-time distributed log ingestion, storage, and analytics platform powered by OpenTelemetry Collector and ClickHouse columnar database for multi-dimensional microservice observability.'
                KeywordsVi   = 'Nhật ký phân tán, ClickHouse, OpenTelemetry, Giám sát hệ thống'
                KeywordsEn   = 'Distributed Logging, ClickHouse, Microservices, Observability'
                PdfFileId    = [Guid]'c3333333-3333-3333-3333-000000000917'
                PdfName      = 'KLTN-ClickHouse-LogAnalytics.pdf'
                PdfSize      = 3580900L
                DocxFileId   = [Guid]'c3333333-3333-3333-3333-000000000918'
                DocxName     = 'KLTN-ClickHouse-LogAnalytics.docx'
                DocxSize     = 2054300L
                Reviews      = @(
                    @{
                        ReviewId       = [Guid]'c3333333-3333-3333-3333-000000000852'
                        Action         = 'RequestRevision'
                        Stage          = 'Advisor'
                        ActorId        = $vKhang
                        DecidedAt      = '2026-11-14T16:30:00Z'
                        Reason         = 'Bản PDF chưa cập nhật bảng benchmark hiệu năng ClickHouse theo góp ý của Hội đồng ở Chương 4 và phần từ khóa tiếng Anh còn thiếu thuật ngữ OpenTelemetry.'
                        PreviousStatus = 'Submitted'
                        ResultStatus   = 'NeedRevision'
                    }
                )
            }
        )
    },
    @{
        SubmissionId = [Guid]'c3333333-3333-3333-3333-000000000804'
        GroupId      = [Guid]'c3333333-3333-3333-3333-000000000204'
        TopicId      = [Guid]'c3333333-3333-3333-3333-000000000104'
        AdvisorId    = $vKhang
        Status       = 'AdvisorConfirmed'
        AdvisorConfirmedBy = $vKhang
        AdvisorConfirmedAt = '2026-11-15T08:15:00Z'
        Packages     = @(
            @{
                PackageId    = [Guid]'c3333333-3333-3333-3333-000000000815'
                VersionNo    = 1
                SubmittedBy  = [Guid]'c3333333-3333-3333-3333-000000000307'
                SubmittedAt  = '2026-11-14T09:00:00Z'
                AbstractVi   = 'Khóa luận xây dựng ứng dụng Android nhận diện ngôn ngữ ký hiệu tiếng Việt theo thời gian thực bằng cách kết hợp trích xuất điểm mốc cơ thể từ MediaPipe Holistic và mạng tích chập theo thời gian TCN được lượng tử hóa INT8 với TensorFlow Lite.'
                AbstractEn   = 'This thesis builds a real-time Android application for Vietnamese Sign Language recognition by combining MediaPipe Holistic landmark extraction with an INT8-quantized Temporal Convolutional Network deployed via TensorFlow Lite.'
                KeywordsVi   = 'Ngôn ngữ ký hiệu tiếng Việt, Thị giác máy tính, MediaPipe, TFLite, Thiết bị di động'
                KeywordsEn   = 'Vietnamese Sign Language, Computer Vision, MediaPipe, TFLite, Mobile AI'
                PdfFileId    = [Guid]'c3333333-3333-3333-3333-000000000919'
                PdfName      = 'KLTN-VietnameseSignLanguage-Mobile.pdf'
                PdfSize      = 4560100L
                DocxFileId   = [Guid]'c3333333-3333-3333-3333-000000000920'
                DocxName     = 'KLTN-VietnameseSignLanguage-Mobile.docx'
                DocxSize     = 2690400L
                Reviews      = @(
                    @{
                        ReviewId       = [Guid]'c3333333-3333-3333-3333-000000000853'
                        Action         = 'AdvisorConfirm'
                        Stage          = 'Advisor'
                        ActorId        = $vKhang
                        DecidedAt      = '2026-11-15T08:15:00Z'
                        Reason         = $null
                        PreviousStatus = 'Submitted'
                        ResultStatus   = 'AdvisorConfirmed'
                    }
                )
            }
        )
    },
    @{
        SubmissionId = [Guid]'c3333333-3333-3333-3333-000000000805'
        GroupId      = [Guid]'c3333333-3333-3333-3333-000000000205'
        TopicId      = [Guid]'c3333333-3333-3333-3333-000000000105'
        AdvisorId    = $vKhang
        Status       = 'ArchiveConfirmed'
        AdvisorConfirmedBy  = $vKhang
        AdvisorConfirmedAt  = '2026-11-13T10:00:00Z'
        ArchivedConfirmedBy = $vFaculty
        ArchivedConfirmedAt = '2026-11-14T15:00:00Z'
        Packages     = @(
            @{
                PackageId    = [Guid]'c3333333-3333-3333-3333-000000000816'
                VersionNo    = 1
                SubmittedBy  = [Guid]'c3333333-3333-3333-3333-000000000309'
                SubmittedAt  = '2026-11-12T11:30:00Z'
                AbstractVi   = 'Đề tài phát triển công cụ phân tích tĩnh và thực thi ký hiệu bằng ngôn ngữ Rust kết hợp bộ giải ràng buộc Z3 nhằm phát hiện các lỗ hổng bảo mật phổ biến trên bytecode hợp đồng thông minh EVM như Reentrancy, Unchecked External Call và Arithmetic Overflow.'
                AbstractEn   = 'This thesis develops a static analysis and symbolic execution tool written in Rust backed by the Z3 SMT solver to detect critical security vulnerabilities in EVM smart contract bytecode, including Reentrancy, Unchecked External Calls, and Arithmetic Overflow.'
                KeywordsVi   = 'Thực thi ký hiệu, Hợp đồng thông minh, EVM, Z3 Solver, Bảo mật Blockchain'
                KeywordsEn   = 'Symbolic Execution, Smart Contract, EVM, Z3 Solver, Blockchain Security'
                PdfFileId    = [Guid]'c3333333-3333-3333-3333-000000000921'
                PdfName      = 'KLTN-EVM-SymbolicVerification.pdf'
                PdfSize      = 3790250L
                DocxFileId   = [Guid]'c3333333-3333-3333-3333-000000000922'
                DocxName     = 'KLTN-EVM-SymbolicVerification.docx'
                DocxSize     = 2140800L
                Reviews      = @(
                    @{
                        ReviewId       = [Guid]'c3333333-3333-3333-3333-000000000854'
                        Action         = 'AdvisorConfirm'
                        Stage          = 'Advisor'
                        ActorId        = $vKhang
                        DecidedAt      = '2026-11-13T10:00:00Z'
                        Reason         = $null
                        PreviousStatus = 'Submitted'
                        ResultStatus   = 'AdvisorConfirmed'
                    },
                    @{
                        ReviewId       = [Guid]'c3333333-3333-3333-3333-000000000855'
                        Action         = 'FacultyConfirm'
                        Stage          = 'Faculty'
                        ActorId        = $vFaculty
                        DecidedAt      = '2026-11-14T15:00:00Z'
                        Reason         = $null
                        PreviousStatus = 'AdvisorConfirmed'
                        ResultStatus   = 'ArchiveConfirmed'
                    }
                )
            }
        )
    },
    @{
        SubmissionId = [Guid]'c3333333-3333-3333-3333-000000000806'
        GroupId      = [Guid]'c3333333-3333-3333-3333-000000000206'
        TopicId      = [Guid]'c3333333-3333-3333-3333-000000000106'
        AdvisorId    = $vGvA
        Status       = 'Submitted'
        Packages     = @(
            @{
                PackageId    = [Guid]'c3333333-3333-3333-3333-000000000817'
                VersionNo    = 1
                SubmittedBy  = [Guid]'c3333333-3333-3333-3333-000000000311'
                SubmittedAt  = '2026-11-15T09:40:00Z'
                AbstractVi   = 'Đề tài thiết kế mô hình bảo mật Zero-Trust cho hệ thống Microservices triển khai trên Kubernetes bằng cách tích hợp SPIFFE/SPIRE để định danh workload tự động, Istio mTLS cho mã hóa lưu lượng nội bộ và Open Policy Agent để thực thi chính sách phân quyền động.'
                AbstractEn   = 'This thesis designs a Zero-Trust security architecture for Kubernetes microservices integrating SPIFFE/SPIRE for automated workload identity, Istio mTLS for encrypted service-to-service communication, and Open Policy Agent for dynamic authorization.'
                KeywordsVi   = 'Zero-Trust, Kubernetes, Microservices, Istio, SPIFFE, Open Policy Agent'
                KeywordsEn   = 'Zero-Trust, Kubernetes, Microservices, Istio, SPIFFE, Open Policy Agent'
                PdfFileId    = [Guid]'c3333333-3333-3333-3333-000000000923'
                PdfName      = 'KLTN-ZeroTrust-Kubernetes.pdf'
                PdfSize      = 3645120L
                DocxFileId   = [Guid]'c3333333-3333-3333-3333-000000000924'
                DocxName     = 'KLTN-ZeroTrust-Kubernetes.docx'
                DocxSize     = 2095600L
                Reviews      = @()
            }
        )
    }
)

# Step 3: Build one atomic transaction SQL inserting final_submission + packages + files + links + reviews
$sb = [System.Text.StringBuilder]::new()
$null = $sb.AppendLine('BEGIN;')

foreach ($spec in $specs) {
    $gKey = $spec.GroupId.ToString()
    if (-not $minuteByGroup.ContainsKey($gKey)) {
        throw "Locked council minute metadata not found for group $gKey."
    }
    $m = $minuteByGroup[$gKey]
    $defenseSnapshotJson = [ArchivalSeedHelper]::ComputeDefenseSnapshotJson(
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
        $pdfSha  = [ArchivalSeedHelper]::Sha256Hex("demo-pdf:$($pkg.PdfFileId):$($pkg.PdfName)")
        $docxSha = [ArchivalSeedHelper]::Sha256Hex("demo-docx:$($pkg.DocxFileId):$($pkg.DocxName)")

        $fileSpecs = @(
            [SeedFileSpec]@{
                Role           = 'ThesisPdf'
                FileId         = $pkg.PdfFileId
                FileSize       = $pkg.PdfSize
                ContentType    = 'application/pdf'
                ChecksumSha256 = $pdfSha
            },
            [SeedFileSpec]@{
                Role           = 'ThesisDocx'
                FileId         = $pkg.DocxFileId
                FileSize       = $pkg.DocxSize
                ContentType    = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
                ChecksumSha256 = $docxSha
            }
        )
        $manifestSha = [ArchivalSeedHelper]::ComputeManifest(
            $pkg.AbstractVi,
            $pkg.AbstractEn,
            $pkg.KeywordsVi,
            $pkg.KeywordsEn,
            $fileSpecs
        )

        $pdfObjectKey  = "demo/archival/$($spec.GroupId)/v$($pkg.VersionNo)/$($pkg.PdfName)"
        $docxObjectKey = "demo/archival/$($spec.GroupId)/v$($pkg.VersionNo)/$($pkg.DocxName)"

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

Write-Host 'Seeded Archival Demo data cleanly!' -ForegroundColor Green
Write-Host "Dev accounts password: $demoPasswordPlain"
Write-Host ' - GVHD (5 hồ sơ đủ 4 trạng thái): gv.test.khang@ugtm.edu.vn (GV_TEST_01)'
Write-Host '   + 2x Chờ GVHD xác nhận (Submitted - có 1 hồ sơ 2 phiên bản v1->v2)'
Write-Host '   + 1x Cần bổ sung (NeedRevision)'
Write-Host '   + 1x Chờ Khoa nghiệm thu (AdvisorConfirmed)'
Write-Host '   + 1x Đã nghiệm thu (ArchiveConfirmed)'
Write-Host ' - GVHD (1 hồ sơ Submitted):         gv.nguyenvana@ugtm.edu.vn (GV0001)'
Write-Host ' - Faculty (/thesis/review/archival): truongkhoa@ugtm.edu.vn  (FACULTY01)'
Write-Host ' - Sinh viên (22520501..22520512):    22520501@gm.uit.edu.vn .. 22520512@gm.uit.edu.vn'
