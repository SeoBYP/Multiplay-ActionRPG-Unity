using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MagicPigGames.Shared
{

public class MoveComponentToNewObject : MonoBehaviour
{
    [MenuItem("CONTEXT/Component/Move Component to Parent Object")]
    private static void MoveToParentObject(MenuCommand command)
    {
        var componentToMove = command.context as Component;
        if (componentToMove == null)
        {
            Debug.LogError("Component is null.");
            return;
        }

        if (componentToMove.transform.parent == null)
        {
            Debug.LogError("Component has no parent.");
            return;
        }

        var newParentObject = componentToMove.transform.parent.gameObject;

        Undo.RegisterCompleteObjectUndo(newParentObject, "Move Component to Parent Object");
        Undo.RegisterCompleteObjectUndo(componentToMove.gameObject, "Move Component to Parent Object");

        var newComponent = newParentObject.AddComponent(componentToMove.GetType());
        if (newComponent != null)
        {
            ComponentUtility.CopyComponent(componentToMove);
            ComponentUtility.PasteComponentValues(newComponent);
        }

        Undo.DestroyObjectImmediate(componentToMove);

        Selection.activeGameObject = newParentObject;
        EditorGUIUtility.PingObject(newParentObject);
    }

    [MenuItem("CONTEXT/Component/Move Component to New Object")]
    private static void MoveToNewObject(MenuCommand command)
    {
        var componentToMove = command.context as Component;
        if (componentToMove == null)
        {
            Debug.LogError("Component is null.");
            return;
        }

        var parentObject = componentToMove.gameObject;
        var newChildObject = new GameObject($"{componentToMove.GetType().Name}");
        newChildObject.transform.SetParent(parentObject.transform.parent);
        newChildObject.transform.localPosition = Vector3.zero;

        Undo.RegisterCreatedObjectUndo(newChildObject, "Move Component to New Object");
        Undo.RegisterCompleteObjectUndo(componentToMove.gameObject, "Move Component to New Object");

        var newComponent = newChildObject.AddComponent(componentToMove.GetType());
        if (newComponent != null)
        {
            ComponentUtility.CopyComponent(componentToMove);
            ComponentUtility.PasteComponentValues(newComponent);
        }

        Undo.DestroyObjectImmediate(componentToMove);

        Selection.activeGameObject = newChildObject;
        EditorGUIUtility.PingObject(newChildObject);
    }

    [MenuItem("CONTEXT/Component/Move Component to New Child Object")]
    private static void MoveToNewChildObject(MenuCommand command)
    {
        var componentToMove = command.context as Component;
        if (componentToMove == null)
        {
            Debug.LogError("Component is null.");
            return;
        }

        var parentObject = componentToMove.gameObject;
        var newChildObject = new GameObject($"{componentToMove.GetType().Name}");
        newChildObject.transform.SetParent(parentObject.transform);
        newChildObject.transform.localPosition = Vector3.zero;

        Undo.RegisterCreatedObjectUndo(newChildObject, "Move Component to New Child Object");
        Undo.RegisterCompleteObjectUndo(componentToMove.gameObject, "Move Component to New Child Object");

        var newComponent = newChildObject.AddComponent(componentToMove.GetType());
        if (newComponent != null)
        {
            ComponentUtility.CopyComponent(componentToMove);
            ComponentUtility.PasteComponentValues(newComponent);
        }

        Undo.DestroyObjectImmediate(componentToMove);

        Selection.activeGameObject = newChildObject;
        EditorGUIUtility.PingObject(newChildObject);
    }

    private static void MoveToNewChildObject(Component componentToMove, GameObject obj)
    {
        if (componentToMove == null)
        {
            Debug.LogError("Component is null.");
            return;
        }

        Debug.Log($"Parent Object is {obj.name}");
        var newChildObject = new GameObject($"{componentToMove.GetType().Name}");
        newChildObject.transform.SetParent(obj.transform);
        newChildObject.transform.localPosition = Vector3.zero;

        Undo.RegisterCreatedObjectUndo(newChildObject, "Move Component to New Child Object");
        Undo.RegisterCompleteObjectUndo(componentToMove.gameObject, "Move Component to New Child Object");

        var newComponent = newChildObject.AddComponent(componentToMove.GetType());
        if (newComponent != null)
        {
            ComponentUtility.CopyComponent(componentToMove);
            ComponentUtility.PasteComponentValues(newComponent);
        }

        Undo.DestroyObjectImmediate(componentToMove);
    }

    [MenuItem("GameObject/Magic Pig Games/Move ParticleSystem Component to New Child Objects", false, 10)]
    private static void MoveParticleSystemToNewChildObjects(MenuCommand command)
    {
        var selectedObjects = Selection.gameObjects;

        foreach (var obj in selectedObjects)
        {
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem == null) continue;

            MoveToNewChildObject(particleSystem, obj);
        }
    }

    [MenuItem("GameObject/Magic Pig Games/Move ParticleSystem Component to New Child Objects", true)]
    private static bool ValidateMoveParticleSystemToNewChildObjects()
    {
        var selectedObjects = Selection.gameObjects;
        return selectedObjects.Any(obj => obj.GetComponent<ParticleSystem>() != null);
    }
}
}
