namespace Diaspora.Modules.Media.Persistence.Configurations;

/// <summary>
/// Table and column shapes shared by every Media configuration.
/// <para>
/// Named rather than repeated so a length change is one edit. The lengths themselves come from
/// the domain's own constants where one exists — a column narrower than the value object it
/// stores is a truncation nobody notices until a long filename arrives.
/// </para>
/// </summary>
internal static class MediaPersistenceConstants
{
    public const string AssetsTable = "media_assets";
    public const string VariantsTable = "media_variants";
    public const string GrantsTable = "media_grants";
    public const string UploadSessionsTable = "upload_sessions";
    public const string UploadChunksTable = "upload_chunks";
    public const string CollectionsTable = "media_collections";
    public const string CollectionItemsTable = "media_collection_items";
    public const string ProcessingJobsTable = "processing_jobs";
    public const string ProcessingTasksTable = "processing_tasks";

    public const int ContentTypeLength = 128;
    public const int FileNameLength = 260;
    public const int StorageKeyLength = 512;
    public const int StorageProviderLength = 64;
    public const int StorageContainerLength = 128;
    public const int ContentHashLength = 64;
    public const int RoleLength = 64;
    public const int EnumLength = 32;
    public const int ScannerLength = 64;
    public const int ThreatLength = 256;
    public const int AltTextLength = 500;
    public const int CaptionLength = 1000;
    public const int DetailLength = 500;
    public const int TransformationKeyLength = 128;
    public const int CollectionNameLength = 200;
}
