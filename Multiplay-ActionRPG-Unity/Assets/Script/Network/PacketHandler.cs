using ServerCore.Protocol;

namespace Game.Network
{
    public abstract class PacketHandler
    {
        // 공통 기능
        protected void Log(string message)
        {
            UnityEngine.Debug.Log($"[{GetType().Name}] {message}");
        }
        
        protected void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[{GetType().Name}] {message}");
        }
    }
}