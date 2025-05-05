using Cysharp.Threading.Tasks;
using Game.Managers;
using R3;
using TMPro;
using UnityEngine;

public class MainHUD : CanvasUIBehaviour
{
    [SerializeField] private ChatBox chatBox;
    [SerializeField] private TextMeshProUGUI playerNickname;

    public ChatBox ChatBox => chatBox ??= GetComponentInChildren<ChatBox>();
    
    protected override void Start()
    {
        Initialized();
    }

    private void Initialized()
    {
        GameManager.Instance.NickName
            .Subscribe((nickname) => { playerNickname.text = nickname; })
            .AddTo(this.destroyCancellationToken);
    }

    protected override void OnActivate()
    {
    }

    protected override void OnDeactivate()
    {
    }
}