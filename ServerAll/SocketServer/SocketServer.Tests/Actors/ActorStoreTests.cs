using Script.System.GamePlayAbilitySystem;
using Server.Actors;
using Server.Monster;

namespace Server.Tests.Actors;

/// <summary>
/// 액터·참가자 저장소 — <b>누가 존재하고 어떻게 찾는가</b>.
///
/// <para>Room 에서 떼어낸 덕에 방·세션·패킷 없이 직접 칠 수 있다. 여기서 고정하는 불변식:
/// ActorId 부호 규약으로 두 종족이 <b>한 저장소</b>에 공존한다는 것, 참가자를 지우면 액터도 함께 사라진다는 것,
/// 효과 적용 후 <b>사망 몬스터만</b> 즉시 제거된다는 것(플레이어는 부활 대상이라 남는다).</para>
/// </summary>
public class ActorStoreTests
{
    private static readonly GameplayAttributeModifier Lethal =
        new(EGameplayAttribute.Health, -9999, EModifierType.Additive);

    private static ActorStore StoreWithPlayer(long userId = 100, int maxHp = 100)
    {
        var store = new ActorStore();
        store.AddPlayer(userId, "P", 0, 0f, 0f, 0f, 0f, attackPower: 5, defense: 2, maxHp: maxHp, maxMana: 50);
        return store;
    }

    private static MonsterActor AddMonster(ActorStore store, int maxHp = 30)
    {
        var monster = new MonsterActor(store.NextMonsterInstanceId()) { MonsterId = "creepy_demon" };
        monster.Gas.DefineResource(EGameplayAttribute.Health, maxHp);
        store.Add(monster);
        return monster;
    }

    [Fact]
    public void 플레이어와_몬스터가_한_저장소에_ActorId_부호로_공존한다()
    {
        var store = StoreWithPlayer(userId: 100);
        var monster = AddMonster(store);

        Assert.Equal(ActorIds.FromPlayer(100), store.GetMember(100)!.Actor.ActorId);
        Assert.Equal(ActorIds.FromMonster(monster.InstanceId), monster.ActorId);
        Assert.True(monster.ActorId < 0 && store.GetMember(100)!.Actor.ActorId > 0);
    }

    [Fact]
    public void AddPlayer는_스탯을_GAS에_부여한다()
    {
        var store = StoreWithPlayer(maxHp: 300);
        var gas = store.GetMember(100)!.Actor.Gas;

        Assert.Equal(300, gas.Max(EGameplayAttribute.Health));
        Assert.Equal(300, gas[EGameplayAttribute.Health]); // 만피로 시작
        Assert.Equal(5, gas[EGameplayAttribute.AttackPower]);
        Assert.Equal(2, gas[EGameplayAttribute.Defense]);
    }

    [Fact]
    public void 몬스터는_Health만_보유한다()
    {
        var store = new ActorStore();
        var monster = AddMonster(store);

        Assert.True(monster.Gas.Has(EGameplayAttribute.Health));
        Assert.False(monster.Gas.Has(EGameplayAttribute.AttackPower));
        Assert.False(monster.Gas.Has(EGameplayAttribute.Mana));
    }

    [Fact]
    public void InstanceId는_1부터_순차_발급된다()
    {
        var store = new ActorStore();

        Assert.Equal(1, AddMonster(store).InstanceId);
        Assert.Equal(2, AddMonster(store).InstanceId);
        Assert.Equal(2, store.Monsters().Count);
    }

    [Fact]
    public void 참가자를_제거하면_그_액터도_함께_사라진다()
    {
        var store = StoreWithPlayer(100);
        Assert.True(store.ApplyEffect(ActorIds.FromPlayer(100), new[] { Lethal }).Applied);

        Assert.True(store.RemoveMember(100));

        Assert.Null(store.GetMember(100));
        Assert.False(store.ApplyEffect(ActorIds.FromPlayer(100), new[] { Lethal }).Applied); // 액터도 없다
        Assert.False(store.RemoveMember(100)); // 멱등
    }

    [Fact]
    public void 몬스터는_죽는_즉시_저장소에서_제거된다()
    {
        var store = new ActorStore();
        var monster = AddMonster(store);

        var (applied, newHp, died) = store.ApplyEffect(monster.ActorId, new[] { Lethal });

        Assert.True(applied);
        Assert.Equal(0, newHp);
        Assert.True(died);
        Assert.Null(store.GetMonster(monster.InstanceId));
        Assert.False(store.HasAnyMonster());
    }

    [Fact]
    public void 플레이어는_죽어도_저장소에_남는다_부활_대상()
    {
        var store = StoreWithPlayer(100);

        var (applied, newHp, died) = store.ApplyEffect(ActorIds.FromPlayer(100), new[] { Lethal });

        Assert.True(applied);
        Assert.Equal(0, newHp);
        Assert.True(died);
        Assert.NotNull(store.GetMember(100)); // 남아 있어야 부활할 수 있다
    }

    [Fact]
    public void 이미_죽은_액터에는_효과가_적용되지_않는다()
    {
        var store = StoreWithPlayer(100);
        store.ApplyEffect(ActorIds.FromPlayer(100), new[] { Lethal });

        var again = store.ApplyEffect(ActorIds.FromPlayer(100), new[] { Lethal });

        Assert.False(again.Applied);
        Assert.False(again.DiedNow); // 중복 사망 통지가 나가지 않는다
    }

    [Fact]
    public void 없는_ActorId는_적용되지_않는다()
    {
        var store = new ActorStore();

        Assert.False(store.ApplyEffect(ActorIds.FromMonster(999), new[] { Lethal }).Applied);
    }

    [Fact]
    public void 효과는_Health_이외_속성에도_적용된다()
    {
        var store = StoreWithPlayer(100); // defense 2

        store.ApplyEffect(ActorIds.FromPlayer(100), new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.Defense, -1, EModifierType.Additive),
        });

        Assert.Equal(1, store.GetMember(100)!.Actor.Gas[EGameplayAttribute.Defense]);
    }

    [Fact]
    public void HasAnyMonster는_플레이어만_있으면_false다()
    {
        var store = StoreWithPlayer(100);

        Assert.False(store.HasAnyMonster()); // 클리어 판정이 플레이어를 몬스터로 세면 안 된다

        AddMonster(store);
        Assert.True(store.HasAnyMonster());
    }

    [Fact]
    public void 동시에_같은_몬스터를_때려도_사망은_한_번만_보고된다()
    {
        var store = new ActorStore();
        var monster = AddMonster(store, maxHp: 30);

        int deaths = 0;
        Parallel.For(0, 32, _ =>
        {
            if (store.ApplyEffect(monster.ActorId, new[] { Lethal }).DiedNow)
                Interlocked.Increment(ref deaths);
        });

        Assert.Equal(1, deaths); // 드랍·클리어 판정이 중복 발화되면 안 된다
    }
}
