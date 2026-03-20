using System.Reflection;
using Shared.Packet.Packets;

namespace Server.PacketHandler;

public sealed class PacketHandlerRegistry
{
    private readonly Dictionary<Type, PacketHandler> _handlers = new();

    public int Count => _handlers.Count;

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
                var attribute = method.GetCustomAttribute<PacketHandlerAttribute>();
                if (attribute is null)
                    continue;

                if (!ValidateMethodSignature(method, attribute.PacketType))
                {
                    Console.WriteLine($"[PacketHandlerRegistry] Invalid signature: {type.Name}.{method.Name}");
                    continue;
                }

                if (registry._handlers.ContainsKey(attribute.PacketType))
                {
                    Console.WriteLine($"[PacketHandlerRegistry] Duplicate handler: {attribute.PacketType.Name}");
                    continue;
                }

                var wrapper = CreateWrapper(method, attribute.PacketType);

                registry._handlers.Add(attribute.PacketType, wrapper);

                Console.WriteLine($"[PacketHandlerRegistry] Registered: {attribute.PacketType.Name} -> {type.Name}.{method.Name}");
                registeredCount++;
            }
        }

        Console.WriteLine($"[PacketHandlerRegistry] Total: {registeredCount}");
        return registry;
    }

    private static bool ValidateMethodSignature(MethodInfo method, Type packetType)
    {
        if (method.ReturnType != typeof(ValueTask))
            return false;

        var parameters = method.GetParameters();
        if (parameters.Length != 3)
            return false;

        if (parameters[0].ParameterType != typeof(Session))
            return false;

        if (parameters[1].ParameterType != packetType)
            return false;

        if (!typeof(Packet).IsAssignableFrom(parameters[1].ParameterType))
            return false;

        if (parameters[2].ParameterType != typeof(CancellationToken))
            return false;

        return true;
    }

    private static PacketHandler CreateWrapper(MethodInfo method, Type packetType)
    {
        return (session, packet, ct) =>
        {
            // 방어 코드
            if (packet is null)
                return ValueTask.CompletedTask;

            if (packet.GetType() != packetType)
            {
                Console.WriteLine(
                    $"[PacketHandlerRegistry] Packet type mismatch. Expected={packetType.Name}, Actual={packet.GetType().Name}");
                return ValueTask.CompletedTask;
            }

            // method: (Session, PingPackets, CancellationToken) 같은 구체 타입 메서드
            // wrapper: (Session, Packet, CancellationToken) 공통 타입
            return (ValueTask)method.Invoke(null, [session, packet, ct]);
        };
    }

    public PacketDispatcher CreateDispatcher()
    {
        return new PacketDispatcher(_handlers);
    }
}
