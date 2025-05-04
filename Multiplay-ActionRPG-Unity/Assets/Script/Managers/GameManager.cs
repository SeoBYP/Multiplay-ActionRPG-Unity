using R3;

namespace Game.Managers
{
    public class GameManager : PersistentSingleton<GameManager>
    {
        public ReactiveProperty<string> NickName = new("");

        protected override void OnInitializeSingleton()
        {
            
        }
    }
}