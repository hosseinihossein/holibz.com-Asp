using System.ComponentModel.DataAnnotations;
using AspApp.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace AspApp.Controllers;

[ApiController]
[Route("/api/[controller]/[action]")]
public class LibraryController : ControllerBase
{
    readonly ILogger<LibraryController> _logger;
    readonly IMongoCollection<BsonDocument> _mongoCollection;
    readonly StorageConfigs _storageConfigs;

    public LibraryController(
        ILogger<LibraryController> logger,
        StorageConfigs storageConfigs,
        IMongoClient mongoClient,
        MongoDbConfigs mongoDbConfigs
    )
    {
        _logger = logger;

        IMongoDatabase libraryDb = mongoClient.GetDatabase(mongoDbConfigs.LibraryDb);
        _mongoCollection = libraryDb.GetCollection<BsonDocument>(mongoDbConfigs.LibraryCollection);

        _storageConfigs = storageConfigs;
    }


    private async Task<BsonDocument?> GetModelBson(Guid libGuid, string[]? projectItems = null)
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

    private string BsonToJson(BsonDocument bsonDoc)
    {
        string json = bsonDoc.ToJson(
            new JsonWriterSettings()
            {
                OutputMode = JsonOutputMode.RelaxedExtendedJson,
            }
        );

        return json;
    }

    [HttpGet]
    public async Task<IActionResult> GetLibraryCardInfo([FromQuery][StringLength(36)] string libId)
    {
        if (!Guid.TryParseExact(libId, "D", out Guid libGuid))
        {
            ModelState.AddModelError("Library", "Couldn't parse library id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await GetModelBson(libGuid);

        if (bsonDoc is null)
        {
            return NotFound(new { message = "Library not found!" });
        }

        return Content(BsonToJson(bsonDoc), "application/json");
    }

}