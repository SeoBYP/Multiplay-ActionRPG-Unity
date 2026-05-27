namespace Server.Infrastructure;

public class ServerOptions
{
    /// <summary>TCP 서버가 바인드할 주소 (Docker: 0.0.0.0, 로컬: 127.0.0.1)</summary>
    public string Ip { get; set; } = "127.0.0.1";

    /// <summary>클라이언트에게 알려줄 접속 주소. 미설정 시 Ip를 그대로 사용.</summary>
    public string AdvertiseIp { get; set; } = "";

    public int Port { get; set; } = 7777;

    /// <summary>클라이언트가 실제로 접속할 IP. 0.0.0.0 바인드 시 AdvertiseIp 필수.</summary>
    public string ResolvedAdvertiseIp => string.IsNullOrWhiteSpace(AdvertiseIp) ? Ip : AdvertiseIp;
}
