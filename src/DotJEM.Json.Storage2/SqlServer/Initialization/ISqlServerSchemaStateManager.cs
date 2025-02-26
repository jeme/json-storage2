using System.Data;
using System.Data.SqlClient;

namespace DotJEM.Json.Storage2.SqlServer.Initialization;

public interface ISqlServerSchemaStateManager
{
    string Schema { get; }

    Task<bool> Ensure();
}

public class SqlServerSchemaStateManager : ISqlServerSchemaStateManager
{
    private bool created;
    private readonly SqlServerConnectionFactory connectionFactory;
    private readonly SemaphoreSlim padlock = new(1, 1);
  
    public string Schema { get; }

    public SqlServerSchemaStateManager(SqlServerConnectionFactory connectionFactory, string schema, bool created)
    {
        this.connectionFactory = connectionFactory;
        this.Schema = schema;
        this.created = created;
    }

    public async Task<bool> Ensure()
    {
        if (created)
            return created;

        await padlock.WaitAsync();

        if (created)
            return created;

        string commandText = SqlTemplates.CreateSchema(Schema);

        await using SqlConnection connection = connectionFactory.Create();
        await using SqlCommand command = new SqlCommand(commandText, connection);
        await connection.OpenAsync().ConfigureAwait(false);

        await using SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted);
        command.Connection = connection;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);

        created = true;
        padlock.Release();

        return created;
    }


}

public class SqlServerAreaStateManager : ISqlServerSchemaStateManager
{
    private bool created;
    private readonly ISqlServerConnectionFactory connectionFactory;
    private readonly SemaphoreSlim padlock = new(1, 1);

    public string AreaName { get; }
    public bool Exists => created;
    public string Schema { get; }

    public SqlServerAreaStateManager(ISqlServerConnectionFactory connectionFactory, string schema, string area, bool created)
    {
        this.connectionFactory = connectionFactory;
        this.Schema = schema;
        this.AreaName = area;
        this.created = created;
    }


    public async Task<bool> Ensure()
    {
        if (created)
            return created;

        await padlock.WaitAsync();

        if (created)
            return created;

        string dataTableCommandText = SqlTemplates.CreateDataTable(Schema, AreaName);//  SqlServerStatements.Load("CreateDataTable", map);
        string logTableCommandText = SqlTemplates.CreateLogTable(Schema, AreaName); // SqlServerStatements.Load("CreateLogTable", map);
        string schemaTableCommandText = SqlTemplates.CreateSchemasTable(Schema, AreaName); // SqlServerStatements.Load("CreateSchemasTable", map);

        //await using SqlConnection connection = connectionFactory.Create();
        using SqlConnection connection = connectionFactory.Create();
        await connection.OpenAsync().ConfigureAwait(false);

        //await using SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted);
        using SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted);
        await Execute(dataTableCommandText, connection, transaction);
        await Execute(logTableCommandText, connection, transaction);
        await Execute(schemaTableCommandText, connection, transaction);
        //await transaction.CommitAsync().ConfigureAwait(false);
        transaction.Commit();

        created = true;
        padlock.Release();

        return created;
    }

    private async Task Execute(string commandText, SqlConnection connection, SqlTransaction transaction)
    {
        //await using SqlCommand command = new(commandText);
        using SqlCommand command = new(commandText);
        command.Connection = connection;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

}