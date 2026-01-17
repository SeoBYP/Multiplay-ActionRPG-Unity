using UnityEngine;
using UnityEngine.EventSystems;

public abstract class CanvasUIBehaviour : UIBehaviour
{
    private Canvas _canvas;
    public Canvas Canvas => _canvas ??= GetComponent<Canvas>();

    public void Activate()
    {
        Canvas.enabled = true;
        OnActivate();
    }

    protected abstract void OnActivate();
    
    public void Deactivate()
    {
        Canvas.enabled = false;
        OnDeactivate();
    }

    protected abstract void OnDeactivate();
}