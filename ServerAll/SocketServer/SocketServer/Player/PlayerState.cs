using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;

namespace Server.Player;

public class PlayerState
{
    // 스킬별 마지막 발동 시각(Unix ms). 서버 권위 쿨다운 게이트.
    private readonly Dictionary<int, long> _lastSkillCastMs = new();

    public long UserId { get; set; }

    public string Nickname { get; set; } = "";

    /// <summary>
    /// 서버 권위 HP(던전 코옵). 서버가 발행하는 데미지/회복을 자기도 누적해 사망을 직접 감지한다
    /// (클라 미보고 시 불사 핵 차단). 클라는 같은 효과를 즉발 적용(예측), 서버 값이 진실.
    /// authority-model §4(2026-06-11 서버 권위 승격). 0 이하면 다운.
    /// </summary>
    public int Hp { get; set; }
    public int MaxHp { get; set; }

    /// <summary>
    /// 합산 전투 스탯(서버 권위). GameServer 가 게임시작 메시지로 채워 보낸 값(authority-model §4c) —
    /// SocketServer 는 계산 안 하고 받아서 데미지 산식 입력으로만 쓴다. 0 = 미설정(스탯 보너스 없음).
    /// </summary>
    public int AttackPower { get; set; }
    public int Defense { get; set; }

    public bool IsDowned => Hp <= 0;
    
    public float PosX { get; set; }
    
    public float PosY { get; set; }
    
    public float PosZ { get; set; }
    public float RotY { get; set; }

    /// <summary>게임 시작 시 배정된 스폰 슬롯 인덱스. 클라 결정론 스폰 입력으로 전달된다.</summary>
    public int SpawnIndex { get; set; }

    public long LastMovedAt { get; set; }

    /// <summary>
    /// 크래시/네트워크 끊김으로 세션이 사라진 시각(Unix ms). null = 접속 중.
    /// 재접속 유예 창(<see cref="Server.Room.Room.ReconnectGraceMs"/>) 판정에 사용:
    /// 끊김 시 상태를 즉시 지우지 않고 이 값을 찍어 보존(재접속하면 보존 상태로 즉시 복귀),
    /// 유예 만료 시 RoomTickService 스윕이 정리한다. 끊긴 동안 몬스터 AI 타깃에선 제외된다.
    /// </summary>
    public long? DisconnectedAtMs { get; set; }

    /// <summary>
    /// 소켓 세션이 실제로 입장(C_PlayerJoin 성공)했는가. false = GameStart 가 PlayerState 만 미리
    /// 초기화하고 아직 소켓 미입장(로딩 중)인 상태. 몬스터 AI 타깃에서 제외한다(TickMonsters) —
    /// 아직 들어오지도 않은 플레이어가 맞아 죽으면 S_PlayerDead 가 빈 방에 발행돼 유실되기 때문.
    /// <see cref="Server.Room.Room.MarkJoined"/> 가 입장/재접속 시 true 로 세팅한다.
    /// </summary>
    public bool HasJoined { get; set; }

    /// <summary>
    /// 스킬 발동 게이트(서버 권위 쿨다운). 쿨다운이 지났으면 마지막 발동 시각을 기록하고 true,
    /// 아직이면 false(발동 거부 → 데미지 0). C_Attack 연사=폭딜 치팅을 서버에서 차단한다.
    /// </summary>
    public bool TryBeginSkill(int skillId, int cooldownMs, long nowMs)
    {
        if (_lastSkillCastMs.TryGetValue(skillId, out var last)
            && !SkillTimelineMath.CooldownElapsed(cooldownMs, last, nowMs))
            return false;

        _lastSkillCastMs[skillId] = nowMs;
        return true;
    }
}