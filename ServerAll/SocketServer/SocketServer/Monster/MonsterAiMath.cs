using Server.Actors;
using Shared.Infrastructure.Spawn;

namespace Server.Monster;

/// <summary>
/// 몬스터 1마리의 한 틱 이동/페이즈를 계산하는 **순수 함수**(시계·난수 없음 → 단위 테스트 가능).
///
/// 우선순위: 최근접 플레이어가 aggro 범위 → Chase(사거리 안이면 Attack 정지) / 아니면 Patrol(경로 순회) / 둘 다 없으면 Idle.
/// 매 틱 끝에 위치를 맵 경계로 clamp 해 몬스터가 맵을 벗어나지 못하게 한다.
/// </summary>
public static class MonsterAiMath
{
    /// <summary>패트롤 웨이포인트 도달 판정 반경(제곱).</summary>
    private const float WaypointEpsilonSq = 0.04f; // 0.2 units

    /// <summary>한 틱 진행 후 aggro 타깃(추격/공격 중인) 플레이어의 인덱스를 반환한다. 없으면 -1.
    /// 호출자(Room.Tick)가 Attack 페이즈 + 쿨다운 경과 시 이 인덱스로 플레이어를 공격한다.</summary>
    public static int Step(MonsterActor m, IReadOnlyList<TargetPos> players, MapBounds bounds, MonsterStats stats, float dt)
    {
        int nearestIdx = FindNearestIndex(m.PosX, m.PosZ, players, out float nearestDistSq);
        int aggroTarget = -1;

        if (nearestIdx >= 0 && nearestDistSq <= stats.AggroRange * stats.AggroRange)
        {
            aggroTarget = nearestIdx;
            var target = players[nearestIdx];
            float dist = MathF.Sqrt(nearestDistSq);
            if (dist <= stats.AttackRange)
            {
                m.Phase = MonsterPhase.Attack; // 정지 — 공격 발동은 RoomTickService(쿨다운)에서
                FaceTowards(m, target.X, target.Z);
            }
            else
            {
                m.Phase = MonsterPhase.Chase;
                MoveTowards(m, target.X, target.Z, stats.MoveSpeed, dt);
            }
        }
        else if (m.Patrol.Count > 0)
        {
            m.Phase = MonsterPhase.Patrol;
            var wp = m.Patrol[m.PatrolIndex % m.Patrol.Count];
            MoveTowards(m, wp.X, wp.Z, stats.MoveSpeed, dt);

            if (DistSqXZ(m.PosX, m.PosZ, wp.X, wp.Z) <= WaypointEpsilonSq)
                m.PatrolIndex = (m.PatrolIndex + 1) % m.Patrol.Count;
        }
        else
        {
            m.Phase = MonsterPhase.Idle; // 제자리 경비
        }

        // 항상: 맵 경계 안으로 clamp (무경계면 무동작).
        var (cx, cz) = bounds.Clamp(m.PosX, m.PosZ);
        m.PosX = cx;
        m.PosZ = cz;
        return aggroTarget;
    }

    private static int FindNearestIndex(float x, float z, IReadOnlyList<TargetPos> players, out float bestSq)
    {
        bestSq = float.MaxValue;
        int best = -1;
        for (int i = 0; i < players.Count; i++)
        {
            float d = DistSqXZ(x, z, players[i].X, players[i].Z);
            if (d < bestSq)
            {
                bestSq = d;
                best = i;
            }
        }
        return best;
    }

    private static void MoveTowards(MonsterActor m, float tx, float tz, float speed, float dt)
    {
        float dx = tx - m.PosX;
        float dz = tz - m.PosZ;
        float dist = MathF.Sqrt(dx * dx + dz * dz);
        if (dist <= 1e-4f) return;

        float step = speed * dt;
        if (step >= dist)
        {
            m.PosX = tx;
            m.PosZ = tz;
        }
        else
        {
            m.PosX += dx / dist * step;
            m.PosZ += dz / dist * step;
        }
        m.RotY = DirToYaw(dx, dz);
    }

    private static void FaceTowards(MonsterActor m, float tx, float tz)
    {
        float dx = tx - m.PosX;
        float dz = tz - m.PosZ;
        if (dx * dx + dz * dz > 1e-6f)
            m.RotY = DirToYaw(dx, dz);
    }

    /// <summary>방향(dx,dz) → Unity Y축 회전 각도(0=+Z, 90=+X).</summary>
    private static float DirToYaw(float dx, float dz) => MathF.Atan2(dx, dz) * (180f / MathF.PI);

    private static float DistSqXZ(float ax, float az, float bx, float bz)
    {
        float dx = ax - bx;
        float dz = az - bz;
        return dx * dx + dz * dz;
    }
}
