namespace AspApp.Helpers;

public class StorageManager
{
    public DirectoryInfo StorageDirInfo { get; }
    public DirectoryInfo UserAvatarDirInfo { get; }
    public DirectoryInfo LibraryImageDirInfo { get; }

    public StorageManager(IWebHostEnvironment env)
    {
        // Build the absolute paths sequentially
        string storagePath = Path.Combine(env.ContentRootPath, "Storage");
        string userAvatarPath = Path.Combine(storagePath, "UserAvatar");
        string libImagePath = Path.Combine(storagePath, "LibraryImage");

        // 1. Create and set permissions step-by-step
        // 2. Keep a reference to the target directory info
        StorageDirInfo = Directory.CreateDirectory(storagePath);
        UserAvatarDirInfo = Directory.CreateDirectory(userAvatarPath);
        LibraryImageDirInfo = Directory.CreateDirectory(libImagePath);
    }
}