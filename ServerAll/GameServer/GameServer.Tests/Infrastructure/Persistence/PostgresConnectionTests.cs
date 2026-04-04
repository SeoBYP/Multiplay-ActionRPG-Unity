using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GameServer.Tests.Infrastructure.Persistence;

public class PostgresConnectionTests
{
    [Fact]
    public async Task appsettings의_GameDb_연결문자열로_PostgreSQL에_연결할_수_있다()
    {
        var connectionString = LoadGameDbConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);

        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";

        var result = await command.ExecuteScalarAsync();

        Assert.NotNull(result);
        Assert.Equal(1, Convert.ToInt32(result));
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    private static string LoadGameDbConnectionString()
    {
        var apiProjectPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GameServer.API"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("GameDb");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "GameDb 연결 문자열을 찾지 못했습니다.");

        return connectionString!;
    }
}
