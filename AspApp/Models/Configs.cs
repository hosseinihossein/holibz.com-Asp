namespace AspApp.Models;

public class MainConfigs
{
    public string MainDomainName { get; }

    public MainConfigs(string mainDomainName)
    {
        MainDomainName = mainDomainName;
    }
}

public class TurnstileConfigs
{
    public bool Enabled { get; }
    public string SecretKey { get; }

    public TurnstileConfigs(string secretKey, bool enabled)
    {
        SecretKey = secretKey;
        Enabled = enabled;
    }
}

public class MongoDbConfigs
{
    public string ConnectionString { get; }
    public string UserAccountDb { get; }
    public string ProfileCollection { get; }

    public string LibraryDb { get; }
    public string LibraryCollection { get; }


    public MongoDbConfigs(string connectionString, string userAccDb, string profileCol, string libDb, string libCol)
    {
        ConnectionString = connectionString;
        UserAccountDb = userAccDb;
        ProfileCollection = profileCol;

        LibraryDb = libDb;
        LibraryCollection = libCol;
    }
}

/*public class StorageConfigs
{
    public DirectoryInfo StorageDirInfo { get; }
    public DirectoryInfo UserAvatarDirInfo { get; }
    public DirectoryInfo LibraryImageDirInfo { get; }

    public StorageConfigs(IWebHostEnvironment env)
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
        // This is a no-op if the directory already exists
        var dirInfo = Directory.CreateDirectory(path);

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                // Forces explicit 'drwxr-xr-x' (755) permissions
                File.SetUnixFileMode(path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute
                );
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed setting Linux permissions for path {path}: {e.Message}");
            }
        }

        return dirInfo;
    }
}*/
