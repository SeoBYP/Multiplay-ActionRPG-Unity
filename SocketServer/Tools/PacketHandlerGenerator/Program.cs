using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Tools
{
    class PacketHandlerGenerator
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: PacketHandlerGenerator <protocol.proto> <output_directory>");
                Console.WriteLine("Example: PacketHandlerGenerator protocol.proto ../Unity/Assets/Scripts/Network/Handlers/");
                return;
            }

            string protoPath = args[0];
            string outputDir = args[1];

            if (!File.Exists(protoPath))
            {
                Console.WriteLine($"File not found: {protoPath}");
                return;
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            Console.WriteLine("=== PacketHandler Generator v2 ===\n");
            Console.WriteLine($"Input:  {protoPath}");
            Console.WriteLine($"Output: {outputDir}\n");

            // protocol.proto 분석
            string protoContent = File.ReadAllText(protoPath);
            var packets = ExtractServerPackets(protoContent);

            if (packets.Count == 0)
            {
                Console.WriteLine("No server packets found (S_*)");
                return;
            }

            Console.WriteLine($"Found {packets.Count} server packets:");
            foreach (var packet in packets)
            {
                Console.WriteLine($"  - {packet}");
            }
            Console.WriteLine();

            // 패킷을 카테고리별로 그룹화
            var groups = GroupPackets(packets);

            Console.WriteLine($"Grouped into {groups.Count} categories:");
            foreach (var (category, categoryPackets) in groups)
            {
                Console.WriteLine($"  {category}: {string.Join(", ", categoryPackets)}");
            }
            Console.WriteLine();

            // Handlers 폴더 생성
            string handlersDir = Path.Combine(outputDir, "Handlers");
            if (!Directory.Exists(handlersDir))
            {
                Directory.CreateDirectory(handlersDir);
            }

            // Dispatcher 폴더 생성
            string dispatcherDir = Path.Combine(outputDir, "Dispatcher");
            if (!Directory.Exists(dispatcherDir))
            {
                Directory.CreateDirectory(dispatcherDir);
            }

            // 각 카테고리별로 Handler 생성
            int generatedCount = 0;
            foreach (var (category, categoryPackets) in groups)
            {
                string handlerCode = GeneratePacketHandler(category, categoryPackets);
                string handlerPath = Path.Combine(handlersDir, $"{category}PacketHandler.g.cs");
                File.WriteAllText(handlerPath, handlerCode);
                Console.WriteLine($"Generated: {category}PacketHandler.g.cs");
                generatedCount++;
            }

            // PacketDispatcher 생성
            string dispatcherCode = GeneratePacketDispatcher(groups);
            string dispatcherPath = Path.Combine(dispatcherDir, "PacketDispatcher.g.cs");
            File.WriteAllText(dispatcherPath, dispatcherCode);
            Console.WriteLine($"Generated: PacketDispatcher.g.cs");

            Console.WriteLine($"\n Total: {generatedCount} handlers + 1 dispatcher generated\n");
        }

        /// <summary>
        /// S_ 로 시작하는 서버 패킷 추출
        /// </summary>
        static List<string> ExtractServerPackets(string protoContent)
        {
            var packets = new List<string>();
            var regex = new Regex(@"message\s+(S_\w+)\s*{", RegexOptions.Multiline);
            var matches = regex.Matches(protoContent);

            foreach (Match match in matches)
            {
                packets.Add(match.Groups[1].Value);
            }

            return packets;
        }

        /// <summary>
        /// 패킷을 카테고리별로 그룹화
        /// 
        /// S_Chat → Chat
        /// S_CreateRoom → Room
        /// S_JoinRoom → Room
        /// S_Move → Game
        /// </summary>
        static Dictionary<string, List<string>> GroupPackets(List<string> packets)
        {
            var groups = new Dictionary<string, List<string>>();

            foreach (var packet in packets)
            {
                string category = GetCategory(packet);

                if (!groups.ContainsKey(category))
                {
                    groups[category] = new List<string>();
                }

                groups[category].Add(packet);
            }

            return groups;
        }

        /// <summary>
        /// 패킷 카테고리 추출
        /// 
        /// S_Chat → Chat
        /// S_CreateRoom → Room
        /// S_JoinRoom → Room
        /// S_LeaveRoom → Room
        /// S_Move → Game
        /// S_Attack → Game
        /// </summary>
        static string GetCategory(string packetName)
        {
            // S_ 제거
            string name = packetName.Substring(2); // S_Chat → Chat

            // 알려진 패턴 매칭
            if (name.Contains("Room"))
                return "Room";
            if (name.Contains("Chat"))
                return "Chat";
            if (name.Contains("Move") || name.Contains("Attack") || name.Contains("Skill"))
                return "Game";
            if (name.Contains("Login") || name.Contains("Auth"))
                return "Auth";

            // 기본값
            return "Common";
        }

        /// <summary>
        /// 개별 PacketHandler 생성
        /// 
        /// 예: ChatPacketHandler.g.cs
        /// </summary>
        static string GeneratePacketHandler(string category, List<string> packets)
        {
            var sb = new StringBuilder();

            // 헤더
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// This file is automatically generated by PacketHandlerGenerator.");
            sb.AppendLine("// DO NOT EDIT MANUALLY!");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine("using ServerCore.Protocol;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.Events;");
            sb.AppendLine();
            sb.AppendLine("namespace Game.Network.Handlers");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {category} 관련 패킷 핸들러");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {category}PacketHandler : PacketHandler");
            sb.AppendLine("    {");

            // Events
            sb.AppendLine("        #region Events");
            sb.AppendLine();

            foreach (var packet in packets)
            {
                string eventName = GetEventName(packet); // S_Chat → ChatReceived
                sb.AppendLine($"        public UnityEvent<{packet}> On{eventName} = new UnityEvent<{packet}>();");
            }

            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine();

            // Handlers
            sb.AppendLine("        #region Handlers");
            sb.AppendLine();

            foreach (var packet in packets)
            {
                string methodName = GetMethodName(packet); // S_Chat → HandleChat
                string eventName = GetEventName(packet);
                string logName = packet.Substring(2); // S_Chat → Chat
                
                // ⭐ 패킷 필드 이름: S_Chat → SChat (언더스코어 제거)
                string packetFieldName = packet.Replace("_", "");
                
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// {packet} 패킷 처리");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public void {methodName}(Packet packet)");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var data = packet.{packetFieldName};"); // packet.SChat
                sb.AppendLine($"            Debug.Log($\"[{category}Handler] {logName} received\");");
                sb.AppendLine($"            On{eventName}?.Invoke(data);");
                sb.AppendLine($"        }}");
                sb.AppendLine();
            }

            sb.AppendLine("        #endregion");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// PacketDispatcher 생성
        /// </summary>
        static string GeneratePacketDispatcher(Dictionary<string, List<string>> groups)
        {
            var sb = new StringBuilder();

            // 헤더
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// This file is automatically generated by PacketHandlerGenerator.");
            sb.AppendLine("// DO NOT EDIT MANUALLY!");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using Game.Network.Handlers;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using ServerCore.Protocol;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace Game.Network.Dispatcher");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 패킷 디스패처 (자동 생성)");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public class PacketDispatcher");
            sb.AppendLine("    {");

            // Handler Instances
            sb.AppendLine("        #region Handler Instances");
            sb.AppendLine();

            foreach (var (category, _) in groups)
            {
                sb.AppendLine($"        public {category}PacketHandler {category} {{ get; private set; }}");
            }

            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine();

            // Dictionary
            sb.AppendLine("        #region Dictionary");
            sb.AppendLine();
            sb.AppendLine("        private Dictionary<Packet.PayloadOneofCase, Action<Packet>> _handlers;");
            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine();

            // Constructor
            sb.AppendLine("        #region Initialization");
            sb.AppendLine();
            sb.AppendLine("        public PacketDispatcher()");
            sb.AppendLine("        {");
            sb.AppendLine("            // Handler 인스턴스 생성");

            foreach (var (category, _) in groups)
            {
                sb.AppendLine($"            {category} = new {category}PacketHandler();");
            }

            sb.AppendLine();
            sb.AppendLine("            // Dictionary 초기화");
            sb.AppendLine("            InitializeHandlers();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // InitializeHandlers
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 핸들러 딕셔너리 초기화 (자동 생성)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        private void InitializeHandlers()");
            sb.AppendLine("        {");
            sb.AppendLine("            _handlers = new Dictionary<Packet.PayloadOneofCase, Action<Packet>>");
            sb.AppendLine("            {");

            // Dictionary 항목 생성
            int totalCount = 0;
            foreach (var (category, packets) in groups)
            {
                foreach (var packet in packets)
                {
                    totalCount++;
                }
            }

            int currentIndex = 0;
            foreach (var (category, packets) in groups)
            {
                foreach (var packet in packets)
                {
                    string methodName = GetMethodName(packet);
                    
                    // ⭐ PayloadOneofCase enum 이름: S_Chat → SChat (언더스코어 제거)
                    string packetEnumName = packet.Replace("_", "");
                    
                    currentIndex++;
                    string comma = (currentIndex < totalCount) ? "," : "";
                    sb.AppendLine($"                [Packet.PayloadOneofCase.{packetEnumName}] = {category}.{methodName}{comma}");
                }
            }

            sb.AppendLine("            };");
            sb.AppendLine();
            sb.AppendLine($"            Debug.Log($\"[PacketDispatcher] {{_handlers.Count}} handlers registered\");");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine();

            // Dispatch
            sb.AppendLine("        #region Dispatch");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 패킷 디스패치");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public void Dispatch(Packet packet)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_handlers.TryGetValue(packet.PayloadCase, out var handler))");
            sb.AppendLine("            {");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine("                    handler(packet);");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception e)");
            sb.AppendLine("                {");
            sb.AppendLine("                    Debug.LogError($\"[PacketDispatcher] Error: {e.Message}\\n{e.StackTrace}\");");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogWarning($\"[PacketDispatcher] No handler for: {packet.PayloadCase}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// 메서드 이름 생성
        /// S_Chat → HandleChat
        /// </summary>
        static string GetMethodName(string packetName)
        {
            return "Handle" + packetName.Substring(2);
        }

        /// <summary>
        /// 이벤트 이름 생성
        /// S_Chat → ChatReceived
        /// </summary>
        static string GetEventName(string packetName)
        {
            return packetName.Substring(2) + "Received";
        }
    }
}