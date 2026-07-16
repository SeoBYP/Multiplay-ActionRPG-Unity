using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

using Server.PacketHandler.Handler;

namespace Server.Tests.Monster;

/// <summary>
/// AC-B B4: 몬스터 어빌리티 선택. 저작 순서(MonsterDefinition.abilityIds) = 우선순위 →
/// **사거리 안 + 쿨다운 경과인 첫 어빌리티**를 발동한다. 쿨다운은 어빌리티 단위로 추적(보스 다중 스킬 전제).
/// 설계 = ability-so-authoring.md §5.
/// </summary>
public class MonsterAbilitySelectionTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

    private static void Spawn(global::Server.Room.Room room, string monsterId)
        => room.SpawnMonsters(
            new List<MonsterSpawnDef> { new(monsterId, 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 400f, 400f));

    [Fact]
    public void 사거리_밖이면_해당_어빌리티는_발동하지_않는다()
    {
        // creepy_demon_attack 의 activationRange=1.3. aggro(7)엔 들지만 사거리 밖인 거리에 두면
        // 추격(Chase)만 하고 발동은 없어야 한다 → 사거리 판정이 **어빌리티 데이터**로 이뤄짐을 고정.
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 3f, 0f, 0f, 0f); // aggro 안(7) · 공격 사거리 밖(1.3)
        room.MarkJoined(100);
        Spawn(room, "creepy_demon");

        var packets = room.TickMonsters(0.1f, 1_000_000);

        Assert.Empty(packets.OfType<S_AbilityActivated>());
        Assert.Empty(packets.OfType<S_ApplyEffect>());
    }

    [Fact]
    public void 쿨다운은_어빌리티_데이터의_값을_따른다()
    {
        // creepy_demon_attack cooldownMs=1400(어빌리티 저작값). MonsterDef 엔 더 이상 쿨다운이 없다.
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f); // 사거리 안
        room.MarkJoined(100);
        Spawn(room, "creepy_demon");

        int cd = AbilityCatalog.Get("creepy_demon_attack")!.Timeline.CooldownMs;
        const long t0 = 1_000_000;

        Assert.Single(room.TickMonsters(0.1f, t0).OfType<S_AbilityActivated>());              // 첫 발동
        Assert.Empty(room.TickMonsters(0.1f, t0 + cd - 1).OfType<S_AbilityActivated>());      // 쿨다운 1ms 전 → 거부
        Assert.Single(room.TickMonsters(0.1f, t0 + cd).OfType<S_AbilityActivated>());         // 경계 = 발동
    }

    [Fact]
    public void 발동한_어빌리티의_networkId가_신호에_실린다()
    {
        // 클라 라우터가 이 networkId 로 Cue(애니 트리거)를 조회한다 → 몬스터별 연출 분기의 근거.
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f);
        room.MarkJoined(100);
        Spawn(room, "arachnya");

        var act = room.TickMonsters(0.1f, 1_000_000).OfType<S_AbilityActivated>().Single();

        Assert.Equal(AbilityCatalog.Get("arachnya_attack")!.NetworkId, act.SkillId);
        Assert.Equal(ActorIds.FromMonster(1), act.ActorId);
    }

    [Fact]
    public void 데미지와_CC는_어빌리티_저작값을_따른다()
    {
        // arachnya_attack: baseDamage=14, onHitEffectIds=[slow_3s]. MonsterDef 엔 둘 다 없다(어빌리티 소유).
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f, attackPower: 0, defense: 0);
        room.MarkJoined(100);
        Spawn(room, "arachnya");

        var effects = room.TickMonsters(0.1f, 1_000_000).OfType<S_ApplyEffect>().ToList();
        var ability = AbilityCatalog.Get("arachnya_attack")!;

        var dmg = effects.Single(e => e.EffectId == CombatHandler.AbilityDamageEffectId);
        Assert.Equal(-ability.BaseDamage, dmg.Amount); // Defense 0 → base 그대로

        var cc = effects.Single(e => e.EffectId == "slow_3s");
        Assert.Equal(0, cc.Amount); // CC = 상태태그(HP 변경 없음)
    }

    [Fact]
    public void 어빌리티가_없는_몬스터는_공격하지_않는다()
    {
        // 미등록 monsterId → Shared Default(abilityIds 비어있음) → AttackRange 0 → 발동 없음.
        // 저작 누락이 서버 크래시가 아니라 "공격 안 함" 으로 안전하게 degrade 되는지 고정.
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f);
        room.MarkJoined(100);
        Spawn(room, "no_such_monster");

        var packets = room.TickMonsters(0.1f, 1_000_000);

        Assert.Empty(packets.OfType<S_AbilityActivated>());
        Assert.Empty(packets.OfType<S_ApplyEffect>());
        Assert.NotEmpty(packets.OfType<S_MonsterState>()); // 존재는 하고 이동/상태는 정상
    }
}
