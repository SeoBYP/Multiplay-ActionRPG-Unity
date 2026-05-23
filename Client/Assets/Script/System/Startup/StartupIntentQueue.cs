using System.Collections.Generic;

namespace Script.System.Startup
{
    /// <summary>
    /// 로그인 완료 시 발생하는 시작 인텐트를 순서대로 보관하는 큐.
    /// ProjectScope Singleton — ApplyLogin에서 채우고, 각 ViewController.Initialize에서 소진한다.
    /// </summary>
    public class StartupIntentQueue
    {
        private readonly Queue<StartupIntent> _queue = new();

        public void Enqueue(StartupIntent intent)              => _queue.Enqueue(intent);
        public bool TryDequeue(out StartupIntent intent)       => _queue.TryDequeue(out intent);
        public bool HasPending                                 => _queue.Count > 0;
    }
}
