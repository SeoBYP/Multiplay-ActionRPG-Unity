namespace ClientCodegen.Config;

public class CodegenConfig
{
    public required string ProtoDir { get; init; }
    public required string PacketFile { get; init; }
    public required string PacketSerializerFile { get; init; }
    public required string StubSrcDir { get; init; }
    public required string UnityGenDir { get; init; }
    public required string UnityIfaceDir { get; init; }
    public required string UnityServiceDir { get; init; }
    public required string UnityHandlerDir { get; init; }
    public required string UnityPacketDir { get; init; }

    public static CodegenConfig Load(string[] args)
    {
        // 환경변수 or args로 받기
        var repoRoot = args.Length > 0
            ? args[0]
            : Environment.GetEnvironmentVariable("REPO_ROOT")
              ?? throw new InvalidOperationException("REPO_ROOT not set");


        return new CodegenConfig
        {
            ProtoDir = Path.Combine(repoRoot, "ServerAll", "Shared", "Shared.Contracts", "Protos"),
            PacketFile = Path.Combine(repoRoot, "ServerAll", "Shared", "Shared.Packet", "Packets", "Packet.cs"),
            PacketSerializerFile = Path.Combine(repoRoot, "ServerAll", "Shared", "Shared.Packet", "PacketSerializer.cs"),
            StubSrcDir = Path.Combine(repoRoot, "ServerAll", "Shared", "Shared.Contracts", "obj", "Debug", "net10.0"),
            UnityGenDir = Path.Combine(repoRoot, "Client", "Assets", "Script", "Network", "Https", "Generated"),
            UnityIfaceDir = Path.Combine(repoRoot, "Client", "Assets", "Script", "Network", "Https", "Interfaces"),
            UnityServiceDir = Path.Combine(repoRoot, "Client", "Assets", "Script", "Network", "Https", "Services"),
            UnityHandlerDir = Path.Combine(repoRoot, "Client", "Assets", "Script", "Network", "Socket", "Handlers"),
            UnityPacketDir = Path.Combine(repoRoot, "Client", "Assets", "Script", "Network", "Socket", "Packets")
        };
    }
}
