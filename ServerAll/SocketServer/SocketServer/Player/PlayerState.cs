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
    /// 서버 권위 마나(던전 코옵). 스킬/회피 발동 코스트를 여기서 검증·차감해 클라 위조(무한 시전)를 차단한다.
    /// 클라는 같은 코스트로 즉시 차감(예측)하고, 서버가 S_PlayerMana 로 차감/거부를 owner 에게 정정한다.
    /// 자연 회복은 RoomTickService 가 <see cref="RegenMana"/> 로 매 틱 진행(클라는 동일 rate 예측 → 수렴).
    /// </summary>
    public int Mana { get; set; }
    public int MaxMana { get; set; }

    // 리젠 소수부 누적(서버 틱 dt 비례 회복을 정수 Mana 로 환산). 클라 _manaRegenAccum 과 동일 방식.
    private double _manaRegenAccum;

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

    /// <summary>
    /// 마나 차감(권위). 코스트 이상 보유 시 차감하고 true, 부족하면 변경 없이 false.
    /// cost &lt;= 0 은 무료 스킬(항상 true). 호출부(CombatHandler)가 false 면 발동을 거부한다.
    /// </summary>
    public bool TrySpendMana(int cost)
    {
        if (cost <= 0)
            return true;
        if (Mana < cost)
            return false;
        Mana -= cost;
        return true;
    }

    /// <summary>
    /// 시간 비례 마나 자연 회복(서버 권위). <see cref="ManaConfig.RegenPerSecond"/> 를 dt 만큼 누적해
    /// 정수 단위로 Mana 에 더하고 MaxMana 로 클램프한다. 클라 예측과 같은 rate → 만피에서 수렴.
    /// 동기화 패킷은 보내지 않는다(리젠은 클라가 동일 예측).
    /// </summary>
    public void RegenMana(float dt)
    {
        if (MaxMana <= 0 || Mana >= MaxMana)
        {
            _manaRegenAccum = 0;
            return;
        }

        _manaRegenAccum += ManaConfig.RegenPerSecond * dt;
        int whole = (int)_manaRegenAccum;
        if (whole <= 0)
            return;

        _manaRegenAccum -= whole;
        Mana = System.Math.Min(MaxMana, Mana + whole);
    }

    // 회피(Dodge) 무적 — 이 시각(Unix ms)까지 피해를 무시한다. 0 = 무적 아님.
    public long InvulnerableUntilMs { get; set; }

    // 회피 쿨다운 게이트용 — 마지막 회피 발동 시각(Unix ms). 0 = 미발동.
    private long _lastDodgeMs;

    /// <summary>
    /// 회피 발동 게이트(서버 권위 쿨다운 + 마나). 쿨다운(<see cref="DodgeConfig.CooldownMs"/>)이 지났고
    /// 마나가 코스트 이상이면 마나를 차감하고 무적 창(<see cref="DodgeConfig.IframeMs"/>)을 부여하고 true.
    /// 쿨다운/마나 어느 하나라도 모자라면 <b>아무것도 소모하지 않고</b> false(원자적).
    /// C_Dodge 연사로 영구 무적/무한 회피를 만드는 치팅을 서버가 차단한다.
    /// </summary>
    public bool TryBeginDodge(long nowMs, int manaCost = 0)
    {
        if (_lastDodgeMs != 0 && nowMs - _lastDodgeMs < DodgeConfig.CooldownMs)
            return false;
        if (Mana < manaCost) // 마나 부족 → 쿨다운/무적 미소모 거부
            return false;

        if (manaCost > 0)
            Mana -= manaCost;
        _lastDodgeMs = nowMs;
        InvulnerableUntilMs = nowMs + DodgeConfig.IframeMs;
        return true;
    }

    /// <summary>주어진 시각에 회피 무적(i-frame)인가.</summary>
    public bool IsInvulnerableAt(long nowMs) => nowMs < InvulnerableUntilMs;

    // ── 콤보 cadence(서버 권위, 데이터 주도) ──────────────────────────────
    // 직전 콤보 스윙의 시각 + 그 스킬의 SkillTimeline.ComboChainMs 를 기억해, 다음 콤보 공격이
    // 그 시점 전에 오면 거부한다. 콤보는 단계마다 skillId 가 달라(2/3/4) **개별 쿨다운으론 연타를 못 막는다**
    // (각자 첫 발동이라 쿨다운이 비어 있음 → C_Attack{2,3,4} 즉시 연사 = 합산 폭딜).
    // 타이밍의 진실원은 skills.json(SO 저작) — 클라 ComboDriver 가 쓰는 값과 동일하다.
    private long _lastComboAtMs;
    private int _lastComboChainMs;

    /// <summary>
    /// 콤보 공격 cadence 게이트. 직전 콤보 스윙의 <c>ComboChainMs</c> 가 지났으면 true(그리고 이번 스윙을 기록).
    /// 아직이면 <b>아무것도 기록하지 않고</b> false. chainMs=0(데이터 미설정)이면 최소 안전값으로 폴백한다.
    ///
    /// <paramref name="toleranceMs"/> = 네트워크 지터 허용치. 클라는 정확히 ComboChainMs 간격으로 보내지만
    /// 패킷별 지연이 달라 <b>서버 도착 간격이 그보다 짧아질 수 있다</b> → 허용치가 없으면 정상 콤보가 거부돼
    /// 데미지가 유실된다. 허용치만큼 느슨하게 봐도 버스트(즉시 3연타) 차단에는 지장이 없다.
    /// </summary>
    public bool TryBeginComboAttack(long nowMs, int thisChainMs, int minFallbackMs, int toleranceMs = 0)
    {
        if (_lastComboChainMs > 0)
        {
            long required = Math.Max(0, _lastComboChainMs - toleranceMs);
            if (nowMs - _lastComboAtMs < required)
                return false;
        }

        _lastComboAtMs = nowMs;
        // 저작 실수로 0 이어도 버스트 구멍이 열리지 않도록 최소값 보장.
        _lastComboChainMs = thisChainMs > 0 ? thisChainMs : minFallbackMs;
        return true;
    }
}