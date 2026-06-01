using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.GameScene;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화면 전체를 덮는 페이드 캔버스.
/// GUIRoot보다 항상 앞에 보여줘야 함.
/// GameSceneManager가 Addressable로 동적 생성 → 씬 전환 후 파괴한다.
/// </summary>
public class FaderCanvas : UIBehaviour, IFader
{
    [SerializeField] private Image fader;
    [SerializeField] private float duration = 0.5f;

    /// <summary>화면을 검게 덮는다.</summary>
    public async UniTask FadeInAsync(CancellationToken ct)
    {
        fader.CrossFadeAlpha(1f, duration, false);
        await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
    }

    /// <summary>화면을 드러낸다.</summary>
    public async UniTask FadeOutAsync(CancellationToken ct)
    {
        fader.CrossFadeAlpha(0f, duration, false);
        await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
    }
}
