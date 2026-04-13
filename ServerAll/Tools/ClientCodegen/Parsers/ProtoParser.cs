using System.Text.RegularExpressions;
using ClientCodegen.Models;

namespace ClientCodegen.Parsers;

public static class ProtoParser
{
    // option csharp_namespace = "..." ; 패턴을 찾는다.
    private static readonly Regex CsharpNamespaceRegex =
        new(@"option\s+csharp_namespace\s*=\s*""(?<namespace>[^""]+)""\s*;");

    // service ServiceName { 패턴을 찾는다.
    private static readonly Regex ServiceStartRegex =
        new(@"service\s+(?<name>\w+)\s*\{");

    // rpc Name(Request) returns (Response); 패턴을 찾는다. (Body { ... } 도 허용)
    private static readonly Regex RpcRegex =
        new(@"rpc\s+(?<name>\w+)\s*\(\s*(?<reqStream>stream\s+)?(?<req>\w+)\s*\)\s*returns\s*\(\s*(?<resStream>stream\s+)?(?<res>\w+)\s*\)\s*(?:;|\{[^}]*\})");

    // proto 파일 경로를 받아 파일 내용을 읽고 실제 파싱 함수로 넘긴다.
    public static ServiceModel? Parse(string protoPath)
    {
        // 현재 어떤 파일을 파싱하는지 로그를 남긴다.
        Console.WriteLine($"[ProtoParser] ===== Parse File Start =====");
        Console.WriteLine($"[ProtoParser] Path: {protoPath}");

        // 파일 전체 텍스트를 읽는다.
        var content = File.ReadAllText(protoPath);

        // 확장자를 제거한 파일명을 proto 파일 이름으로 사용한다.
        var protoFileName = Path.GetFileNameWithoutExtension(protoPath);

        // 실제 문자열 파싱 메서드를 호출한다.
        return ParseContent(content, protoFileName);
    }

    // proto 텍스트 내용을 ServiceModel 하나로 파싱한다.
    public static ServiceModel? ParseContent(string content, string protoFileName)
    {
        // 문자열 파싱 시작 로그를 남긴다.
        Console.WriteLine($"[ProtoParser] ===== Parse Content Start =====");
        Console.WriteLine($"[ProtoParser] Proto: {protoFileName}");

        // csharp_namespace 선언을 찾는다.
        var namespaceMatch = CsharpNamespaceRegex.Match(content);

        // namespace가 없으면 이후 생성 코드 네임스페이스를 알 수 없으므로 null을 반환한다.
        if (!namespaceMatch.Success)
        {
            Console.WriteLine($"[ProtoParser] csharp_namespace not found.");
            return null;
        }

        // 캡처된 namespace 값을 꺼낸다.
        var csharpNamespace = namespaceMatch.Groups["namespace"].Value;

        // 추출된 namespace를 로그로 남긴다.
        Console.WriteLine($"[ProtoParser] Namespace: {csharpNamespace}");

        // proto 내용에서 service 블록들을 추출한다.
        var serviceBlocks = ExtractServiceBlocks(content).ToList();

        // 현재 구현은 첫 번째 service만 처리한다.
        var firstService = serviceBlocks.FirstOrDefault();

        // service가 없으면 파싱할 대상이 없으므로 null을 반환한다.
        if (string.IsNullOrWhiteSpace(firstService.ServiceName))
        {
            Console.WriteLine($"[ProtoParser] service block not found.");
            return null;
        }

        // service 이름을 로그로 남긴다.
        Console.WriteLine($"[ProtoParser] Service: {firstService.ServiceName}");

        // 파싱된 메서드들을 저장할 리스트를 만든다.
        var methods = new List<MethodModel>();

        // service 본문 안에서 rpc 선언을 전부 찾는다.
        var rpcMatches = RpcRegex.Matches(firstService.Body);

        // rpc 개수를 로그로 남긴다.
        Console.WriteLine($"[ProtoParser] RPC Count: {rpcMatches.Count}");

        // 각 rpc 선언을 순회하면서 MethodModel로 변환한다.
        foreach (Match rpcMatch in rpcMatches)
        {
            // rpc 이름을 읽는다.
            var methodName = rpcMatch.Groups["name"].Value;

            // 요청 타입 이름을 읽는다.
            var requestType = rpcMatch.Groups["req"].Value;

            // 응답 타입 이름을 읽는다.
            var responseType = rpcMatch.Groups["res"].Value;

            // 요청이 stream인지 확인한다.
            var isClientStreaming = rpcMatch.Groups["reqStream"].Success;

            // 응답이 stream인지 확인한다.
            var isServerStreaming = rpcMatch.Groups["resStream"].Success;

            // 요청/응답의 stream 조합으로 MethodKind를 계산한다.
            var kind = GetMethodKind(isClientStreaming, isServerStreaming);

            // 메서드 파싱 결과를 한 줄 로그로 남긴다.
            Console.WriteLine(
                $"[ProtoParser] RPC => Name={methodName}, Request={requestType}, Response={responseType}, Kind={kind}");

            // MethodModel을 생성해 리스트에 추가한다.
            methods.Add(new MethodModel(methodName, requestType, responseType, kind));
        }

        // 최종 ServiceModel 생성 직전 로그를 남긴다.
        Console.WriteLine($"[ProtoParser] ===== Parse Content End =====");

        // 파싱 결과를 ServiceModel로 묶어서 반환한다.
        return new ServiceModel(firstService.ServiceName, csharpNamespace, protoFileName, methods);
    }

