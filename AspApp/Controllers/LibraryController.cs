using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AspApp.Helpers;
using AspApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using OpenIddict.Abstractions;

namespace AspApp.Controllers;

[ApiController]
[Route("/api/[controller]/[action]")]
public class LibraryController : ControllerBase
{
    readonly ILogger<LibraryController> _logger;
    readonly IMongoCollection<BsonDocument> _mongoCollection;
    readonly Library_DbContext _libDb;
    readonly StorageManager _storageManager;

    public LibraryController(
        ILogger<LibraryController> logger,
        StorageManager storageManager,
        IMongoClient mongoClient,
        MongoDbConfigs mongoDbConfigs,
        Library_DbContext libDb
    )
    {
        _logger = logger;

        IMongoDatabase libraryDb = mongoClient.GetDatabase(mongoDbConfigs.LibraryDb);
        _mongoCollection = libraryDb.GetCollection<BsonDocument>(mongoDbConfigs.LibraryCollection);

        _storageManager = storageManager;

        _libDb = libDb;
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

    private Dictionary<string, object?> BsonToJson(BsonDocument bsonDoc)
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

        //return Content(BsonToJson(bsonDoc), "application/json");
        return Ok(BsonToJson(bsonDoc));
    }

    [HttpGet]
    public async Task<IActionResult> GetLibraryIds([FromQuery][StringLength(36)] string userId)
    {
        if (!Guid.TryParseExact(userId, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse the user id!");
            return BadRequest(ModelState);
        }

        Guid[] libIds = await _libDb.Users.Where(u => u.Id == userGuid)
        .SelectMany(u => u.Libraries)
        .Select(l => l.Id)
        .ToArrayAsync();

        return Ok(new { libIds });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(256 * 1024)]//120 KB for image and some extra for other data like title, description ...
    public async Task<IActionResult> SubmitNewLibrary([FromForm] Library_NewLibrary_FormModel formModel)
    {
        if (!Guid.TryParseExact(User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse the user id!");
            return BadRequest(ModelState);
        }
        var user = await _libDb.Users.FirstOrDefaultAsync(u => u.Id == userGuid);
        if (user is null)
        {
            ModelState.AddModelError("User", $"Couldn't find the user! userId: {userGuid}");
            return BadRequest(ModelState);
        }

        Library_Library_DbModel newLib = new()
        {
            Owner = user,
        };

        await _libDb.Libraries.AddAsync(newLib);
        await _libDb.SaveChangesAsync();

        var bsonDoc = new BsonDocument()
        {
            { "_id", new BsonBinaryData(newLib.Id, GuidRepresentation.Standard) },
            { "ownerId", new BsonBinaryData(userGuid, GuidRepresentation.Standard) },
            { "title", new BsonString(formModel.Title) },
            { "createdAt", new BsonDateTime(DateTime.UtcNow) },
        };
        if (!string.IsNullOrWhiteSpace(formModel.Description))
        {
            bsonDoc.Add("description", new BsonString(formModel.Description));
        }

        if (formModel.Image is not null)
        {
            string imageName = Guid.NewGuid().ToString("D") + ".webp";
            string imageFileFullPath = Path.Combine(_storageManager.LibraryImageDirInfo.FullName, imageName);
            using (FileStream fs = System.IO.File.Create(imageFileFullPath))
            {
                await formModel.Image.CopyToAsync(fs);
            }

            string imageLinkPath = Path.Combine(
                "/" +
                _storageManager.StorageDirInfo.Name,
                _storageManager.LibraryImageDirInfo.Name,
                imageName
            );
            bsonDoc.Add("imageUrl", new BsonString(imageLinkPath));
        }

        await _mongoCollection.InsertOneAsync(bsonDoc);

        return Ok(new { success = true });
    }

}