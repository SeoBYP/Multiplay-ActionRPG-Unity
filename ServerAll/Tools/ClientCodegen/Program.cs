using ClientCodegen.Config;
using ClientCodegen.Copiers;
using ClientCodegen.Generators;
using ClientCodegen.Models;
using ClientCodegen.Parsers;

namespace ClientCodegen;

internal static class Program
{
    private static void Main(string[] args)
    {
        var config = CodegenConfig.Load(args);

        Console.WriteLine($"[Program] ProtoDir        : {config.ProtoDir}");
        Console.WriteLine($"[Program] UnityIfaceDir   : {config.UnityIfaceDir}");
        Console.WriteLine($"[Program] UnityServiceDir : {config.UnityServiceDir}");

        if (!Directory.Exists(config.ProtoDir))
        {
            Console.WriteLine("[Program] Proto directory not found.");
            return;
        }

        var protoFiles = Directory.GetFiles(config.ProtoDir, "*.proto", SearchOption.TopDirectoryOnly);
        var services = new List<ServiceModel>();

        Console.WriteLine($"[Program] Proto file count: {protoFiles.Length}");

        foreach (var protoFile in protoFiles)
        {
            Console.WriteLine();
            Console.WriteLine("[Program] ------------------------------");
            Console.WriteLine($"[Program] Parsing: {protoFile}");

            var service = ProtoParser.Parse(protoFile);

            if (service is null)
            {
                Console.WriteLine("[Program] Parse failed.");
                continue;
            }

            services.Add(service);

            Console.WriteLine($"[Program] ServiceName     : {service.ServiceName}");
            Console.WriteLine($"[Program] CsharpNamespace : {service.CsharpNamespace}");
            Console.WriteLine($"[Program] MethodCount     : {service.Methods.Count}");
        }

        Console.WriteLine();
        Console.WriteLine("[Program] ===== Copy Stubs =====");
        StubCopier.Copy(config);

        Console.WriteLine();
        Console.WriteLine("[Program] ===== Generate Interfaces =====");
        InterfaceGenerator.Generate(services, config.UnityIfaceDir);

        Console.WriteLine();
        Console.WriteLine("[Program] ===== Generate Services =====");
        ServiceWrapperGenerator.Generate(services, config.UnityServiceDir);

        Console.WriteLine();
        Console.WriteLine("[Program] ===== Generate Socket Packets =====");
        PacketHandlerGenerator.Generate(config.PacketFile, config.PacketSerializerFile, config.UnityPacketDir);
    }
}
