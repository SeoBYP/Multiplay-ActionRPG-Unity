using System.Text.RegularExpressions;
using ClientCodegen.Models;

namespace ClientCodegen.Parsers;

public static class ProtoParser
{
    private static readonly Regex CsharpNamespaceRegex =
        new(@"option\s+csharp_namespace\s*=\s*""(?<namespace>[^""]+)""\s*;");

    private static readonly Regex ServiceStartRegex =
        new(@"service\s+(?<name>\w+)\s*\{");

    // rpc 본문 { ... } 도 허용
    private static readonly Regex RpcRegex =
        new(@"rpc\s+(?<name>\w+)\s*\(\s*(?<reqStream>stream\s+)?(?<req>\w+)\s*\)\s*returns\s*\(\s*(?<resStream>stream\s+)?(?<res>\w+)\s*\)\s*(?:;|\{[^}]*\})");

    public static ServiceModel? Parse(string protoPath)
    {
        Console.WriteLine($"[ProtoParser] ===== Parse File Start =====");
        Console.WriteLine($"[ProtoParser] Path: {protoPath}");

        var content = File.ReadAllText(protoPath);

        var protoFileName = Path.GetFileNameWithoutExtension(protoPath);

        return ParseContent(content, protoFileName);
    }

    public static ServiceModel? ParseContent(string content, string protoFileName)
    {
        Console.WriteLine($"[ProtoParser] ===== Parse Content Start =====");
        Console.WriteLine($"[ProtoParser] Proto: {protoFileName}");

        var namespaceMatch = CsharpNamespaceRegex.Match(content);

        if (!namespaceMatch.Success)
        {
            Console.WriteLine($"[ProtoParser] csharp_namespace not found.");
            return null;
        }

        var csharpNamespace = namespaceMatch.Groups["namespace"].Value;

        Console.WriteLine($"[ProtoParser] Namespace: {csharpNamespace}");

        var serviceBlocks = ExtractServiceBlocks(content).ToList();

        // 현재는 첫 번째 service만 처리한다.
        var firstService = serviceBlocks.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstService.ServiceName))
        {
            Console.WriteLine($"[ProtoParser] service block not found.");
            return null;
        }

        Console.WriteLine($"[ProtoParser] Service: {firstService.ServiceName}");

        var methods = new List<MethodModel>();

        var rpcMatches = RpcRegex.Matches(firstService.Body);

        Console.WriteLine($"[ProtoParser] RPC Count: {rpcMatches.Count}");

        foreach (Match rpcMatch in rpcMatches)
        {
            var methodName = rpcMatch.Groups["name"].Value;

            var requestType = rpcMatch.Groups["req"].Value;

            var responseType = rpcMatch.Groups["res"].Value;

            var isClientStreaming = rpcMatch.Groups["reqStream"].Success;

            var isServerStreaming = rpcMatch.Groups["resStream"].Success;

            var kind = GetMethodKind(isClientStreaming, isServerStreaming);

            Console.WriteLine(
                $"[ProtoParser] RPC => Name={methodName}, Request={requestType}, Response={responseType}, Kind={kind}");

            methods.Add(new MethodModel(methodName, requestType, responseType, kind));
        }

        Console.WriteLine($"[ProtoParser] ===== Parse Content End =====");

        return new ServiceModel(firstService.ServiceName, csharpNamespace, protoFileName, methods);
    }

    private static IEnumerable<(string ServiceName, string Body)> ExtractServiceBlocks(string content)
    {
        var matches = ServiceStartRegex.Matches(content);

        foreach (Match match in matches)
        {
            var serviceName = match.Groups["name"].Value;

            var openBraceIndex = match.Index + match.Length - 1;

            var closeBraceIndex = FindMatchingBrace(content, openBraceIndex);

            if (closeBraceIndex < 0)
            {
                Console.WriteLine($"[ProtoParser] Invalid service block: {serviceName}");
                continue;
            }

            var bodyStart = openBraceIndex + 1;

            var bodyLength = closeBraceIndex - bodyStart;

            var body = content.Substring(bodyStart, bodyLength);

            Console.WriteLine($"[ProtoParser] Service block found: {serviceName}");

            yield return (serviceName, body);
        }
    }

    private static int FindMatchingBrace(string text, int openBraceIndex)
    {
        var depth = 0;

        for (var i = openBraceIndex; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static MethodKind GetMethodKind(bool isClientStreaming, bool isServerStreaming)
    {
        if (isClientStreaming && isServerStreaming)
        {
            return MethodKind.BiDi;
        }

        if (isClientStreaming)
        {
            return MethodKind.ClientStreaming;
        }

        if (isServerStreaming)
        {
            return MethodKind.ServerStreaming;
        }

        return MethodKind.Unary;
    }
}