    // proto 텍스트 전체에서 service 블록들을 찾아 반환한다.
    private static IEnumerable<(string ServiceName, string Body)> ExtractServiceBlocks(string content)
    {
        // service 시작 패턴을 전부 찾는다.
        var matches = ServiceStartRegex.Matches(content);

        // service 선언을 하나씩 순회한다.
        foreach (Match match in matches)
        {
            // 캡처 그룹에서 service 이름을 꺼낸다.
            var serviceName = match.Groups["name"].Value;

            // 현재 service 선언의 여는 중괄호 위치를 계산한다.
            var openBraceIndex = match.Index + match.Length - 1;

            // 여는 중괄호와 짝이 되는 닫는 중괄호 위치를 찾는다.
            var closeBraceIndex = FindMatchingBrace(content, openBraceIndex);

            // 닫는 중괄호를 못 찾았으면 잘못된 proto이므로 건너뛴다.
            if (closeBraceIndex < 0)
            {
                Console.WriteLine($"[ProtoParser] Invalid service block: {serviceName}");
                continue;
            }

            // service 본문 시작 위치는 여는 중괄호 다음 문자다.
            var bodyStart = openBraceIndex + 1;

            // service 본문 길이는 닫는 중괄호 이전까지다.
            var bodyLength = closeBraceIndex - bodyStart;

            // service 본문 문자열을 잘라낸다.
            var body = content.Substring(bodyStart, bodyLength);

            // 어떤 service 블록을 찾았는지 로그를 남긴다.
            Console.WriteLine($"[ProtoParser] Service block found: {serviceName}");

            // service 이름과 본문을 튜플로 반환한다.
            yield return (serviceName, body);
        }
    }

    // 여는 중괄호 위치를 기준으로 닫는 중괄호 위치를 찾는다.
    private static int FindMatchingBrace(string text, int openBraceIndex)
    {
        // 현재 중괄호 깊이를 저장한다.
        var depth = 0;

        // 여는 중괄호 위치부터 문자열 끝까지 순회한다.
        for (var i = openBraceIndex; i < text.Length; i++)
        {
            // 여는 중괄호를 만나면 깊이를 증가시킨다.
            if (text[i] == '{')
            {
                depth++;
            }
            // 닫는 중괄호를 만나면 깊이를 감소시킨다.
            else if (text[i] == '}')
            {
                depth--;

                // 깊이가 0이면 시작 중괄호와 짝이 맞는 위치를 찾은 것이다.
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        // 끝까지 찾지 못했으면 실패를 의미하는 -1을 반환한다.
        return -1;
    }

    // 요청/응답 stream 여부를 MethodKind로 변환한다.
    private static MethodKind GetMethodKind(bool isClientStreaming, bool isServerStreaming)
    {
        // 요청과 응답이 모두 stream이면 BiDi다.
        if (isClientStreaming && isServerStreaming)
        {
            return MethodKind.BiDi;
        }

        // 요청만 stream이면 ClientStreaming이다.
        if (isClientStreaming)
        {
            return MethodKind.ClientStreaming;
        }

        // 응답만 stream이면 ServerStreaming이다.
        if (isServerStreaming)
        {
            return MethodKind.ServerStreaming;
        }

        // 둘 다 아니면 Unary다.
        return MethodKind.Unary;
    }
}
