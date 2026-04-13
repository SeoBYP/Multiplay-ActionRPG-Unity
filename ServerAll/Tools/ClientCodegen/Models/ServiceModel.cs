namespace ClientCodegen.Models;

public record ServiceModel(
    string ServiceName, // "AuthService"
    string CsharpNamespace, // "GameServer.Grpc.Auth"
    string ProtoFileName, // "auth"
    List<MethodModel> Methods);