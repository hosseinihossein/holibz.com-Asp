using AspApp.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AspApp.Helpers;

public class UserProfileManager
{
    private readonly IMongoCollection<BsonDocument> _mongoCollection;
    public UserProfileManager(
        IMongoClient mongoClient,
        MongoDbConfigs mongoDbConfigs
    )
    {
        IMongoDatabase userAccountDb = mongoClient.GetDatabase(mongoDbConfigs.UserAccountDb);
        _mongoCollection = userAccountDb.GetCollection<BsonDocument>(mongoDbConfigs.ProfileCollection);
    }

    public async Task SubmitNewUserAccount(NewUserProfileModel model)
    {
        //create profile
        await CreateProfile(model);
    }

    private async Task CreateProfile(NewUserProfileModel model)
    {
        //create user profile document on mongodb
        var bsonDoc = new BsonDocument
        {
            {"_id", new BsonBinaryData(model.Id, GuidRepresentation.Standard)},
            { "createdAt", new BsonDateTime(model.CreatedAt) },
        };
        if (!string.IsNullOrWhiteSpace(model.AvatarUrl))
        {
            bsonDoc.Add("avatarUrl", new BsonString(model.AvatarUrl));
        }
        await _mongoCollection.InsertOneAsync(bsonDoc);
    }


}

public class NewUserProfileModel
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
