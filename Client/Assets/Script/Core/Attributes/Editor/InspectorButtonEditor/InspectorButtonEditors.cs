#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    public class InspectorButtonEditors
    {
        [CustomEditor(typeof(MonoBehaviour), true)]
        [CanEditMultipleObjects]
        public sealed class MonoBehaviourInspectorButtonEditor : InspectorButtonEditorBase
        {
        }

        [CustomEditor(typeof(ScriptableObject), true)]
        [CanEditMultipleObjects]
        public sealed class ScriptableObjectInspectorButtonEditor : InspectorButtonEditorBase
        {
        }
    }
}
#endif