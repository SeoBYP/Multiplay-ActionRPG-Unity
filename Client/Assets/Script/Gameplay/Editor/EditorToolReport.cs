using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// 에디터 툴(Exporter 등)의 결과 알림 — <b>메인 스레드를 붙잡지 않는</b> 다이얼로그.
    ///
    /// <para><c>EditorUtility.DisplayDialog</c> 는 사람이 클릭할 때까지 에디터 메인 스레드를 점유한다.
    /// 메뉴 항목을 Unity CLI(<c>unity command … menu</c>)로 호출하면 응답이 다이얼로그에 막혀 타임아웃되고,
    /// <b>그 뒤의 모든 Pipeline 명령도 다이얼로그를 닫을 때까지 타임아웃</b>한다.</para>
    ///
    /// <para><b>해법</b>: <c>EditorApplication.delayCall</c> 로 다음 에디터 프레임에 띄운다.
    /// 명령은 즉시 반환되고 다이얼로그는 에디터 자체 update 루프에서 뜬다 — 사람은 그대로 확인창을
    /// 보고, 자동화는 막히지 않는다.</para>
    ///
    /// <para><b>규칙</b>: 자동화가 호출할 수 있는 로직(<c>BakeAll</c>/<c>ImportAll</c> 류)에는
    /// 다이얼로그를 두지 않는다. 다이얼로그는 <c>[MenuItem]</c> 래퍼에서 이 클래스로만 띄운다.</para>
    /// </summary>
    public static class EditorToolReport
    {
        /// <summary>결과 알림을 다음 에디터 프레임으로 미뤄 띄운다. 콘솔에도 남긴다.</summary>
        public static void Later(string title, string message)
        {
            Debug.Log($"[{title}] {message}");
            EditorApplication.delayCall += () => EditorUtility.DisplayDialog(title, message, "확인");
        }

        /// <summary>실패 알림(콘솔은 LogError). 사용법은 <see cref="Later"/> 와 동일.</summary>
        public static void ErrorLater(string title, string message)
        {
            Debug.LogError($"[{title}] {message}");
            EditorApplication.delayCall += () => EditorUtility.DisplayDialog(title, message, "확인");
        }
    }
}
