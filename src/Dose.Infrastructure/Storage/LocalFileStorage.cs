using Dose.Application.Abstractions;

namespace Dose.Infrastructure.Storage;

/// <summary>
/// Development storage on local disk, outside the web root. Production swaps this for a private
/// Azure Blob container behind the same <see cref="IFileStorage"/> interface.
/// </summary>
public sealed class LocalFileStorage(string rootPath) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(rootPath);

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct)
    {
        if (extension is not (".jpg" or ".png" or ".webp")) throw new ArgumentException("Unsupported extension.", nameof(extension));
        var now = DateTime.UtcNow;
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    private string Resolve(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid file key.");
        return full;
    }
}
