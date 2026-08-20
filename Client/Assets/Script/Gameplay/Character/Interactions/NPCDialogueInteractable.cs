using Game.System.Dialogue;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 대화 NPC 상호작용. InteractionDetector(E)가 최근접으로 선택 → Interact → 대화 시작.
    /// npcId(문자열)만 들고 IDialogueLauncher 로 위임 — 대화 내용/창은 Presentation/GUI 가 소유(레이어 경계).
    /// launcher 는 DI 직접 주입이 아니라 씬 바인더(DialogueNpcBinder)가 Bind 로 꽂는다(씬 배치 NPC N개 대응).
    /// </summary>
    public class NPCDialogueInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string npcId;
        [Tooltip("카메라가 바라볼 대상(보통 NPC 머리/시선 트랜스폼). 비우면 이 오브젝트 transform.")]
        [SerializeField] private Transform lookTarget;

        private IDialogueLauncher _launcher;
        private IDialogueCamera _camera;

        /// <summary>대화 NPC 식별자(DialogueCatalog 키).</summary>
        public string NpcId => npcId;

        /// <summary>씬 바인더가 런처/카메라를 주입(InjectGameObject 대신). camera 는 optional(미배치 시 null).</summary>
        public void Bind(IDialogueLauncher launcher, IDialogueCamera camera = null)
        {
            _launcher = launcher;
            _camera = camera;
        }

        public string InteractionPrompt => "대화";

        public void Interact(GameObject interactor)
        {
            if (string.IsNullOrEmpty(npcId))
            {
                Debug.LogWarning("[NPCDialogueInteractable] npcId 미설정");
                return;
            }
            if (_launcher == null)
            {
                Debug.LogWarning($"[NPCDialogueInteractable] launcher 미바인딩(npcId={npcId}) — DialogueNpcBinder 등록 확인");
                return;
            }
            // 카메라 먼저 대상 등록(이후 DialogueModel 이 노드 구도 SetShot). 카메라 미배치면 생략.
            _camera?.Enter(lookTarget != null ? lookTarget : transform, interactor != null ? interactor.transform : null);
            _launcher.Open(npcId);
        }
    }
}
