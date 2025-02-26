using System.Collections;
using System.Data;
using System.Data.SqlClient;
using DotJEM.Json.Storage2.Cache;
using DotJEM.Json.Storage2.SqlServer.Initialization;

namespace DotJEM.Json.Storage2.SqlServer;

public class SqlServerStorageContextBuilder<TJson> : IStorageContextBuilder<TJson>
{
    private readonly string connectionString;
    private readonly IJsonConverter<TJson> converter;

    private string schema = "dbo";
    private IAuditInformationProvider auditInformationProvider;

    public SqlServerStorageContextBuilder(string connectionString, IJsonConverter<TJson> converter)
    {
        this.connectionString = connectionString;
        this.converter = converter;
    }

    public SqlServerStorageContextBuilder<TJson> ForSchema(string name)
    {
        this.schema = name;
        return this;
    }

    public SqlServerStorageContextBuilder<TJson> Use(IAuditInformationProvider auditInformationProvider)
    {
        this.auditInformationProvider = auditInformationProvider;
        return this;
    }

    public async Task<IStorageContext<TJson>> Build()
    {
        SqlServerConnectionFactory connectionFactory = new SqlServerConnectionFactory(connectionString);
        SqlServerStorageAreaFactory areaFactory = await CreateStorageAreaFactory(schema, connectionFactory);
        return new SqlServerStorageContext<TJson>(connectionFactory, areaFactory,
            auditInformationProvider ?? new DefaultAuditInformationProvider(), converter);
    }

    public static async Task<SqlServerStorageAreaFactory> CreateStorageAreaFactory(string schema, SqlServerConnectionFactory connectionFactory)
    {
        await using SqlConnection connection = connectionFactory.Create();
        await connection.OpenAsync().ConfigureAwait(false);

        await using SqlCommand command = new(SqlTemplates.SelectSchemaExists());
        command.Parameters.Add("schema", SqlDbType.NVarChar).Value = schema;
        command.Connection = connection;

        //TODO: Throw better exception.
        int schemaExists = (int)(await command.ExecuteScalarAsync().ConfigureAwait(false) ?? throw new Exception());
        if (schemaExists == 0)
        {
            //TODO: Needs to pass a state object to track creation of schema.
            return new SqlServerStorageAreaFactory(new SqlServerSchemaStateManager(connectionFactory, schema, false));
        }

        await using SqlCommand command2 = new(SqlTemplates.SelectTableNames());
        command2.Parameters.Add(new SqlParameter("schema", SqlDbType.NVarChar)).Value = schema;
        command2.Connection = connection;

        await using SqlDataReader reader = await command2.ExecuteReaderAsync().ConfigureAwait(false);


        HashSet<string> names = new();
        while (await reader.ReadAsync())
        {
            string tableName = reader.GetString(0);
            string area = tableName.Substring(0, tableName.LastIndexOf('.'));
            names.Add(area);
        }

        Dictionary<string, AreaInfo> areas = names
            .ToDictionary(name => name, name =>
                new AreaInfo(name, new SqlServerAreaStateManager(connectionFactory, schema, name, true)));

        return new SqlServerStorageAreaFactory(new SqlServerSchemaStateManager(connectionFactory, schema, true), areas);

        async Task<bool> SchemaExists()
        {
            await using SqlCommand command = new(SqlTemplates.SelectSchemaExists());
            command.Parameters.Add("schema", SqlDbType.NVarChar).Value = schema;
            command.Connection = connection;
            //TODO: Throw better exception.
            return 0 == (int)(await command.ExecuteScalarAsync().ConfigureAwait(false) ?? throw new Exception());
        }

    }
}



public interface IAuditInformationProvider
{
    string UserName { get; }
}

public class DefaultAuditInformationProvider : IAuditInformationProvider
{
    public string UserName => Environment.UserName;
}

public interface IJsonConverter<TJson>
{
    TJson Parse(string json);
    string ToString(TJson document, bool indent = false);
}

public class SqlServerStorageContext<TJson> : IStorageContext<TJson>
{
    private readonly AsyncCache<SqlServerStorageArea<TJson>> areas = new();
    private readonly SqlServerStorageAreaFactory areaFactory;

    public ISqlServerConnectionFactory ConnectionFactory { get; }
    public ISqlServerCommandFactory CommandFactory { get; }
    public IAuditInformationProvider AuditInformation { get; }
    public IJsonConverter<TJson> JsonConverter { get; }

    public SqlServerStorageContext(
        SqlServerConnectionFactory connectionFactory,
        SqlServerStorageAreaFactory areaFactory,
        IAuditInformationProvider auditInformation,
        IJsonConverter<TJson> jsonConverter)
    {
        this.areaFactory = areaFactory;
        ConnectionFactory = connectionFactory;
        CommandFactory = new SqlServerCommandFactory(connectionFactory);
        AuditInformation = auditInformation;
        JsonConverter = jsonConverter;
    }

