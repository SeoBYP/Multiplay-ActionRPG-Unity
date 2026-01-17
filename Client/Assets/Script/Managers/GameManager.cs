using System;
using R3;

namespace Game.Managers
{
    public class GameManager : PersistentSingleton<GameManager>
    {
        public ReactiveProperty<string> NickName = new("");

        protected override void OnInitializeSingleton()
        {
            
        }

        private void Start()
        {
            if (string.IsNullOrWhiteSpace(NickName.Value))
            {
                GUI.Get<NickNameInputPopup>().Activate();
            }
        }
    }
}