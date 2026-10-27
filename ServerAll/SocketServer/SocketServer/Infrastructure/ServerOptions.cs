namespace Server.Infrastructure;

public class ServerOptions
{
    public string Ip { get; set; } = "127.0.0.1";

    public string AdvertiseIp { get; set; } = "";

    public int Port { get; set; } = 7777;

    // 0.0.0.0 바인드 시 AdvertiseIp 필수.
    public string ResolvedAdvertiseIp => string.IsNullOrWhiteSpace(AdvertiseIp) ? Ip : AdvertiseIp;
}
