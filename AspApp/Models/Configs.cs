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
    public string AppDb { get; }
    public string ProfileCollection { get; }
    public string LibraryCollection { get; }
    public string ShelfCollection { get; }
    public string DocumentCollection { get; }


    public MongoDbConfigs(IConfiguration configuration)
    {
        ConnectionString = configuration.GetValue<string>("MongoDb:ConnectionString")!;

        AppDb = configuration.GetValue<string>("MongoDb:AppDb")!;

        ProfileCollection = configuration.GetValue<string>("MongoDb:ProfileCollection")!;
        LibraryCollection = configuration.GetValue<string>("MongoDb:LibraryCollection")!;
        ShelfCollection = configuration.GetValue<string>("MongoDb:ShelfCollection")!;
        DocumentCollection = configuration.GetValue<string>("MongoDb:DocumentCollection")!;
    }
}

