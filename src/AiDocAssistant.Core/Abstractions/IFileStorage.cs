namespace AiDocAssistant.Core.Abstractions;

/// <summary>
/// Storage for original files. Currently local disk;
/// the interface allows adding S3/MinIO without changing business logic.
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default);

    /// <summary>Absolute path to a file by its StoragePath.</summary>
    string GetFullPath(string storagePath);
}

public sealed record StoredFile(string StoragePath, long SizeBytes);
