namespace Storage.Domain.Exceptions;

public class StorageObjectNotFoundException : Exception
{
    public string Bucket { get; }
    public string Key { get; }

    public StorageObjectNotFoundException(string bucket, string key)
        : base($"Object '{key}' not found in bucket '{bucket}'.")
    {
        Bucket = bucket;
        Key = key;
    }
}