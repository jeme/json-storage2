namespace DotJEM.Json.Storage2;

public interface IStorageArea<TJson>
{
    string Name { get; }

    IAsyncEnumerable<StorageObject<TJson>> GetAsync();
    IAsyncEnumerable<StorageObject<TJson>> GetAsync(CancellationToken cancellation);
    IAsyncEnumerable<StorageObject<TJson>> GetAsync(long skip);
    IAsyncEnumerable<StorageObject<TJson>> GetAsync(long skip, CancellationToken cancellation);
    IAsyncEnumerable<StorageObject<TJson>> GetAsync(long skip, int take, CancellationToken cancellation);

    Task<StorageObject<TJson>?> GetAsync(Guid id);
    Task<StorageObject<TJson>?> GetAsync(Guid id, CancellationToken cancellation);
    Task<StorageObject<TJson>> InsertAsync(string contentType, TJson obj);
    Task<StorageObject<TJson>> InsertAsync(string contentType, TJson obj, CancellationToken cancellation);
    Task<StorageObject<TJson>> InsertAsync(InsertStorageObject<TJson> obj);
    Task<StorageObject<TJson>> InsertAsync(InsertStorageObject<TJson> obj, CancellationToken cancellation);
    Task<StorageObject<TJson>> UpdateAsync(Guid id, TJson obj);
    Task<StorageObject<TJson>> UpdateAsync(Guid id, TJson obj, CancellationToken cancellation);
    Task<StorageObject<TJson>> UpdateAsync(UpdateStorageObject<TJson> obj);
    Task<StorageObject<TJson>> UpdateAsync(UpdateStorageObject<TJson> obj, CancellationToken cancellation);
    Task<StorageObject<TJson>?> DeleteAsync(Guid id);
    Task<StorageObject<TJson>?> DeleteAsync(Guid id, CancellationToken cancellation);
}

public readonly record struct ChangeCount(int Created, int Updated, int Deleted)
{
    public int Total { get; } = Created + Updated + Deleted;

    public static ChangeCount operator +(ChangeCount left, ChangeCount right)
    {
        return new ChangeCount(
            left.Created + right.Created,
            left.Updated + right.Updated,
            left.Deleted + right.Deleted);
    }

    public static implicit operator int(ChangeCount count)
    {
        return count.Total;
    }

    public override string ToString()
    {
        return $"Created: {Created}, Updated: {Updated}, Deleted: {Deleted}";
    }
}

public interface IStorageAreaChangeCollection<TJson>
{

}

public interface IStorageAreaLog<TJson>
{
    
}

public interface IStorageAreaLogObserver<TJson>
{

}