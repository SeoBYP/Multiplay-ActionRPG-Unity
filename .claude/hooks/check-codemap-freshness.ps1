# Stop hook: codemap freshness guard.
# If server/client FEATURE source changed in the working tree but
# docs/wiki/codemap.md was NOT updated, remind to record location + rationale
# there (so the next session reads the map instead of re-deriving = token waste).
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

# Feature source = server (.cs, not test/bin/obj) OR client script (.cs, not test/Generated)
$feature = $changed | Where-Object {
    ( $_ -match '^ServerAll/' -and $_ -match '\.cs$' -and $_ -notmatch '/(bin|obj)/' -and $_ -notmatch 'Tests/' ) -or
    ( $_ -match '^Client/Assets/Script/' -and $_ -match '\.cs$' -and $_ -notmatch '/Generated/' -and $_ -notmatch '/Tests/' )
}
$mapTouched = $changed | Where-Object { $_ -match 'docs/wiki/codemap\.md$' }

if ($feature.Count -eq 0 -or $mapTouched.Count -gt 0) { exit 0 }

$list = (($feature | Select-Object -First 8) -join ', ')
$msg = "[codemap guard] Feature source changed but docs/wiki/codemap.md was not updated: $list. " +
       "Record location + rationale there (domain index and/or a decision-log entry) so the next session reads the map instead of re-reading code to re-derive 'where' and 'why' (token waste). " +
       "If this is a top-level/canonical-path change, also update auto-memory MEMORY.md. " +
       "If no map change is warranted (trivial edit), say so before finishing."

$out = @{
    systemMessage      = '[codemap guard] feature source changed without a codemap update'
    hookSpecificOutput = @{ hookEventName = 'Stop'; additionalContext = $msg }
} | ConvertTo-Json -Compress -Depth 6

Write-Output $out
exit 0
