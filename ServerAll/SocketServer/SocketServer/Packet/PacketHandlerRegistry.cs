using System.Reflection;
using ServerCore.Protocol;

namespace Server.Packet;

public class PacketHandlerRegistry
{
    private readonly Dictionary<ServerCore.Protocol.Packet.PayloadOneofCase, PacketHandler> _handlers = new();

    /// <summary>
    /// 등록된 핸들러 개수
    /// </summary>
    public int Count => _handlers.Count;

    /// <summary>
    /// 자동 등록 빌더
    /// 
    /// 현재 어셈블리에서 [PacketHandler] Attribute가 붙은 모든 메서드를 찾아 등록합니다.
    /// </summary>
    public static PacketHandlerRegistry Build()
    {
        var registry = new PacketHandlerRegistry();

        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes();

        int registeredCount = 0;

        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

            foreach (var method in methods)
            {
                // PacketHandlerAttribute 메서드 Attribute 있는지 확인
                var attribute = method.GetCustomAttribute<PacketHandlerAttribute>();
                if (attribute is null)
                    continue;
                
                // 메서드 시그니처 검증
                if (!ValidateMethodSignature(method))
                {
                    Console.WriteLine($"[PacketHandlerRegistry] Invalid signature: {type.Name}.{method.Name}");
                    continue;
                }

                // PayloadOneofCase로 변환
                if (!TryParsePayloadCase(attribute.PayloadCaseName, out var payloadCase))
                {
                    Console.WriteLine($"[PacketHandlerRegistry] Invalid PayloadCase: {attribute.PayloadCaseName}");
                    continue;
                }

                // 델리게이트 생성
                var handler = (PacketHandler)Delegate.CreateDelegate(typeof(PacketHandler), method);

                // 등록
                if (registry._handlers.TryAdd(payloadCase, handler))
                {
                    Console.WriteLine(
                        $"[PacketHandlerRegistry] Registered: {payloadCase} → {type.Name}.{method.Name}");
                    registeredCount++;
                }
            }
        }

        Console.WriteLine($"[PacketHandlerRegistry] Total: {registeredCount} handlers registered\n");
        return registry;
    }

    /// <summary>
    /// 메서드 시그니처 검증
    /// 
    /// 올바른 형식:
    /// public static ValueTask Handle(Session session, Packet packet, CancellationToken ct)
    /// </summary>
    private static bool ValidateMethodSignature(MethodInfo methodInfo)
    {
        // 반환 타입은 항상 ValueTask
        if (methodInfo.ReturnType != typeof(ValueTask))
            return false;

        // 파라미터: Session, Packet, CancellationToken
        var parameters = methodInfo.GetParameters();
        if (parameters.Length != 3)
            return false;

        if (parameters[0].ParameterType != typeof(Session))
            return false;

        if (parameters[1].ParameterType != typeof(ServerCore.Protocol.Packet))
            return false;

        if (parameters[2].ParameterType != typeof(CancellationToken))
            return false;
        return true;
    }

    /// <summary>
    /// PayloadCaseName을 PayloadOneofCase로 변환
    /// 
    /// "CChat" → Packet.PayloadOneofCase.CChat
    /// </summary>
    private static bool TryParsePayloadCase(string caseName, out ServerCore.Protocol.Packet.PayloadOneofCase result)
    {
        return Enum.TryParse(caseName, ignoreCase: true, out result);
    }

    /// <summary>
    /// Dispatcher 생성
    /// </summary>
    public PacketDispatcher CreateDispatcher()
    {
        return new PacketDispatcher(_handlers);
    }
}