using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 원격 플레이어 로코모션 복원 — 순수 계산(테스트로 고정).
    ///
    /// <b>왜 방향을 패킷에 싣지 않는가</b>: 원격은 이미 위치(→속도)와 회전(→facing)을 받는다.
    /// 둘을 조합하면 로컬 <see cref="GroundState"/> 가 쓰는 것과 <b>같은 공식</b>으로 MoveX/MoveY 가 나온다 —
    /// 방향은 이미 전송되고 있는 정보라 바이트를 더 쓸 이유가 없다.
    /// 반대로 점프·낙하·사다리는 전부 "y 가 변한다"로 같아 복원이 불가능해, 그것만 1바이트(AnimState)로 싣는다.
    /// </summary>
    public static class RemoteLocomotion
    {
        /// <summary>
        /// 월드 속도(m/s)를 <paramref name="rotYDegrees"/> 가 정의하는 facing 프레임으로 분해한다.
        /// 반환 x = 우측 성분(게걸음), y = 전방 성분(전진/후진). <b>단위는 m/s 그대로</b> —
        /// 컨트롤러 2D 블렌드 좌표가 클립 실측 속도라 정규화하면 발이 미끄러진다.
        /// </summary>
        public static Vector2 ToFacingFrame(Vector3 worldVelocity, float rotYDegrees)
        {
            Vector3 forward = Quaternion.Euler(0f, rotYDegrees, 0f) * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            worldVelocity.y = 0f; // 수직은 로코모션 블렌드와 무관(그건 AnimState 가 담당)
            return new Vector2(Vector3.Dot(worldVelocity, right), Vector3.Dot(worldVelocity, forward));
        }
    }
}
