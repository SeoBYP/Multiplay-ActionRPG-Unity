using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// 일반 이동 query에서 root pose를 CharacterControllerBase.Move로 전달하는 PoseSetter 구현체입니다.
    /// </summary>
    public class MotionPoseSetter : PoseSetter
    {
        /// <summary>
        /// BlendingResults의 root position/rotation을 캐릭터 이동 어댑터에 적용합니다.
        /// </summary>
        public override void SetRootPose(BlendingResults blendingResults, CharacterControllerBase characterControllerBase)
        {
            characterControllerBase.Move(blendingResults.rootPosition, blendingResults.rootRotation, Time.fixedDeltaTime);
        }
    }
}