    public async Task<IStorageArea<TJson>> AreaAsync(string name)
    {
        return await areas.GetOrAdd(name, key => areaFactory.Create(key, this));
    }

    public bool Release(string name)
    {
        return areas.Release(name);
    }

    public SqlConnection CreateConnection() => ConnectionFactory.Create();
}

public class SqlServerConnectionFactory : ISqlServerConnectionFactory
{
    private readonly string connectionString;

    public SqlServerConnectionFactory(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public SqlConnection Create() => new(connectionString);
}
public interface ISqlServerCommandFactory
{
    ISqlServerCommand Create(string commandText, params Parameter[] parameters);
}
public class SqlServerCommandFactory : ISqlServerCommandFactory
{
    private readonly ISqlServerConnectionFactory connectionFactory;

    public SqlServerCommandFactory(ISqlServerConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public ISqlServerCommand Create(string commandText, params Parameter[] parameters)
    {
        SqlConnection connection = connectionFactory.Create();
        SqlCommand command = new(commandText, connection);
        foreach ((string name, SqlDbType sqlDbType, object value) in parameters)
            command.Parameters.Add(name, sqlDbType).Value = value;
        return new SqlServerCommand(connection, command);
    }
}

public interface ISqlServerCommand : IDisposable
{
    Task<T> ExecuteScalarAsync<T>(CancellationToken cancellationToken);
    Task<ISqlServerDataReader<T>> ExecuteReaderAsync<T>(string[] columns, Func<object[], T> factory, CancellationToken cancellationToken);
}

public class SqlServerCommand : ISqlServerCommand
{
    // ReSharper disable once NotAccessedField.Local
    private readonly SqlConnection connection;
    private readonly SqlCommand command;

    public SqlServerCommand(SqlConnection connection, SqlCommand command)
    {
        this.connection = connection;
        this.command = command;
    }

    public async Task<T> ExecuteScalarAsync<T>(CancellationToken cancellationToken)
    {
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlTransaction transaction = connection.BeginTransaction();
        command.Transaction = transaction;
        T value = (T)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException());
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return value;
    }

    public async Task<ISqlServerDataReader<T>> ExecuteReaderAsync<T>(string[] columns, Func<object[], T> factory, CancellationToken cancellationToken)
    {
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        SqlDataReader? reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return new SqlServerDataReader<T>(columns, factory, reader);
    }

    public void Dispose()
    {
        connection.Dispose();
        command.Dispose();
    }
}


public readonly record struct Parameter(string Name, SqlDbType Type, object value)
{
    public static implicit operator Parameter((string name, int value) tuple)
        => new(tuple.name, SqlDbType.Int, tuple.value);

    public static implicit operator Parameter((string name, long value) tuple)
        => new(tuple.name, SqlDbType.BigInt, tuple.value);

    public static implicit operator Parameter((string name, string value) tuple)
        => new(tuple.name, SqlDbType.NVarChar, tuple.value);

    public static implicit operator Parameter((string name, DateTime value) tuple)
        => new(tuple.name, SqlDbType.DateTime, tuple.value);

    public static implicit operator Parameter((string name, Guid value) tuple)
        => new(tuple.name, SqlDbType.UniqueIdentifier, tuple.value);

    public static implicit operator Parameter((string name, byte[] value) tuple)
        => new(tuple.name, SqlDbType.VarBinary, tuple.value);

}


public interface ISqlServerDataReader<out T> : IDisposable, IEnumerable<T>, IAsyncEnumerable<T>
{
}

public class SqlServerDataReader<T> : ISqlServerDataReader<T>
{
    private readonly string[] columns;
    private readonly Func<object[], T> factory;
    private readonly SqlDataReader reader;

    public SqlServerDataReader(string[] columns, Func<object[], T> factory, SqlDataReader reader)
    {
        this.columns = columns;
        this.factory = factory;
        this.reader = reader;
    }

    public IEnumerator<T> GetEnumerator()
    {
        int[] c = columns.Select(name => reader.GetOrdinal(name)).ToArray();
        while (reader.Read())
        {
            //TODO: Better way of handling column specs as this is surely horrible performance, but function first!.
            object[] values = new object[c.Length];
            for (int i = 0; i < c.Length; i++)
                values[i] = reader.GetValue(c[i]);
            yield return factory(values);
        }
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
    {
        int[] c = columns.Select(name => reader.GetOrdinal(name)).ToArray();
        while (await reader.ReadAsync(cancellationToken))
        {
            //TODO: Better way of handling column specs as this is surely horrible performance, but function first!.
            object[] values = new object[c.Length];
            for (int i = 0; i < c.Length; i++)
                values[i] = reader.GetValue(c[i]);
            yield return factory(values);
        }
    }

    /// <inheritdoc />
    public void Dispose() => reader.Dispose();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}