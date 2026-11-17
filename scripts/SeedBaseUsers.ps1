[CmdletBinding()]
param(
    [string]$Password = 'UgtmDemo@2026',
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
    if ($line -match '^\s*DATABASE_CONNECTION_STRING\s*=(.*)$') {
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

$env:PGHOST = if ($pgParams.ContainsKey('host')) { $pgParams['host'] } elseif ($pgParams.ContainsKey('server')) { $pgParams['server'] } else { 'localhost' }
$env:PGPORT = if ($pgParams.ContainsKey('port')) { $pgParams['port'] } else { '5432' }
$env:PGDATABASE = $pgParams['database']
$env:PGUSER = if ($pgParams.ContainsKey('username')) { $pgParams['username'] } else { $pgParams['user id'] }
$env:PGPASSWORD = $pgParams['password']
if ($pgParams.ContainsKey('ssl mode') -and $pgParams['ssl mode'] -match 'require|verify') {
    $env:PGSSLMODE = 'require'
}
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
    $tmp = [IO.Path]::Combine([IO.Path]::GetTempPath(), "ugtm_seed_base_users_$([Guid]::NewGuid().ToString('N')).sql")
    try {
        [IO.File]::WriteAllText($tmp, $SqlText, [System.Text.UTF8Encoding]::new($false))
        $prevEncoding = [Console]::OutputEncoding
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        try {
            $output = & $psql -v ON_ERROR_STOP=1 -f $tmp 2>&1
            if ($LASTEXITCODE -ne 0) {
                throw "psql failed (exit $LASTEXITCODE):`n$($output -join "`n")"
            }
            return $output
        }
        finally {
            [Console]::OutputEncoding = $prevEncoding
        }
    }
    finally {
        if (Test-Path -LiteralPath $tmp) {
            Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
        }
    }
}

if ($Reset) {
    $resetSql = @'
BEGIN;
DELETE FROM identity.user_role
WHERE user_id::text LIKE 'e0000000-0000-0000-0000-0000000000%';
DELETE FROM identity."user"
WHERE id::text LIKE 'e0000000-0000-0000-0000-0000000000%';
COMMIT;
'@
    Invoke-PsqlFile $resetSql | Out-Null
    Write-Host 'Reset completed: removed base sample users (e0000000-0000-0000-0000-0000000000*).' -ForegroundColor Yellow
    return
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
    [Array]::Copy($prfBytes, 0, $output, 1, 4)
    [Array]::Copy($iterBytes, 0, $output, 5, 4)
    [Array]::Copy($saltLenBytes, 0, $output, 9, 4)
    [Array]::Copy($salt, 0, $output, 13, $salt.Length)
    [Array]::Copy($subkey, 0, $output, 13 + $salt.Length, $subkey.Length)
    return [Convert]::ToBase64String($output)
}

$passwordHash = Get-AspNetIdentityV3Hash $Password

$seedSql = @'
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
            ('e0000000-0000-0000-0000-000000000004'::uuid, 'GV_BASE_01', 'Giảng viên mẫu',            'lecturer@ugtm.edu.vn',   'Lecturer'),
            ('e0000000-0000-0000-0000-000000000005'::uuid, 'SV_BASE_01', 'Sinh viên mẫu',             'student@ugtm.edu.vn',    'Student'),
            ('e0000000-0000-0000-0000-000000000006'::uuid, 'HD_BASE_01', 'Thành viên Hội đồng mẫu',   'council@ugtm.edu.vn',    'CouncilMember')
        ) AS t(default_id, user_code, full_name, email, role_name)
    LOOP
        SELECT id INTO v_role_id
        FROM identity.role
        WHERE role_name = v_item.role_name
        LIMIT 1;

        IF v_role_id IS NULL THEN
            RAISE EXCEPTION 'Missing role % in identity.role', v_item.role_name;
        END IF;

        INSERT INTO identity."user" (
            id, user_code, full_name, email, normalized_email, password_hash,
            status, security_stamp, concurrency_stamp, access_failed_count,
            lockout_end_at, created_at, updated_at, activated_at, email_verified_at, password_changed_at
        )
        VALUES (
            v_item.default_id,
            v_item.user_code,
            v_item.full_name,
            v_item.email,
            UPPER(v_item.email),
            v_pwd_hash,
            'Active',
            gen_random_uuid(),
            gen_random_uuid(),
            0,
            NULL,
            NOW(),
            NOW(),
            NOW(),
            NOW(),
            NOW()
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
        WHERE user_id = v_user_id
          AND role_id <> v_role_id;

        INSERT INTO identity.user_role (user_id, role_id, assigned_at)
        SELECT v_user_id, v_role_id, NOW()
        WHERE NOT EXISTS (
            SELECT 1 FROM identity.user_role
            WHERE user_id = v_user_id AND role_id = v_role_id
        );
    END LOOP;
END $$;

COMMIT;

SELECT u.user_code, u.full_name, u.email, u.status, COALESCE(string_agg(r.role_name, ', ' ORDER BY r.role_name), '') AS roles
FROM identity."user" u
LEFT JOIN identity.user_role ur ON ur.user_id = u.id
LEFT JOIN identity.role r ON r.id = ur.role_id
WHERE u.normalized_email IN (
    'ADMIN@UGTM.EDU.VN',
    'TRUONGKHOA@UGTM.EDU.VN',
    'PDT@UGTM.EDU.VN',
    'LECTURER@UGTM.EDU.VN',
    'STUDENT@UGTM.EDU.VN',
    'COUNCIL@UGTM.EDU.VN'
)
GROUP BY u.user_code, u.full_name, u.email, u.status
ORDER BY roles;
'@

$seedSql = $seedSql.Replace('__PWD_HASH__', $passwordHash)
$result = Invoke-PsqlFile $seedSql
$result
Write-Host "Seeded 6 base role users successfully (password: $Password)." -ForegroundColor Green
