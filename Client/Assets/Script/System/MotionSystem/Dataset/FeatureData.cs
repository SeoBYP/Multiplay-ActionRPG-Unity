using System;
using System.Linq;
using Unity.Mathematics;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Pose search에서 비교할 수 있도록 한 frame의 검색 feature를 모아둔 데이터입니다.
    /// 실제 bone transform 적용 데이터는 Dataset.AnimationData/BoneData에 있고,
    /// 이 타입은 "어떤 frame이 현재 query와 가장 가까운가"를 빠르게 판단하는 용도입니다.
    /// </summary>
    [Serializable]
    public struct FeatureData
    {
        /// <summary>
        /// 이 feature가 가리키는 animation clip index입니다.
        /// Dataset.animationsData[animationID]와 연결됩니다.
        /// </summary>
        public int animationID;

        /// <summary>
        /// animationID 안에서의 frame index입니다.
        /// Dataset.animationsData[animationID][animFrame]과 연결됩니다.
        /// </summary>
        public int animFrame;

        /// <summary>
        /// bone 위치와 속도를 정규화해서 저장한 feature 배열입니다.
        /// 일반적으로 position/velocity가 교차로 들어가며, weights 배열로 사용할 항목을 선택합니다.
        /// </summary>
        public float3[] positionsAndVelocities;

        /// <summary>
        /// 현재 frame 이후의 예상 root 위치 offset 목록입니다.
        /// 입력 방향과 앞으로의 이동 궤적이 비슷한 frame을 고르는 데 사용합니다.
        /// </summary>
        public float3[] futureOffsets;

        /// <summary>
        /// 현재 frame 이후의 예상 facing 방향 목록입니다.
        /// 회전/방향 전환이 자연스러운 pose를 고르는 데 사용합니다.
        /// </summary>
        public float3[] futureDirections;

        /// <summary>
        /// 현재 frame 이전의 root 위치 offset 목록입니다.
        /// 지금 pose가 어떤 이동 흐름에서 왔는지 비교하기 위한 과거 trajectory feature입니다.
        /// </summary>
        public float3[] pastOffsets;

        /// <summary>
        /// 현재 frame 이전의 facing 방향 목록입니다.
        /// 갑작스러운 방향 전환이나 UTurn 후보를 고를 때 continuity 판단에 사용할 수 있습니다.
        /// </summary>
        public float3[] pastDirections;

        /// <summary>
        /// 정규화된 bone position/velocity feature를 원래 스케일로 되돌립니다.
        /// 디버깅, gizmo 표시, distance 분석에서 실제 단위 값을 확인할 때 사용합니다.
        /// </summary>
        public (float3[], float3[]) UnNormalizeValues(
            float3[] meansPos,
            float3[] stdsPos,
            float3[] meansVel,
            float3[] stdsVel,
            float[] weights)
        {
            float3[] unNormalizedPos = new float3[weights.Count(w => w == 1f) / 2];
            float3[] unNormalizedVel = new float3[weights.Count(w => w == 1f) / 2];
            int counterPos = 0;
            int counterVel = 0;
            for (int i = 0; i < positionsAndVelocities.Length; i++)
            {
                if (weights[i] == 0)
                {
                    continue;
                }

                float3 real;
                if (i % 2 == 0)
                {
                    real = positionsAndVelocities[i] * stdsVel[i / 2] + meansVel[i / 2];
                    unNormalizedVel[counterVel] = real;
                    counterVel++;
                    continue;
                }
                real = positionsAndVelocities[i] * stdsPos[i / 2] + meansPos[i / 2];
                unNormalizedPos[counterPos] = real;
                counterPos++;
            }

            return (unNormalizedPos, unNormalizedVel);
        }

        /// <summary>
        /// feature 내용을 로그로 확인하기 위한 문자열을 만듭니다.
        /// 런타임 성능 경로보다는 디버깅 용도로 사용하는 함수입니다.
        /// </summary>
        public override string ToString()
        {
            string result = "[" + animationID + "][" + animFrame + "]";
            result += "\nOffsets \n";
            futureOffsets.ForEach(offsets =>
            {
                result += offsets;
            });
            result += "\nDirections \n";
            futureDirections.ForEach(dir =>
            {
                result += dir;
            });

            result += "\nBones \n";
            positionsAndVelocities.ForEach(pos =>
            {
                result += pos;
            });
            return result;
        }
    }
}
