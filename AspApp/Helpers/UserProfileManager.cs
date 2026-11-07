using AspApp.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AspApp.Helpers;

public class UserProfileManager
{
    private readonly IMongoCollection<BsonDocument> _userProfileCollection;
    public UserProfileManager(
        IMongoDatabase mongoUserDb,
        IOptions<MongoDbConfigs> mongoDbConfigsOptions
    )
    {
        _userProfileCollection = mongoUserDb.GetCollection<BsonDocument>(mongoDbConfigsOptions.Value.ProfileCollection);
    }

    public async Task SubmitNewUserAccount(NewUserAccountModel model)
    {
        //create profile
        await CreateProfile(model);
    }

    private async Task CreateProfile(NewUserAccountModel model)
    {
        //create user profile document on mongodb
        var bsonDoc = new BsonDocument
        {
            {"_id", new BsonBinaryData(model.Id, GuidRepresentation.Standard)},
            { "avatarUrl", new BsonString(model.AvatarUrl) },
            { "createdAt", new BsonDateTime(model.CreatedAt) },
        };
        await _userProfileCollection.InsertOneAsync(bsonDoc);
    }


}

public class NewUserAccountModel
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
