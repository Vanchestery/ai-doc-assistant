using AiDocAssistant.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace AiDocAssistant.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.Root);
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        // Disk file name does not depend on user input — protection against path traversal
        var extension = Path.GetExtension(originalFileName);
        var storagePath = Path.Combine(
            DateTime.UtcNow.ToString("yyyy-MM"),
            $"{Guid.NewGuid():N}{extension}");

        var fullPath = Path.Combine(_root, storagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, ct);

        return new StoredFile(storagePath, file.Length);
    }

    public string GetFullPath(string storagePath) => Path.Combine(_root, storagePath);
}
