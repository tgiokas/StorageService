namespace StorageService.Application.Errors;

public static class ErrorCodes
{
    public static class STORAGE
    {
        public const string GenericUnexpected = "STR-000";
        public const string BucketNotFound = "STR-001";
        public const string ObjectNotFound = "STR-002";
        public const string UploadFailed = "STR-003";
        public const string DownloadFailed = "STR-004";
        public const string DeleteFailed = "STR-005";
        public const string BucketCreationFailed = "STR-006";
        public const string InvalidKey = "STR-007";
        public const string InvalidBucket = "STR-008";
        public const string PresignedUrlFailed = "STR-009";
        public const string ListObjectsFailed = "STR-010";
        public const string MetadataRetrievalFailed = "STR-011";
        public const string ProviderNotConfigured = "STR-012";
        public const string ContentEmpty = "STR-013";
        public const string ContentTypeMissing = "STR-014";
    }
}
