namespace AspApp.Models;

public class MainConfigs
{
    public string? MainDomainName { get; set; }
}

public class TurnstileConfigs
{
    public bool Enabled { get; set; }
    public string SecretKey { get; set; } = string.Empty;
}

public class MongoDbConfigs
{
    public string ConnectionString { get; set; } = "mongodb://mongodb:27017";
    public string UserAccountDb { get; set; } = "UserAccount_Db";
    public string ProfileCollection { get; set; } = "Profile";
}