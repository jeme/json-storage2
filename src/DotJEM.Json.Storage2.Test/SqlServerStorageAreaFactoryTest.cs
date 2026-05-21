using DotJEM.Json.Storage2.SqlServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DotJEM.Json.Storage2.Test;

public class NewtonsoftJsonConverter : IJsonConverter<JObject>
{
    public JObject Parse(string json)
    {
        return JObject.Parse(json);
    }

    public string ToString(JObject document, bool indent = false)
    {
        return document.ToString(indent ? Formatting.Indented : Formatting.None);
    }
}

public class SqlServerStorageContextIntegrationTest
{
    [Test]
    public async Task EnsureLogTable_NoTableExists_ShouldCreateTable()
    {
        IStorageContext<JObject> context = await new SqlServerStorageContextBuilder<JObject>(TestSqlConnectionFactory.ConnectionString, new NewtonsoftJsonConverter())
            .Build();

        IStorageArea<JObject> area = await context.AreaAsync("test");
        await area.InsertAsync("na", new JObject());



    }

    [Test]
    public async Task EnsureLogTable_NoTableExists_ShouldCreateTables()
    {
        IStorageContext<JObject> context = await new SqlServerStorageContextBuilder<JObject>(TestSqlConnectionFactory.ConnectionString, new NewtonsoftJsonConverter())
            .ForSchema("fox")
            .Build();

        IStorageArea<JObject> area = await context.AreaAsync("test");
        StorageObject<JObject> so = await area.InsertAsync("na",new JObject());

        Console.WriteLine(so);
        
        StorageObject<JObject>? so2 = await area.GetAsync(so.Id);
        Console.WriteLine(so2);

        StorageObject<JObject> so3 = await area.UpdateAsync(so.Id, JObject.FromObject(new { foo = "Fax" }));
        Console.WriteLine(so3);

        StorageObject<JObject> so4 = await area.UpdateAsync(so.Id, JObject.FromObject(new { foo = "Foo" }));
        Console.WriteLine(so4);

        StorageObject<JObject>? so5 = await area.GetAsync(so.Id);
        Console.WriteLine(so5);
    }

    [Test]
    public async Task GetAsync_NoTableExists_ShouldCreateTables()
    {
        IStorageContext<JObject> context = await new SqlServerStorageContextBuilder<JObject>(TestSqlConnectionFactory.ConnectionString, new NewtonsoftJsonConverter())
            .ForSchema("fox")
            .Build();
        IStorageArea<JObject> area = await context.AreaAsync("test");

        await area.InsertAsync("na", JObject.FromObject(new { track="T-01"}));
        await area.InsertAsync("na", JObject.FromObject(new { track= "T-02" }));
        await area.InsertAsync("na", JObject.FromObject(new { track= "T-03" }));
        await foreach (StorageObject<JObject> obj in area.GetAsync())
        {
            Console.WriteLine(obj);
        }
    }

    [Test]
    public async Task InsertAsync_Record_ShouldAddToTable()
    {
        IStorageContext<JObject> context = await new SqlServerStorageContextBuilder<JObject>(TestSqlConnectionFactory.ConnectionString, new NewtonsoftJsonConverter())
            .ForSchema("fox")
            .Build();

        IStorageArea<JObject> area = await context.AreaAsync("test");

        StorageObject<JObject> obj = await area.InsertAsync("na", JObject.FromObject(new { track = "T-01" }));
        StorageObject<JObject>? obj2 = await area.GetAsync(obj.Id);

        Assert.That(obj2, Is.Not.Null & Has.Property(nameof(StorageObject<JObject>.UpdatedBy)).EqualTo(obj.UpdatedBy));

    }

    [Test, Explicit]
    public async Task GetAsync_ChangeLog()
    {
        IStorageContext<JObject> context = await new SqlServerStorageContextBuilder<JObject>(TestSqlConnectionFactory.ConnectionString, new NewtonsoftJsonConverter())
            .ForSchema("fox")
            .Build();
        IStorageArea<JObject> area = await context.AreaAsync("test");


        await area.InsertAsync("na", JObject.FromObject(new { track = "T-01" }));
        await area.InsertAsync("na", JObject.FromObject(new { track = "T-02" }));
        await area.InsertAsync("na", JObject.FromObject(new { track = "T-03" }));
        await foreach (StorageObject<JObject> obj in area.GetAsync())
        {
            Console.WriteLine(obj);
        }
    }

}