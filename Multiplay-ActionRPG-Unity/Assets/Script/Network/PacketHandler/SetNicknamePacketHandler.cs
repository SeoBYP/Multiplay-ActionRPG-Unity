using Game.Managers;

namespace Game.Network
{
    public class SetNicknamePacketHandler : IPacketHandler
    {
        public void Handle(Packet packet)
        {
            if (packet is S_SetNicknamePacket nicknamePacket)
            {
                if (nicknamePacket.success)
                {
                    GameManager.Instance.NickName.Value = nicknamePacket.message; 
                    // 성공 시 서버가 닉네임 메시지로 반환
                    GUI.Get<NickNameInputPopup>().Deactivate();
                    UnityEngine.Debug.Log($"[닉네임 설정 성공] {nicknamePacket.message}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[닉네임 설정 실패] {nicknamePacket.message}");
                    // 실패 UI 표시 등 처리 필요
                }
            }
        }
    }
}