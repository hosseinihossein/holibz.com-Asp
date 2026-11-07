namespace AspApp.Models;

public class MainConfigs
{
    public string MainDomainName { get; }

    public MainConfigs(IConfiguration configuration)
    {
        MainDomainName = configuration.GetValue<string>("MainDomainName") ?? "https://holibz.com";
    }
}

public class TurnstileConfigs
{
    public bool Enabled { get; }
    public string SecretKey { get; }

    public TurnstileConfigs(IConfiguration configuration)
    {
        SecretKey = configuration.GetValue<string>("Turnstile:SecretKey") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(SecretKey))
        {
            Enabled = configuration.GetValue<bool>("Turnstile:Enabled");
        }
    }
}

public class MongoDbConfigs
{
    public string ConnectionString { get; }
    public string UserAccountDb { get; }
    public string ProfileCollection { get; }

    public string LibraryDb { get; }
    public string LibraryCollection { get; }


    public MongoDbConfigs(IConfiguration configuration)
    {
        ConnectionString = configuration.GetValue<string>("MongoDb:ConnectionString") ?? "mongodb://mongodb:27017";
        UserAccountDb = configuration.GetValue<string>("MongoDb:UserAccountDb") ?? "UserAccount_Db";
        ProfileCollection = configuration.GetValue<string>("MongoDb:ProfileCollection") ?? "Profile";

        LibraryDb = configuration.GetValue<string>("MongoDb:LibraryDb") ?? "Library_Db";
        LibraryCollection = configuration.GetValue<string>("MongoDb:LibraryCollection") ?? "Library";
    }
}

public class StorageConfigs
{
    readonly ILogger<StorageConfigs> _logger;
    public DirectoryInfo StorageDirInfo { get; }
    public DirectoryInfo UserProfileDirInfo { get; }
    public DirectoryInfo UserProfileAvatarDirInfo { get; }

    public StorageConfigs(IWebHostEnvironment env, ILogger<StorageConfigs> logger)
    {
        _logger = logger;

        try
        {
            // Build the absolute paths sequentially
            string storagePath = Path.Combine(env.ContentRootPath, "Storage");
            string userProfilePath = Path.Combine(storagePath, "UserProfile");
            string avatarPath = Path.Combine(userProfilePath, "Avatar");

            // 1. Create and set permissions step-by-step
            // 2. Keep a reference to the target directory info
            StorageDirInfo = EnsureSecureDirectory(storagePath);
            UserProfileDirInfo = EnsureSecureDirectory(userProfilePath);
            UserProfileAvatarDirInfo = EnsureSecureDirectory(avatarPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure initializing file system storage folders.");
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
                _logger.LogWarning("Failed setting Linux permissions for path {Path}: {Message}", path, e.Message);
            }
        }

        return dirInfo;
    }
}
