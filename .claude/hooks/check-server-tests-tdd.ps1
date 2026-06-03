# Stop hook: TDD guard for server changes.
# If ServerAll non-test source changed in the working tree but NO server test
# changed, inject a reminder to add unit + integration tests (test-first).
#
# Non-blocking: adds context only, never forces a loop. Once a server test file
# is touched (or the model justifies why none is needed), it stays quiet.
#
# English messages on purpose: a .ps1 run under Windows PowerShell 5.1 is read
# with the system codepage, so non-ASCII literals can be mangled. Keeping this
# guard ASCII makes it robust across PowerShell 5.1 / 7.

$ErrorActionPreference = 'SilentlyContinue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

# Only meaningful inside a git repo
if (-not (git rev-parse --show-toplevel 2>$null)) { exit 0 }

$changed = @()
$changed += git diff --name-only HEAD 2>$null            # unstaged vs HEAD
$changed += git diff --name-only --cached 2>$null         # staged
$changed += git ls-files --others --exclude-standard 2>$null  # new untracked
$changed = $changed | Where-Object { $_ } | Sort-Object -Unique

# Non-test server source (exclude bin/obj and any *.Tests project dir)
$src = $changed | Where-Object {
    $_ -match '^ServerAll/' -and $_ -match '\.cs$' -and
    $_ -notmatch '/(bin|obj)/' -and $_ -notmatch 'Tests/'
}
# Any server test file
$test = $changed | Where-Object { $_ -match '^ServerAll/.*Tests/.*\.cs$' }

if ($src.Count -eq 0 -or $test.Count -gt 0) { exit 0 }

$list = (($src | Select-Object -First 8) -join ', ')
$msg = "[server-TDD guard] Server source changed but no server test changed: $list. " +
       "TDD requires a failing test first, then implementation. Add tests now: " +
       "UNIT -> GameServer.Tests/Application/Services/*Tests.cs (fakes) or SocketServer.Tests/**. " +
       "INTEGRATION -> GameServer.Tests/Infrastructure/Integrations/*IntegrationTests.cs using the " +
       "RepositoryIntegrationTests collection + RepositoryTestFixture (Testcontainers Postgres+Redis), " +
       "or a full-flow test modeled on GameServer.Tests/E2E/GameStartE2ETest.cs (TestGameServerHost). " +
       "If tests are genuinely unnecessary for this change, state why before finishing."

$out = @{
    systemMessage      = '[server-TDD guard] server source changed without a matching server test'
    hookSpecificOutput = @{ hookEventName = 'Stop'; additionalContext = $msg }
} | ConvertTo-Json -Compress -Depth 6

Write-Output $out
exit 0
