using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    [CreateAssetMenu(fileName = "PoseSearchSchema", menuName = "MotionMatchingV2/Schema")]
    public class PoseSearchSchema : ScriptableObject
    {
        [SerializeField] private float sampleRate = 30f;
        [SerializeField] private List<PoseSearchChannelBase> channels = new();

        public float SampleRate => sampleRate;
        public float PoseStep => 1f / sampleRate;
        public IReadOnlyList<PoseSearchChannelBase> Channels => channels;

        public int TotalFeatureDimension => channels.Sum(c => c.FeatureDimension);

        /// <summary>
        /// 채널 i가 Feature Vector 내에서 시작하는 float 인덱스 (stride 오프셋).
        /// </summary>
        public int GetChannelOffset(int channelIndex)
        {
            int offset = 0;
            for (int i = 0; i < channelIndex; i++)
                offset += channels[i].FeatureDimension;
            return offset;
        }

        /// <summary>
        /// 전체 채널 Weight 합으로 정규화한 채널 i의 가중치.
        /// Cost 계산 시 채널 간 스케일을 맞추는 데 사용한다.
        /// </summary>
        public float GetNormalizedWeight(int channelIndex)
        {
            float total = channels.Sum(c => c.Weight);
            if (total <= 0f) return channels.Count > 0 ? 1f / channels.Count : 0f;
            return channels[channelIndex].Weight / total;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (sampleRate <= 0f) sampleRate = 1f;
        }
#endif
    }
}
