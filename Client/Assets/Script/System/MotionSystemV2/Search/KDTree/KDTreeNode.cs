namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// KDTree의 단일 노드. NativeArray에 저장되는 blittable 구조체.
    /// splitDim == -1이면 리프 노드, poseIndex에 포즈 인덱스가 저장된다.
    /// </summary>
    public struct KDTreeNode
    {
        // 분할 차원. -1이면 리프 노드
        public int   splitDim;
        // 분할 기준값 (해당 차원의 중앙값)
        public float splitValue;
        // 왼쪽 자식 노드 인덱스 (-1이면 없음)
        public int   left;
        // 오른쪽 자식 노드 인덱스 (-1이면 없음)
        public int   right;
        // 리프 노드에서만 유효한 포즈 인덱스. 내부 노드는 -1
        public int   poseIndex;
    }
}
