namespace Storage.Domain.Exceptions;

public class StorageDecryptionException : Exception
{
    public StorageDecryptionException(string message, Exception innerException)
        : base(message, innerException) { }
}
