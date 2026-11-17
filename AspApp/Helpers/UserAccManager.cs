using AspApp.Database;
using AspApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace AspApp.Helpers;

public class UserAccountManager
{
    private readonly IMongoCollection<BsonDocument> _profileCollection;
    readonly IWebHostEnvironment _env;
    readonly Application_DbContext _appDb;
    readonly BsonManager _bsonManager;
    readonly StorageManager _storageManager;
    public UserAccountManager(
        IMongoClient mongoClient,
        MongoDbConfigs mongoDbConfigs,
        IWebHostEnvironment env,
        Application_DbContext appDb,
        BsonManager bsonManager,
        StorageManager storageManager
    )
    {
        IMongoDatabase userAccountDb = mongoClient.GetDatabase(mongoDbConfigs.AppDb);
        _profileCollection = userAccountDb.GetCollection<BsonDocument>(mongoDbConfigs.ProfileCollection);

        _env = env;
        _appDb = appDb;
        _bsonManager = bsonManager;
        _storageManager = storageManager;
    }

    public async Task CreateNewUser(UserAccManager_NewUserModel model)
    {
        // postgres
        App_User_DbModel libUser = new()
        {
            Id = model.Id,
        };
        _appDb.Users.Add(libUser);
        await _appDb.SaveChangesAsync();

        // mongodb
        await CreateNewProfile(model);
    }
    private async Task CreateNewProfile(UserAccManager_NewUserModel model)
    {
        //create user profile document on mongodb
        var bsonDoc = new BsonDocument
        {
            {"_id", new BsonBinaryData(model.Id, GuidRepresentation.Standard)},
            { "createdAt", new BsonDateTime(DateTime.UtcNow) },
        };
        if (!string.IsNullOrWhiteSpace(model.AvatarUrl))
        {
            bsonDoc.Add("avatarUrl", new BsonString(model.AvatarUrl));
        }
        if (!string.IsNullOrWhiteSpace(model.Name))
        {
            bsonDoc.Add("name", new BsonString(model.Name));
        }
        await _profileCollection.InsertOneAsync(bsonDoc);
    }
    /*
        public async Task DeleteUser(Guid userId)
        {
            var user = await _appDb.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
            {
                return;
            }
            _appDb.Users.Remove(user);
            await _appDb.SaveChangesAsync();

            await DeleteProfile(userId);
        }

        private async Task DeleteProfile(Guid userId)
        {
            var deletingProfile = await _bsonManager.GetFirstBsonDoc(_profileCollection, userId, ["avatarUrl"]);
            if (deletingProfile is null)
            {
                return;
            }

            var filter = Builders<BsonDocument>.Filter
            .Eq("_id", new BsonBinaryData(userId, GuidRepresentation.Standard));
            var result = await _profileCollection.DeleteOneAsync(filter);

            if (deletingProfile.TryGetValue("avatarUrl", out BsonValue avataraUrlBsonValue))
            {
                string avatarUrl = avataraUrlBsonValue.AsString;
                DeleteAvatarFile(avatarUrl);
            }
        }
    */
    public async Task<string> SaveAvatarAndGetUrl(IFormFile imageFile)
    {
        string imageName = Guid.NewGuid().ToString("D") + ".webp";
        string imageFileFullPath = Path.Combine(_storageManager.UserAvatarDirInfo.FullName, imageName);
        using (FileStream fs = File.Create(imageFileFullPath))
        {
            await imageFile.CopyToAsync(fs);
        }

        return Path.Combine(
            "/" +
            _storageManager.StorageDirInfo.Name,
            _storageManager.UserAvatarDirInfo.Name,
            imageName
        );
    }

    public void DeleteAvatarFile(string avatarUrl)
    {
        string avatarFilePath = Path.Combine(_env.ContentRootPath, avatarUrl);

        if (File.Exists(avatarFilePath))
        {
            File.Delete(avatarFilePath);
        }
    }



}

public class UserAccManager_NewUserModel
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
}
