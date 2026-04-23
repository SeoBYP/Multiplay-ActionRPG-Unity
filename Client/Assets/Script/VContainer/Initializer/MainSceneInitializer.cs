using System;
using Game.Main.Input;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    public class MainSceneInitializer : IInitializable, IDisposable
    {
        [Inject] private PlayerInputActions _playerInputActions;
        
        public void Initialize()
        {
            _playerInputActions.Enable();
            _playerInputActions.Player.Enable();
            _playerInputActions.UI.Enable();
        }

        public void Dispose()
        {
            _playerInputActions.Disable();
            _playerInputActions.Player.Disable();
            _playerInputActions.UI.Disable();
            _playerInputActions?.Dispose();
        }
    }
}