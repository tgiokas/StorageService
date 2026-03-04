namespace StorageService.Application.Errors;

public static class ErrorCodes
{
    public static class STORAGE
    {
        public const string GenericUnexpected = "STR-000";
        public const string ErrorInErrorCodes = "STR-001";
        public const string BucketNotFound = "STR-002";
        public const string ObjectNotFound = "STR-003";
        public const string UploadFailed = "STR-004";
        public const string DownloadFailed = "STR-005";
        public const string DeleteFailed = "STR-006";
        public const string BucketCreationFailed = "STR-007";
        public const string InvalidKey = "STR-008";
        public const string InvalidBucket = "STR-009";
        public const string PresignedUrlFailed = "STR-010";
        public const string ListObjectsFailed = "STR-011";
        public const string MetadataRetrievalFailed = "STR-012";
        public const string ProviderNotConfigured = "STR-013";
        public const string ContentEmpty = "STR-014";
        public const string ContentTypeMissing = "STR-015";
        public const string EncryptionFailed = "STR-016";
        public const string DecryptionFailed = "STR-017";
        public const string EncryptionKeyMissing = "STR-018";
        public const string IndexEntryNotFound = "STR-019";
        public const string IndexQueryFailed = "STR-020";
        public const string IndexUpdateFailed = "STR-021";
        public const string IndexDeleteFailed = "STR-022";
    }
}