using System.Data.Common;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;


ConsoleApp.Run<MainCommands>(args);
public class MainCommands : ConsoleAppBase
{
    private readonly ILogger<MainCommands> logger;

    public MainCommands(ILogger<MainCommands> logger)
    {
        this.logger = logger;
    }
    [RootCommand]
    public async Task Root([Option("dp")] DbProvider db, [Option("cs")] string connectionString, [Option("s")] string[] sql, [Option("nt")] bool noTransaction)
    {
        using (var connection = ConnectionFactory(db, connectionString))
        {
            await connection.OpenAsync();
            await using var transaction = noTransaction ? null : await connection.BeginTransactionAsync(Context.CancellationToken);
            await using (var batch = BatchFactory(db, connection, transaction))
            {
                foreach (var x in sql)
                {
                    logger.LogTrace(x);
                    batch.BatchCommands.Add(CommandFactory(db, x));
                }
                try
                {
                    await batch.ExecuteNonQueryAsync();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Sql execute failed.");
                    throw;
                }
            }
        }
    }



    private DbConnection ConnectionFactory(DbProvider provider, string connectionString)
    => provider switch
    {
        DbProvider.PostgreSQL => new NpgsqlConnection(connectionString),
        DbProvider.MySQL => new MySqlConnection(connectionString),
        _ => throw new InvalidOperationException($"{nameof(DbProvider)}:{provider} is unknown.")
    };
    private DbBatch BatchFactory(DbProvider provider, DbConnection connection, DbTransaction? transaction)
    => provider switch
    {
        DbProvider.PostgreSQL => new NpgsqlBatch(connection as NpgsqlConnection, transaction as NpgsqlTransaction),
        DbProvider.MySQL => new MySqlBatch(connection as MySqlConnection, transaction as MySqlTransaction),
        _ => throw new InvalidOperationException($"{nameof(DbProvider)}:{provider} is unknown.")
    };
    private DbBatchCommand CommandFactory(DbProvider provider, string query)
    => provider switch
    {
        DbProvider.PostgreSQL => new NpgsqlBatchCommand(query),
        DbProvider.MySQL => new MySqlBatchCommand(query),
        _ => throw new InvalidOperationException($"{nameof(DbProvider)}:{provider} is unknown.")
    };

}


public enum DbProvider
{
    PostgreSQL,
    MySQL
}