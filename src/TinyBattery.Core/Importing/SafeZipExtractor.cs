using System.IO.Compression;

namespace TinyBattery.Core.Importing;

public sealed class SafeZipExtractor
{
    public async Task<IReadOnlyList<string>> ExtractAsync(
        string zipPath,
        string destination,
        IReadOnlySet<string> allowedExtensions,
        CancellationToken ct,
        long maxFileBytes = 10L * 1024 * 1024,
        long maxTotalBytes = 50L * 1024 * 1024,
        int maxEntries = 2048)
    {
        Directory.CreateDirectory(destination);

        var destinationFull = Path.GetFullPath(destination)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var extracted = new List<string>();
        long totalBytes = 0;
        var entryCount = 0;

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            entryCount++;
            if (entryCount > maxEntries)
            {
                throw new InvalidDataException(
                    $"The archive contains more than {maxEntries} entries.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var relative = entry.FullName.Replace('\\', '/');
            if (Path.IsPathRooted(relative)
                || relative.Split('/').Any(segment => segment is ".." or ".")
                || relative.Contains(':', StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Archive entry '{entry.FullName}' has an unsafe path.");
            }

            var target = Path.GetFullPath(Path.Combine(destination, relative));
            if (!target.StartsWith(destinationFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Archive entry '{entry.FullName}' escapes the destination.");
            }

            var extension = Path.GetExtension(target).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidDataException(
                    $"Archive entry '{entry.FullName}' has a disallowed extension '{extension}'.");
            }

            if ((entry.ExternalAttributes & 0xF000) == 0xA000)
            {
                throw new InvalidDataException(
                    $"Archive entry '{entry.FullName}' is a symbolic link.");
            }

            if (entry.Length > maxFileBytes)
            {
                throw new InvalidDataException(
                    $"Archive entry '{entry.FullName}' exceeds {maxFileBytes} bytes.");
            }

            totalBytes += entry.Length;
            if (totalBytes > maxTotalBytes)
            {
                throw new InvalidDataException(
                    $"The archive expands beyond {maxTotalBytes} bytes.");
            }

            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var input = entry.Open();
            await using var output = File.Create(target);
            await input.CopyToAsync(output, ct);
            extracted.Add(target);
        }

        return extracted;
    }
}
