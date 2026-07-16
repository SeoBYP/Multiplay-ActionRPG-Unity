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
# Known residual limitation: the freshness signal is "newest source mtime" vs "image .Created".
# If a source edit produces byte-identical compiled output (comment/whitespace-only change),
# Docker reuses the existing image ID and .Created does NOT advance, so the warning persists
# even though you did rebuild. Rebuilding once and confirming the image digest is unchanged is
# enough to dismiss it. Fixing this properly would mean stamping a source hash into the image
# at build time (Dockerfile LABEL + build arg) -- not worth the build-cache cost today.
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

# Transitive ProjectReference closure of an entry .csproj -> the project directories whose
# sources actually end up compiled into that image.
#
# Why derive this instead of listing dirs by hand: the old version mapped gameserver to
# @('GameServer','Shared'), but ServerAll/Shared holds Shared.Packet, which ONLY SocketServer
# references. Editing Shared.Packet therefore flagged gameserver forever: the Dockerfile does
# 'COPY Shared/ Shared/' so the layer cache is busted and a rebuild DOES run, but the compiled
# output is byte-identical, so Docker keeps the existing image ID and .Created never advances
# -> a warning that can never be cleared -> alarm fatigue -> the guard gets ignored, which is
# exactly how a genuinely stale image slips through.
#
# A hardcoded list also rots the dangerous way: add a reference later and the guard goes
# FALSE NEGATIVE (silently misses a stale image). The csproj graph is the same source of
# truth the compiler uses, so it cannot drift.
function Get-ProjectDirs([string]$entryRelPath) {
    $entry = Resolve-Path -LiteralPath (Join-Path $serverDir $entryRelPath) -ErrorAction SilentlyContinue
    if (-not $entry) { return @() }

    $seen  = New-Object 'System.Collections.Generic.HashSet[string]'
    $queue = New-Object 'System.Collections.Generic.Queue[string]'
    $queue.Enqueue($entry.Path)

    while ($queue.Count -gt 0) {
        $cur = $queue.Dequeue()
        if (-not $seen.Add($cur)) { continue }
        $text = Get-Content -Raw -LiteralPath $cur -ErrorAction SilentlyContinue
        if (-not $text) { continue }
        foreach ($m in [regex]::Matches($text, 'ProjectReference\s+Include\s*=\s*"([^"]+)"')) {
            $rel = $m.Groups[1].Value.Replace('\', [IO.Path]::DirectorySeparatorChar)
            $ref = Resolve-Path -LiteralPath (Join-Path (Split-Path $cur -Parent) $rel) -ErrorAction SilentlyContinue
            if ($ref) { $queue.Enqueue($ref.Path) }
        }
    }
    return @($seen | ForEach-Object { Split-Path $_ -Parent })
}

# Newest source mtime under the given project dirs (bin/obj excluded).
# *.json is included on purpose: embedded catalogs (e.g. Shared.Infrastructure/Abilities/abilities.json,
# appsettings) change server behaviour, so a json-only edit with no rebuild is a genuinely stale image.
function Get-NewestSrcUtc([string[]]$dirs) {
    $files = foreach ($p in $dirs) {
        if (Test-Path $p) {
            Get-ChildItem -Path $p -Recurse -File -Include *.cs, *.csproj, *.proto, *.json |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
        }
    }
    if (-not $files) { return $null }
    ($files | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
}

# 3. Each image <- the entry project Docker publishes (see the matching Dockerfile).
$pairs = @(
    @{ Img = 'infra-gameserver';   Entry = 'GameServer/GameServer.API/GameServer.API.csproj' }
    @{ Img = 'infra-socketserver'; Entry = 'SocketServer/SocketServer/SocketServer.csproj' }
)

$stale = @()
foreach ($p in $pairs) {
    $dirs = Get-ProjectDirs $p.Entry
    if (-not $dirs) { continue }
    $newest = Get-NewestSrcUtc $dirs
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
