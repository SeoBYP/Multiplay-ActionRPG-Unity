namespace Script.System.Startup
{
    /// <summary>
    /// 로그인 완료 후 씬이 준비되면 순서대로 처리할 시작 인텐트의 기반 타입.
    /// StartupIntentQueue에 쌓인 후 각 ViewController의 Initialize에서 소진된다.
    /// </summary>
    public abstract class StartupIntent { }
}
