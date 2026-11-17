using MongoDB.Bson;
using MongoDB.Driver;

namespace AspApp.Helpers;

public class BsonManager
{
    public async Task<BsonDocument?> GetFirstBsonDoc(
        IMongoCollection<BsonDocument> mongoCollection,
        Guid guid,
        string[]? projectItems = null
    )
    {
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(guid, GuidRepresentation.Standard));

        IFindFluent<BsonDocument, BsonDocument> findFluent = mongoCollection.Find(filter);

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

    public async Task<BsonDocument?> GetFirstBsonDoc(
        IMongoCollection<BsonDocument> mongoCollection,
        Dictionary<string, object> conditions,
        string[]? projectItems = null
    )
    {
        List<FilterDefinition<BsonDocument>> filterList = [];
        foreach (var kv in conditions)
        {
            var bsonValue = SimpleTypesToBsonValue(kv.Value);
            if (bsonValue is not null)
            {
                filterList.Add(Builders<BsonDocument>.Filter.Eq(kv.Key, bsonValue));
            }
        }
        var filter = Builders<BsonDocument>.Filter.And(filterList);

        IFindFluent<BsonDocument, BsonDocument> findFluent = mongoCollection.Find(filter);

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
    private BsonValue? SimpleTypesToBsonValue(object obj)
    {
        if (obj is null)
        {
            return null;
        }
        else if (obj is bool boolValue)
        {
            return new BsonBoolean(boolValue);
        }
        else if (obj is Guid guidValue)
        {
            return new BsonBinaryData(guidValue, GuidRepresentation.Standard);
        }
        else if (obj is DateTime dateTimeValue)
        {
            return new BsonDateTime(dateTimeValue);
        }
        else if (obj is double doubleValue)
        {
            return new BsonDouble(doubleValue);
        }
        else if (obj is int intValue)
        {
            return new BsonInt32(intValue);
        }
        else if (obj is string stringValue)
        {
            return new BsonString(stringValue);
        }

        return null;
    }
}