using AspApp.Models;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AspApp.Helpers;

public class LibraryManager
{
    readonly Library_DbContext _libDb;
    readonly IMongoCollection<BsonDocument> _mongoCollection;
    readonly IWebHostEnvironment _env;
    public LibraryManager(
        Library_DbContext libDb,
        IMongoClient mongoClient,
        MongoDbConfigs mongoDbConfigs,
        IWebHostEnvironment env
    )
    {
        _libDb = libDb;
        IMongoDatabase libraryDb = mongoClient.GetDatabase(mongoDbConfigs.LibraryDb);
        _mongoCollection = libraryDb.GetCollection<BsonDocument>(mongoDbConfigs.LibraryCollection);
        _env = env;
    }

    public async Task<BsonDocument?> GetFirstBsonDoc(Guid libGuid, string[]? projectItems = null)
    {
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(libGuid, GuidRepresentation.Standard));

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

    public async Task CreateNewUser(Guid userId)
    {
        Library_User_DbModel libUser = new()
        {
            Id = userId,
        };
        await _libDb.Users.AddAsync(libUser);
        await _libDb.SaveChangesAsync();
    }

    public async Task DeleteUser(Guid userId)
    {
        var user = await _libDb.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return;
        }
        _libDb.Users.Remove(user);
        await _libDb.SaveChangesAsync();
    }

    public async Task DeleteAllLibraries()
    {
        await _libDb.Libraries.ExecuteDeleteAsync();
    }

    public void DeleteImage(string imageUrl)
    {
        string imageFullPath = Path.Combine(_env.ContentRootPath, imageUrl);

        if (File.Exists(imageFullPath))
        {
            File.Delete(imageFullPath);
        }
    }

}

