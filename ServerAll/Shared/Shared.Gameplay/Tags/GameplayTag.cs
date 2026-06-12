using System;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 계층형 문자열 태그("State.Dead", "State.Buff.Atk"). 상태 게이트·Cue 트리거의 키.
    /// 현재는 **정확 일치**만 지원한다(계층 부모 매칭은 필요해질 때 후속). 값 동등성 struct.
    /// 클라·서버 공유(Shared.Gameplay) — UnityEngine 의존 없음.
    /// </summary>
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        private readonly string? _value;

        public GameplayTag(string? value) => _value = value;

        /// <summary>태그 문자열. default(GameplayTag)는 빈 문자열.</summary>
        public string Value => _value ?? string.Empty;

        /// <summary>비어있지 않은 태그인지. 컨테이너는 유효한 태그만 담는다.</summary>
        public bool IsValid => !string.IsNullOrEmpty(_value);

        public bool Equals(GameplayTag other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is GameplayTag t && Equals(t);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;

        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Equals(b);
        public static bool operator !=(GameplayTag a, GameplayTag b) => !a.Equals(b);

        /// <summary>"State.Dead" 같은 리터럴을 태그로 바로 쓸 수 있게 한다.</summary>
        public static implicit operator GameplayTag(string value) => new GameplayTag(value);
    }
}
