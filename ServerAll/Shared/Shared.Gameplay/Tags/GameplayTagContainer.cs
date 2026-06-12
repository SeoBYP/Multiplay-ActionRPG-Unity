using System.Collections;
using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 한 액터가 보유한 GameplayTag 집합. 상태 표현(예: State.Dead)과 질의의 단일 지점.
    /// 유효(비어있지 않은) 태그만 담는다. 중복은 무시(집합).
    /// </summary>
    public sealed class GameplayTagContainer : IEnumerable<GameplayTag>
    {
        private readonly HashSet<GameplayTag> _tags = new();

        public int Count => _tags.Count;

        /// <summary>유효한 태그면 추가하고 새로 들어갔는지 반환. 무효/중복이면 false.</summary>
        public bool Add(GameplayTag tag) => tag.IsValid && _tags.Add(tag);

        public bool Remove(GameplayTag tag) => _tags.Remove(tag);

        public bool HasTag(GameplayTag tag) => _tags.Contains(tag);

        /// <summary>주어진 태그 중 하나라도 보유하면 true.</summary>
        public bool HasAny(IEnumerable<GameplayTag> tags)
        {
            foreach (var t in tags)
                if (_tags.Contains(t))
                    return true;
            return false;
        }

        public void Clear() => _tags.Clear();

        public IEnumerator<GameplayTag> GetEnumerator() => _tags.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
