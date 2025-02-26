namespace DotJEM.Json.Storage2;

/// <summary>
/// 
/// </summary>
public interface IStorageContext<TJson>
{
    Task<IStorageArea<TJson>> AreaAsync(string name);

    bool Release(string name);
}
/// <summary>
/// 
/// </summary>
public interface IStorageContextBuilder<TJson>
{
    Task<IStorageContext<TJson>> Build();
}