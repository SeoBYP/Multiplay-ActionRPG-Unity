# PreToolUse guard: before a PlayMode E2E run (run_tests), if the running Docker
# server image is OLDER than its corresponding ServerAll source, inject a
# "redeploy first" warning into the model context.
#
# Why: the dungeon-leave regression was rooted in "server code fixed but the
# container image was stale". Unit tests were green while E2E hit the OLD server
# and produced a false result. This guard catches that blind spot automatically.
#
# Non-blocking: emits systemMessage + additionalContext only (no hard block), to
# avoid false positives from mtime skew. To hard-block, add permissionDecision='ask'.
#
# Messages are ASCII/English on purpose: a .ps1 file run under Windows PowerShell 5.1
# is read with the system codepage, so non-ASCII literals can be mangled. English
# keeps the guard robust across PowerShell 5.1 / 7.

$ErrorActionPreference = 'SilentlyContinue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

# 1. Parse hook stdin JSON
$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }
try { $data = $raw | ConvertFrom-Json } catch { exit 0 }

# 2. Only guard PlayMode (= Docker E2E). EditMode unit tests are unaffected.
if ($data.tool_input.mode -ne 'PlayMode') { exit 0 }

$serverDir = Join-Path $PSScriptRoot '..\..\ServerAll'
if (-not (Test-Path $serverDir)) { exit 0 }

# Newest source mtime under one or more ServerAll subtrees (bin/obj excluded).
function Get-NewestSrcUtc([string[]]$subdirs) {
    $files = foreach ($d in $subdirs) {
        $p = Join-Path $serverDir $d
        if (Test-Path $p) {
            Get-ChildItem -Path $p -Recurse -File -Include *.cs, *.csproj, *.proto |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
        }
    }
    if (-not $files) { return $null }
    ($files | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
}

# 3. Map each image to the source subtrees that affect it (Shared affects both).
$pairs = @(
    @{ Img = 'infra-gameserver';   Dirs = @('GameServer', 'Shared') }
    @{ Img = 'infra-socketserver'; Dirs = @('SocketServer', 'Shared') }
)

$stale = @()
foreach ($p in $pairs) {
    $newest = Get-NewestSrcUtc $p.Dirs
    if (-not $newest) { continue }

    $created = docker image inspect $p.Img --format '{{.Created}}' 2>$null
    if (-not $created) { continue }
    try { $createdUtc = ([datetime]$created).ToUniversalTime() } catch { continue }

    if ($newest -gt $createdUtc) {
        $stale += ("{0} (image built {1:yyyy-MM-dd HH:mm}Z, src changed {2:yyyy-MM-dd HH:mm}Z)" -f $p.Img, $createdUtc, $newest)
    }
}

if ($stale.Count -eq 0) { exit 0 }

# 4. Warn + inject model context
$msg = "[stale-image guard] Running server image is OLDER than its source: " +
       ($stale -join '; ') +
       ". Rebuild/redeploy before PlayMode E2E: 'cd ServerAll/Infra && docker compose build <svc> && docker compose up -d <svc>' (gameserver and/or socketserver). Otherwise E2E tests the OLD server and gives false results."

$out = @{
    systemMessage      = $msg
    hookSpecificOutput = @{
        hookEventName     = 'PreToolUse'
        additionalContext = $msg
    }
} | ConvertTo-Json -Compress -Depth 6

Write-Output $out
exit 0
