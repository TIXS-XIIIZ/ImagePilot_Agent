using ImagePilot.Api.Models;

namespace ImagePilot.Api.Services;

public sealed class FolderPickerService
{
    public FolderListResult List(string? requestedPath)
    {
        var drives = DriveInfo.GetDrives()
            .Where(drive => drive.IsReady)
            .Select(drive => new FolderEntry($"{drive.Name} {drive.VolumeLabel}".Trim(), drive.RootDirectory.FullName))
            .OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var currentPath = ResolveCurrentPath(requestedPath, drives);
        var directory = new DirectoryInfo(currentPath);
        var folders = directory.EnumerateDirectories()
            .Where(folder => !folder.Attributes.HasFlag(FileAttributes.Hidden) && !folder.Attributes.HasFlag(FileAttributes.System))
            .Select(folder => new FolderEntry(folder.Name, folder.FullName))
            .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FolderListResult(
            directory.FullName,
            directory.Parent?.FullName,
            drives,
            folders);
    }

    public FolderListResult CreateSubfolder(string currentPath, string folderName)
    {
        if (!Directory.Exists(currentPath))
        {
            throw new DirectoryNotFoundException("The current folder no longer exists.");
        }

        var cleanName = folderName.Trim();
        if (string.IsNullOrWhiteSpace(cleanName))
        {
            throw new ArgumentException("Enter a folder name.");
        }

        if (cleanName is "." or ".." ||
            cleanName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            cleanName.Contains(Path.DirectorySeparatorChar) ||
            cleanName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Folder name contains invalid characters.");
        }

        var parentPath = Path.GetFullPath(currentPath);
        var folderPath = Path.GetFullPath(Path.Combine(parentPath, cleanName));
        var parentPrefix = Path.EndsInDirectorySeparator(parentPath)
            ? parentPath
            : parentPath + Path.DirectorySeparatorChar;
        if (!folderPath.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Folder must be created inside the current location.");
        }

        if (Directory.Exists(folderPath))
        {
            throw new IOException("A folder with this name already exists.");
        }

        Directory.CreateDirectory(folderPath);
        return List(folderPath);
    }

    private static string ResolveCurrentPath(string? requestedPath, IReadOnlyList<FolderEntry> drives)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath) && Directory.Exists(requestedPath))
        {
            return Path.GetFullPath(requestedPath);
        }

        return drives.FirstOrDefault()?.Path ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
