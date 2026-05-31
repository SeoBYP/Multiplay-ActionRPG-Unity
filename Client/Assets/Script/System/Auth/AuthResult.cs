namespace Game.System.Auth
{
    public enum AuthResult
    {
        Success,
        NeedLogin,
        TokenExpired,
        Failed
    }
}