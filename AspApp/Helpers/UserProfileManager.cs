using AspApp.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.IO;
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

    public async Task<BsonDocument?> GetFirstBsonDoc(Guid userGuid, string[]? projectItems = null)
    {
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        IFindFluent<BsonDocument, BsonDocument> findFluent = _mongoCollection.Find(filter);

        if (projectItems is not null && projectItems.Length > 0)
        {
            ProjectionDefinition<BsonDocument>? projection = null;
            foreach (string item in projectItems)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    projection = Builders<BsonDocument>.Projection.Include(item);
                }
            }

            if (projection is not null)
            {
                findFluent = findFluent.Project<BsonDocument>(projection);
            }
        }

        return await findFluent.FirstOrDefaultAsync();
    }

    public Dictionary<string, object?> BsonToJson(BsonDocument bsonDoc)
    {
        /*string json = bsonDoc.ToJson(
            new JsonWriterSettings()
            {
                OutputMode = JsonOutputMode.Shell,
            }
        );

        return json;*/
        Dictionary<string, object?> dictionary = [];

        foreach (var bsonElement in bsonDoc)
        {
            dictionary.Add(bsonElement.Name, BsonValueToSimpleTypes(bsonElement.Value));
        }

        return dictionary;
    }
    private object? BsonValueToSimpleTypes(BsonValue bsonValue)
    {
        if (bsonValue.IsBoolean)
        {
            return bsonValue.AsBoolean;
        }
        else if (bsonValue.IsGuid)
        {
            return bsonValue.AsGuid;
        }
        else if (bsonValue.IsBsonDateTime)
        {
            return bsonValue.ToUniversalTime();
        }
        else if (bsonValue.IsBsonDocument)
        {
            return BsonValueToSimpleTypes(bsonValue);
        }
        else if (bsonValue.IsBsonNull)
        {
            return null;
        }
        else if (bsonValue.IsDouble)
        {
            return bsonValue.AsDouble;
        }
        else if (bsonValue.IsInt32)
        {
            return bsonValue.AsInt32;
        }
        else if (bsonValue.IsObjectId)
        {
            return bsonValue.ToString();
        }
        else if (bsonValue.IsString)
        {
            return bsonValue.ToString();
        }
        return new { };
    }

    public async Task CreateNewUserProfile(NewUserProfileModel model)
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

    public async Task DeleteUserProfile(Guid userId)
    {
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(userId, GuidRepresentation.Standard));
        await _mongoCollection.DeleteOneAsync(filter);
    }




}

public class NewUserProfileModel
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
