using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Dialogue
{
    /// <summary>
    /// npcId → DialogueDefinition 매핑(ScriptableObject). NPC 는 npcId(문자열)만 들고, 대화 내용은 이 카탈로그가 소유.
    /// ItemDisplayCatalog·DropTable 과 동일한 클라 콘텐츠 저작 컨벤션.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Dialogue/Dialogue Catalog", fileName = "DialogueCatalog")]
    public sealed class DialogueCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string npcId;
            public DialogueDefinition dialogue;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<string, DialogueDefinition> _byId;

        public DialogueDefinition Get(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return null;
            _byId ??= BuildIndex();
            return _byId.GetValueOrDefault(npcId);
        }

        private Dictionary<string, DialogueDefinition> BuildIndex()
        {
            var dict = new Dictionary<string, DialogueDefinition>(entries.Length);
            foreach (var e in entries)
                if (e != null && !string.IsNullOrEmpty(e.npcId) && e.dialogue != null)
                    dict[e.npcId] = e.dialogue;
            return dict;
        }
    }
}
