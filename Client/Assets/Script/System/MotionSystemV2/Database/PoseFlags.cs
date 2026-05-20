using System;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// 포즈 선택 시 적용되는 플래그. PoseMetadata에 비트 필드로 저장된다.
    /// </summary>
    [Flags]
    public enum PoseFlags : int
    {
        None            = 0,
        IsLooping       = 1 << 0,   // 루핑 애니메이션
        BlockTransition = 1 << 1,   // 이 포즈로 직접 점프 금지 (재생 중 도달만 허용)
    }
}
