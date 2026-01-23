// See https://aka.ms/new-console-template for more information

using StackExchange.Redis;

Console.WriteLine("Hello, World!");

ConnectionMultiplexer connectionMultiplexer = ConnectionMultiplexer.Connect("localhost");
IDatabase db = connectionMultiplexer.GetDatabase(0);

var result = db.HashGet("test", "test");
if (result.HasValue)
{
    Console.WriteLine(result);
}
else
{
    Console.WriteLine("null");
}

db.HashSet("test", [new HashEntry("test", "Hello, World!")]);
var result2 = db.HashGet("test", "test");
if (result2.HasValue)
{
    Console.WriteLine(result2);
}
else
{
    Console.WriteLine("null");
}

var session = new Session("123", 1, "test", DateTime.Now, DateTime.Now);
db.HashSet(session.SessionId, [new HashEntry("UserId", session.UserId), new HashEntry("UserName", session.UserName)]);
var sessionResult = db.HashGetAll(session.SessionId);
foreach (var item in sessionResult)
{
    Console.WriteLine($"{item.Name}: {item.Value}");
}

var userName = sessionResult.First(x => x.Name == "UserName").Value;
Console.WriteLine($"UserName is {userName}");

public class Session
{
    public string SessionId { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime LoginAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    
    private Session(){ }
    
    public Session(string sessionId, long userId, string userName, DateTime loginAt, DateTime lastActiveAt)
    {
        SessionId = sessionId;
        UserId = userId;
        UserName = userName;
        LoginAt = loginAt;
        LastActiveAt = lastActiveAt;
    }
}