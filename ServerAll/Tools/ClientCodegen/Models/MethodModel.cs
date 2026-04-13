namespace ClientCodegen.Models;

public record MethodModel(
    string Name,             // "Login"
    string RequestType,      // "LoginRequest"
    string ResponseType,     // "LoginResponse"
    MethodKind Kind);        // Unary

