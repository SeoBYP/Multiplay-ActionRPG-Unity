namespace Game.System.Dialogue
{
    /// <summary>대화 카메라 구도(노드별 그래프툴에서 선택). 런타임 DialogueCameraController 가 vcam 타깃을 그에 맞게 세팅.</summary>
    public enum DialogueShot
    {
        Closeup,      // NPC 얼굴/상체 클로즈업 (LookAt=NPC, Follow=NPC)
        OverShoulder, // 플레이어 어깨 너머 NPC (LookAt=NPC, Follow=Player)
        TwoShot,      // 플레이어+NPC 둘 다 (TargetGroup)
    }
}
