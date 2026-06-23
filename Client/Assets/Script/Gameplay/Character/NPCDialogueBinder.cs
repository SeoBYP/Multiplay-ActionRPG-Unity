using Game.System.Dialogue;
using UnityEngine;
using VContainer.Unity;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 씬에 배치된 모든 NPCDialogueInteractable 에 IDialogueLauncher 를 꽂는다(엔트리 포인트).
    /// 개별 NPC 마다 DI(InjectGameObject)를 돌리지 않고 한 번에 바인딩 — 씬 NPC N개 대응.
    /// (런타임 스폰 NPC 는 스폰 시점에 별도 Bind 필요 — A1 은 씬 배치 NPC 대상.)
    /// </summary>
    public sealed class NPCDialogueBinder : IInitializable
    {
        private readonly IDialogueLauncher _launcher;
        private readonly IDialogueCamera _camera;

        // camera 는 씬에 컨트롤러가 있으면 주입, 없으면 null(대화는 정상·카메라만 미동작).
        public NPCDialogueBinder(IDialogueLauncher launcher, IDialogueCamera camera = null)
        {
            _launcher = launcher;
            _camera = camera;
        }

        public void Initialize()
        {
            var npcs = Object.FindObjectsByType<NPCDialogueInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var npc in npcs)
                npc.Bind(_launcher, _camera);
        }
    }
}
