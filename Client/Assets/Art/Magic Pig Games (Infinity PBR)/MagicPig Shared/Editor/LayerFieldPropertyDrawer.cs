#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace MagicPigGames.Shared
{

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(LayerField))]
public class LayerFieldPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        property.intValue = EditorGUI.LayerField(position, label, property.intValue);
    }
}
#endif
}
