# Stop hook: networking E2E coverage guard.
# If networking source (client socket layer, SocketServer, or packet defs) changed
# in the working tree but NO socket E2E / SocketServer test changed, remind to add
# or update a PlayMode E2E (or server test) for the connection behavior.
#
# Rationale: liveness/connection bugs (idle-timeout, heartbeat, disconnect handling,
# join validation) silently slipped because E2E covered only happy-path protocol flows.
# Connection handling MUST be E2E-tested. See .claude/rules/testing.md (connection coverage policy).
#
# Non-blocking, fail-safe. English messages (PowerShell 5.1 .ps1 encoding safety).

$ErrorActionPreference = 'SilentlyContinue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

if (-not (git rev-parse --show-toplevel 2>$null)) { exit 0 }

$changed = @()
$changed += git diff --name-only HEAD 2>$null
$changed += git diff --name-only --cached 2>$null
$changed += git ls-files --others --exclude-standard 2>$null
$changed = $changed | Where-Object { $_ } | Sort-Object -Unique

# Networking source (NOT tests): client socket layer / SocketServer / shared+client packet defs.
$netSrc = $changed | Where-Object {
    ( $_ -match '^Client/Assets/Script/Network/Socket/' -and $_ -match '\.cs$' -and $_ -notmatch '/Tests/' ) -or
    ( $_ -match '^ServerAll/SocketServer/SocketServer/' -and $_ -match '\.cs$' -and $_ -notmatch '/(bin|obj)/' ) -or
    ( $_ -match 'Shared\.Packet/.*\.cs$' )
}

# Connection-level test coverage: socket E2E (PlayMode) or SocketServer tests.
$netTest = $changed | Where-Object {
    ( $_ -match '^Client/Assets/Script/Tests/PlayMode/E2E/Network/Socket/' ) -or
    ( $_ -match '^Client/Assets/Script/Tests/PlayMode/Network/Socket/' ) -or
    ( $_ -match '^ServerAll/SocketServer/SocketServer\.Tests/' )
}

if ($netSrc.Count -eq 0 -or $netTest.Count -gt 0) { exit 0 }

$list = (($netSrc | Select-Object -First 8) -join ', ')
$msg = "[network-e2e guard] Networking source changed but NO socket E2E / SocketServer test was touched: $list. " +
       "Connection handling (auth/join validation, heartbeat/keep-alive, idle timeout, reconnect/grace, server-initiated disconnect, broadcast) MUST be covered by a PlayMode E2E (Client/.../Tests/PlayMode/E2E/Network/Socket) or a SocketServer test — see .claude/rules/testing.md (connection coverage policy). " +
       "Liveness gaps (e.g. the missing client heartbeat) slipped exactly because only happy-path protocol flows were E2E-tested. " +
       "Add/extend a test, or if this change genuinely needs none (pure refactor), say so before finishing."

$out = @{
    systemMessage      = '[network-e2e guard] networking source changed without a connection test'
    hookSpecificOutput = @{ hookEventName = 'Stop'; additionalContext = $msg }
} | ConvertTo-Json -Compress -Depth 6

# HOOK_DEDUP — 같은 경고를 매 Stop 마다 주입하면 에이전트가 계속 다시 깨어나 빈 응답이 반복된다.
#              내용이 바뀌지 않았으면 침묵한다(문제가 바뀌거나 새로 생기면 다시 알린다).
$stateDir = Join-Path $PSScriptRoot ".state"
if (-not (Test-Path $stateDir)) { New-Item -ItemType Directory -Force -Path $stateDir | Out-Null }
$stateFile = Join-Path $stateDir "network-e2e-guard.hash"
$md5  = [System.Security.Cryptography.MD5]::Create()
$hash = [System.BitConverter]::ToString($md5.ComputeHash([Text.Encoding]::UTF8.GetBytes($msg))).Replace("-","")
if ((Test-Path $stateFile) -and ((Get-Content $stateFile -Raw).Trim() -eq $hash)) { exit 0 }
Set-Content -Path $stateFile -Value $hash -NoNewline

Write-Output $out
exit 0
