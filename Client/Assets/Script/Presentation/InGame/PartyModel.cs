using System;
using System.Collections.Generic;
using Game.Network.Socket;
using Game.System.Auth;
using Game.System.Player;
using Script.System.GamePlayAbilitySystem;
using VContainer.Unity;

namespace Game.Presentation.InGame
{
    /// <summary>던전 파티 HUD 한 줄. HP 진실원 = 서버 권위(S_ApplyEffect→ASC). 원격 MaxHp 는 prefab 기본값(근사).</summary>
    public readonly struct PartyMemberInfo
    {
        public readonly long UserId;
        public readonly string Nickname;
        public readonly int Hp;
        public readonly int MaxHp;
        public readonly bool IsLocal;

        public PartyMemberInfo(long userId, string nickname, int hp, int maxHp, bool isLocal)
        {
            UserId = userId; Nickname = nickname; Hp = hp; MaxHp = maxHp; IsLocal = isLocal;
        }
    }

    /// <summary>
    /// 파티 HP HUD 의 Model — <see cref="PartyAscRegistry"/>(로컬+원격 ASC) + 로스터(닉네임) + 각 ASC Health 를
    /// 집계해 파티원 리스트로 노출한다. 구성/HP 변경 시 <see cref="Changed"/> 발행 → View(PartyHpPanel)가 재렌더.
    /// 신규 패킷 없이 기존 GAS(S_ApplyEffect 방 브로드캐스트)만 사용 — EffectReceiver 가 원격 ASC 로 라우팅.
    /// </summary>
    public sealed class PartyModel : IInitializable, IDisposable
    {
        private readonly PartyAscRegistry _registry;
        private readonly ISocketPacketState _state;
        private readonly AuthSession _auth;
        private readonly List<AbilitySystemComponent> _subscribed = new();

        public event Action Changed;

        public PartyModel(PartyAscRegistry registry, ISocketPacketState state, AuthSession auth)
        {
            _registry = registry;
            _state = state;
            _auth = auth;
        }

        public void Initialize()
        {
            _registry.Changed += RebuildSubscriptions;
            RebuildSubscriptions();
        }

        public void Dispose()
        {
            _registry.Changed -= RebuildSubscriptions;
            UnsubscribeAll();
        }

        /// <summary>레지스트리 구성이 바뀌면 각 ASC 의 HP 변경 구독을 재구성하고 View 재렌더를 알린다.</summary>
        private void RebuildSubscriptions()
        {
            UnsubscribeAll();
            foreach (var kv in _registry.Entries)
            {
                var asc = kv.Value;
                if (asc == null) continue;
                asc.OnAttributeChanged += OnAttributeChanged;
                _subscribed.Add(asc);
            }
            Changed?.Invoke();
        }

        private void UnsubscribeAll()
        {
            foreach (var asc in _subscribed)
                if (asc != null) asc.OnAttributeChanged -= OnAttributeChanged;
            _subscribed.Clear();
        }

        private void OnAttributeChanged(EGameplayAttribute type, int current, int max)
        {
            if (type == EGameplayAttribute.Health) Changed?.Invoke();
        }

        /// <summary>현재 파티원 스냅샷(닉네임 + HP). 자기 자신 포함.</summary>
        public IReadOnlyList<PartyMemberInfo> GetParty()
        {
            var list = new List<PartyMemberInfo>();
            foreach (var kv in _registry.Entries)
            {
                var asc = kv.Value;
                if (asc == null) continue;
                var hp = asc.GetAttribute(EGameplayAttribute.Health);
                string nick = _state.TryGetPlayer(kv.Key, out var snap) && !string.IsNullOrEmpty(snap.Nickname)
                    ? snap.Nickname
                    : $"Player {kv.Key}";
                bool isLocal = _auth != null && kv.Key == _auth.UserId;
                list.Add(new PartyMemberInfo(kv.Key, nick, hp?.CurrentValue ?? 0, hp?.MaxValue ?? 0, isLocal));
            }
            return list;
        }
    }
}
