namespace Game.Presentation.Title
{
    /// <summary>
    /// 타이틀 화면에서 발행되는 Intent.
    /// View는 Accept(TitleIntent)만 호출하며 서비스를 직접 호출하지 않는다.
    /// </summary>
    public abstract class TitleIntent
    {
        private TitleIntent() { }

        public sealed class Login : TitleIntent
        {
            public readonly string Email;
            public readonly string Password;

            public Login(string email, string password)
            {
                Email    = email;
                Password = password;
            }
        }
    }
}
