namespace AspApp.Helpers;

public class StorageManager
{
    public DirectoryInfo StorageDirInfo { get; }
    public DirectoryInfo UserAvatarDirInfo { get; }
    public DirectoryInfo LibraryImageDirInfo { get; }

    public StorageManager(IWebHostEnvironment env)
    {
        try
        {
            // Build the absolute paths sequentially
            string storagePath = Path.Combine(env.ContentRootPath, "Storage");
            string userAvatarPath = Path.Combine(storagePath, "UserAvatar");
            string libImagePath = Path.Combine(storagePath, "LibraryImage");

            // 1. Create and set permissions step-by-step
            // 2. Keep a reference to the target directory info
            StorageDirInfo = EnsureSecureDirectory(storagePath);
            UserAvatarDirInfo = EnsureSecureDirectory(userAvatarPath);
            LibraryImageDirInfo = EnsureSecureDirectory(libImagePath);
        }
        catch// (Exception ex)
        {
            Console.WriteLine("** Critical failure initializing file system storage folders.");
            throw; // Re-throw on initialization failure so the container crashes early if storage is misconfigured
        }
    }

    private DirectoryInfo EnsureSecureDirectory(string path)
    {
        var dirInfo = Directory.CreateDirectory(path);
        return dirInfo;
        /*if (OperatingSystem.IsWindows())
        {
            var dirInfo = Directory.CreateDirectory(path);
            return dirInfo;
        }
        else
        {
            // Forces explicit 'drwxr-xr-x' (755) permissions
            var dirUnixPermission =
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            // This is a no-op if the directory already exists
            var dirInfo = Directory.CreateDirectory(path, dirUnixPermission);
            return dirInfo;
        }*/
    }
}