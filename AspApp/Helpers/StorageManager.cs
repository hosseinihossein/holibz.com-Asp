namespace AspApp.Helpers;

public class StorageManager
{
    public DirectoryInfo StorageDirInfo { get; }
    public DirectoryInfo UserAvatarDirInfo { get; }
    public DirectoryInfo LibraryImageDirInfo { get; }
    public DirectoryInfo ShelfImageDirInfo { get; }
    public DirectoryInfo DocumentImageDirInfo { get; }

    public StorageManager(IWebHostEnvironment env)
    {
        // Build the absolute paths sequentially
        string storagePath = Path.Combine(env.ContentRootPath, "Storage");
        string userAvatarPath = Path.Combine(storagePath, "UserAvatar");
        string libImagePath = Path.Combine(storagePath, "LibraryImage");
        string shelfImagePath = Path.Combine(storagePath, "ShelfImage");
        string docImagePath = Path.Combine(storagePath, "DocumentImage");

        // 1. Create and set permissions step-by-step
        // 2. Keep a reference to the target directory info
        StorageDirInfo = Directory.CreateDirectory(storagePath);
        UserAvatarDirInfo = Directory.CreateDirectory(userAvatarPath);
        LibraryImageDirInfo = Directory.CreateDirectory(libImagePath);
        ShelfImageDirInfo = Directory.CreateDirectory(shelfImagePath);
        DocumentImageDirInfo = Directory.CreateDirectory(docImagePath);
    }
}