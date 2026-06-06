<#
.SYNOPSIS
    개발용 데이터 리셋 — Docker 서버의 PostgreSQL + Redis 누적 테스트 데이터를 정리한다.

.DESCRIPTION
    E2E / MPPM 테스트는 방·세션·계정을 만들고 teardown에서 정리하지 않아 실행할수록 누적된다
    (로비에 임시 방 목록이 쌓이는 원인). 이 스크립트가 한 번에 정리한다.

    기본(전체 클린 슬레이트): 방·세션·로비 churn + 계정 + 채팅 전부 삭제 + Redis FLUSHDB.
    -KeepAccounts: 계정(users/credentials/profiles/user_sessions)과 유저 인증 Redis 키는 보존,
                   방·세션·로비 churn만 삭제. (로그인 유지한 채 로비만 비우고 싶을 때)

    안전장치: 외래키가 앱 레벨이라 캐스케이드 위험 없음. 컨테이너 env에서 DB 접속정보를 읽어
              docker-compose와 항상 일치. 운영 환경에서는 절대 쓰지 말 것(컨테이너명이 dev 전용).

.EXAMPLE
    pwsh ServerAll/Infra/reset-dev-data.ps1
    pwsh ServerAll/Infra/reset-dev-data.ps1 -KeepAccounts
#>
[CmdletBinding()]
param(
    [switch]$KeepAccounts,
    [string]$PgContainer    = "gameserver-postgres",
    [string]$RedisContainer = "gameserver-redis"
)

$ErrorActionPreference = "Stop"

function Invoke-Psql([string]$sql) {
    docker exec -e "PGPASSWORD=$pgPass" $PgContainer psql -U $pgUser -d $pgDb -v ON_ERROR_STOP=1 -c $sql
}

# ── DB 접속정보를 컨테이너 env에서 읽는다 (docker-compose와 항상 일치) ──
$env = docker inspect $PgContainer --format '{{range .Config.Env}}{{println .}}{{end}}'
$pgDb   = ($env | Select-String '^POSTGRES_DB=')       -replace 'POSTGRES_DB=', ''       | ForEach-Object Trim
$pgUser = ($env | Select-String '^POSTGRES_USER=')     -replace 'POSTGRES_USER=', ''     | ForEach-Object Trim
$pgPass = ($env | Select-String '^POSTGRES_PASSWORD=') -replace 'POSTGRES_PASSWORD=', '' | ForEach-Object Trim

if (-not $pgDb) { throw "PostgreSQL 컨테이너($PgContainer) env를 못 읽었습니다. 컨테이너가 떠 있는지 확인하세요." }

Write-Host "[reset-dev-data] target: pg=$PgContainer/$pgDb redis=$RedisContainer  KeepAccounts=$KeepAccounts"

# ── 1. 방 / 세션 / 로비 churn (항상 삭제) ──────────────────────────
Write-Host "[1/3] 방·세션·로비 데이터 삭제..."
Invoke-Psql @"
BEGIN;
DELETE FROM game_session_players;
DELETE FROM game_sessions;
DELETE FROM dungeon_room_players;
DELETE FROM dungeon_rooms;
DELETE FROM outbox_messages;
COMMIT;
"@

# ── 2. 계정 + 채팅 (기본 삭제, -KeepAccounts 시 보존) ──────────────
if (-not $KeepAccounts) {
    Write-Host "[2/3] 계정·채팅 삭제 (전체 클린 슬레이트)..."
    Invoke-Psql @"
BEGIN;
DELETE FROM user_sessions;
DELETE FROM user_profiles;
DELETE FROM user_credentials;
DELETE FROM chat_messages;
DELETE FROM users;
COMMIT;
"@
} else {
    Write-Host "[2/3] -KeepAccounts: 계정 보존 (방·세션만 정리)."
}

# ── 3. Redis ──────────────────────────────────────────────────────
if (-not $KeepAccounts) {
    Write-Host "[3/3] Redis FLUSHDB (전체 비움 — 스트림/그룹은 서버가 재생성)..."
    docker exec $RedisContainer redis-cli FLUSHDB | Out-Null
} else {
    Write-Host "[3/3] Redis: 방/게임세션 키만 삭제 (유저 인증 세션 보존)..."
    foreach ($p in 'game:room:*', 'game:gamesession:*', 'game:session:player:*', 'stream:room:*') {
        docker exec $RedisContainer sh -c "redis-cli --scan --pattern '$p' | while read k; do redis-cli UNLINK `"`$k`" >/dev/null; done"
    }
}

Write-Host "[reset-dev-data] 완료. 로비를 새로고침하면 방 목록이 비어 있습니다." -ForegroundColor Green
