using System;
using System.Collections.Generic;

namespace Game.Presentation.Dialogue
{
    /// <summary>대화 화면 상태(불변). IsOpen=false 면 창 닫힘. View 는 이 타입만 본다.</summary>
    public sealed class DialogueState
    {
        public static readonly DialogueState Closed = new(false, string.Empty, string.Empty, Array.Empty<DialogueChoiceView>(), Array.Empty<string>());

        public bool IsOpen { get; }
        public string Speaker { get; }
        public string BodyText { get; }
        public IReadOnlyList<DialogueChoiceView> Choices { get; }

        /// <summary>대화가 열린 동안 비활성화할 GameObject 이름들(per-대화). 닫힘 상태는 빈 목록.</summary>
        public IReadOnlyList<string> HideObjects { get; }

        public DialogueState(bool isOpen, string speaker, string bodyText,
            IReadOnlyList<DialogueChoiceView> choices, IReadOnlyList<string> hideObjects)
        {
            IsOpen = isOpen;
            Speaker = speaker;
            BodyText = bodyText;
            Choices = choices;
            HideObjects = hideObjects ?? Array.Empty<string>();
        }
    }

    /// <summary>현재 노드의 노출 선택지 한 개(View-facing). Index 는 SelectChoice 에 그대로 넘긴다.</summary>
    public sealed class DialogueChoiceView
    {
        public string Label { get; }
        public int Index { get; }

        public DialogueChoiceView(string label, int index)
        {
            Label = label;
            Index = index;
        }
    }
}
