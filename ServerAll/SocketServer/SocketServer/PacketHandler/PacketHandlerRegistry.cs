using System.Reflection;
using Microsoft.Extensions.Logging;
using Shared.Packet.Packets;

namespace Server.PacketHandler;

public sealed class PacketHandlerRegistry
{
    private readonly Dictionary<Type, PacketHandler> _handlers = new();

    public int Count => _handlers.Count;

    public static PacketHandlerRegistry Build(ILogger<PacketHandlerRegistry> logger)
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
                    logger.LogWarning("Invalid packet handler signature: {TypeName}.{MethodName}", type.Name, method.Name);
                    continue;
                }

                if (registry._handlers.ContainsKey(attribute.PacketType))
                {
                    logger.LogWarning("Duplicate packet handler for {PacketType}", attribute.PacketType.Name);
                    continue;
                }

                var wrapper = CreateWrapper(method, attribute.PacketType, logger);
                registry._handlers.Add(attribute.PacketType, wrapper);

                logger.LogInformation(
                    "Registered packet handler {PacketType} -> {TypeName}.{MethodName}",
                    attribute.PacketType.Name,
                    type.Name,
                    method.Name);
                registeredCount++;
            }
        }

        logger.LogInformation("Registered {RegisteredCount} packet handlers", registeredCount);
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

    private static PacketHandler CreateWrapper(MethodInfo method, Type packetType, ILogger<PacketHandlerRegistry> logger)
    {
        return (session, packet, ct) =>
        {
            if (packet is null)
                return ValueTask.CompletedTask;

            if (packet.GetType() != packetType)
            {
                logger.LogWarning(
                    "Packet type mismatch. Expected={ExpectedPacketType}, Actual={ActualPacketType}",
                    packetType.Name,
                    packet.GetType().Name);
                return ValueTask.CompletedTask;
            }

            return (ValueTask)method.Invoke(null, [session, packet, ct])!;
        };
    }

    public PacketDispatcher CreateDispatcher(ILogger<PacketDispatcher> logger)
    {
        return new PacketDispatcher(_handlers, logger);
    }
}
