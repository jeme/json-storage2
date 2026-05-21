namespace DotJEM.Json.Storage2;


public readonly record struct StorageChange<TJson>(long Revision, Guid Id, char Event, int Version, DateTime Time, string User, TJson Data)
{

}

/// <summary/>
public readonly record struct StorageObject<TJson>(string ContentType, Guid Id, int Version, DateTime Created, DateTime Updated, string CreatedBy,
    string UpdatedBy, TJson Data) {

    /// <summary/>
    public override string ToString()
    {
        return $"{ContentType}/{Id} Version={Version}, Created={Created:O} By={CreatedBy}, Updated={Updated:O} By={UpdatedBy}" +
               $"{Environment.NewLine}{Data}";
    }

    /// <summary>
    /// Converts <see cref="StorageObject"/> into an <see cref="UpdateStorageObject"/>
    /// that can be used with <see cref="IStorageArea.UpdateAsync(UpdateStorageObject)"/>
    /// </summary>
    /// <remarks>
    /// When converting to an update object it only includes the <see cref="ContentType"/>, <see cref="Id"/> and <see cref="Data"/>
    /// properties, and leaves the rest for the <see cref="IStorageArea"/> to manage.
    /// </remarks>
    public static implicit operator UpdateStorageObject<TJson>(StorageObject<TJson> o)
        => new (o.ContentType, o.Id, o.Data);

    /// <summary>
    /// Converts <see cref="StorageObject"/> into an <see cref="InsertStorageObject"/>
    /// that can be used with <see cref="IStorageArea.InsertAsync(InsertStorageObject)"/>
    /// </summary>
    /// <remarks>
    /// When converting to an update object it only includes the <see cref="ContentType"/> and <see cref="Data"/>
    /// properties, and leaves the rest for the <see cref="IStorageArea"/> to manage.
    /// </remarks>
    public static implicit operator InsertStorageObject<TJson>(StorageObject<TJson> o)
        => new (o.ContentType, o.Data);
}

/// <summary>
/// 
/// </summary>
/// <param name="ContentType"></param>
/// <param name="Id"></param>
/// <param name="Data"></param>
/// <param name="Updated"></param>
/// <param name="UpdatedBy"></param>
public readonly record struct UpdateStorageObject<TJson>(string ContentType, Guid Id, TJson Data, DateTime? Updated = null, string? UpdatedBy = null);

/// <summary>
/// 
/// </summary>
/// <param name="ContentType"></param>
/// <param name="Data"></param>
/// <param name="Created"></param>
/// <param name="CreatedBy"></param>
public readonly record struct InsertStorageObject<TJson>(string ContentType, TJson Data, DateTime? Created = null, string? CreatedBy = null);

/// <summary>
/// 
/// </summary>
/// <param name="ContentType"></param>
/// <param name="Id"></param>
/// <param name="Data"></param>
/// <param name="Updated"></param>
/// <param name="UpdatedBy"></param>
public readonly record struct DeleteStorageObject(string ContentType, Guid Id, DateTime? Updated = null, string? UpdatedBy = null);
