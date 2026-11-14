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
    readonly LibraryManager _libraryManager;

    public LibraryController(
        ILogger<LibraryController> logger,
        StorageManager storageManager,
        IMongoClient mongoClient,
        MongoDbConfigs mongoDbConfigs,
        Library_DbContext libDb,
        LibraryManager libraryManager
    )
    {
        _logger = logger;

        IMongoDatabase libraryDb = mongoClient.GetDatabase(mongoDbConfigs.LibraryDb);
        _mongoCollection = libraryDb.GetCollection<BsonDocument>(mongoDbConfigs.LibraryCollection);

        _storageManager = storageManager;

        _libDb = libDb;

        _libraryManager = libraryManager;
    }


    [HttpGet]
    public async Task<IActionResult> GetLibraryCardInfo([FromQuery][StringLength(36)] string libId)
    {
        if (!Guid.TryParseExact(libId, "D", out Guid libGuid))
        {
            ModelState.AddModelError("Library", "Couldn't parse library id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _libraryManager.GetFirstBsonDoc(libGuid);

        if (bsonDoc is null)
        {
            return NotFound(new { message = "Library not found!" });
        }

        return Ok(_libraryManager.BsonToJson(bsonDoc));
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

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> SubmitImage([FromForm] Library_SubmitImage_FormModel formModel)
    {
        string? userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParseExact(userIdString, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        if (!Guid.TryParseExact(formModel.LibraryId, "D", out Guid libGuid))
        {
            ModelState.AddModelError("Library", "Couldn't parse the Library id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _libraryManager.GetFirstBsonDoc(libGuid, ["ownerId", "imageUrl"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("Library", "Library not found!");
            return BadRequest(ModelState);
        }

        Guid ownerId = bsonDoc.GetValue("ownerId").AsGuid;
        if (ownerId != userGuid)
        {
            ModelState.AddModelError("Library", "You are not the owner of the library!");
            return BadRequest(ModelState);
        }

        string imageName = Guid.NewGuid().ToString("D") + ".webp";
        string imageFullPath = Path.Combine(_storageManager.LibraryImageDirInfo.FullName, imageName);
        using (FileStream fs = System.IO.File.Create(imageFullPath))
        {
            await formModel.ImageFile.CopyToAsync(fs);
        }

        //delete the old avatar file
        if (bsonDoc.TryGetValue("imageUrl", out BsonValue imageUrlBsonValue) && imageUrlBsonValue.IsString)
        {
            string imageUrl = imageUrlBsonValue.AsString;
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                _libraryManager.DeleteImage(imageUrl);
            }
        }

        //update avatarId for user profile document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(libGuid, GuidRepresentation.Standard));

        string imageLinkPath = Path.Combine(
            "/" +
            _storageManager.StorageDirInfo.Name,
            _storageManager.LibraryImageDirInfo.Name,
            imageName
        );
        var update = Builders<BsonDocument>.Update.Set("imageUrl", new BsonString(imageLinkPath));
        await _mongoCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });

        return Ok(new { success = true });
    }

    [HttpDelete]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage([FromQuery][StringLength(36)] string libraryId)
    {
        if (!Guid.TryParseExact(User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        if (!Guid.TryParseExact(libraryId, "D", out Guid libGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _libraryManager.GetFirstBsonDoc(libGuid, ["ownerId", "imageUrl"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("Library", "Library not found!");
            return BadRequest(ModelState);
        }

        Guid ownerId = bsonDoc.GetValue("ownerId").AsGuid;
        if (ownerId != userGuid)
        {
            ModelState.AddModelError("Library", "You are not the owner of the library!");
            return BadRequest(ModelState);
        }

        //delete the image file and update database
        if (bsonDoc.TryGetValue("imageUrl", out BsonValue imageUrlBsonValue) && imageUrlBsonValue.IsString)
        {
            string imageUrl = imageUrlBsonValue.AsString;
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                //delete file
                _libraryManager.DeleteImage(imageUrl);

                //update mongodb
                var filter = Builders<BsonDocument>.Filter
                .Eq("_id", new BsonBinaryData(libGuid, GuidRepresentation.Standard));
                var update = Builders<BsonDocument>.Update.Unset("imageUrl");
                await _mongoCollection.UpdateOneAsync(filter, update);
            }
        }

        return Ok(new { success = true });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitTitle([FromForm] Library_SubmitTitle_FormModel formModel)
    {
        if (!Guid.TryParseExact(User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        if (!Guid.TryParseExact(formModel.LibraryId, "D", out Guid libGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _libraryManager.GetFirstBsonDoc(libGuid, ["ownerId"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("Library", "Library not found!");
            return BadRequest(ModelState);
        }

        Guid ownerId = bsonDoc.GetValue("ownerId").AsGuid;
        if (ownerId != userGuid)
        {
            ModelState.AddModelError("Library", "You are not the owner of the library!");
            return BadRequest(ModelState);
        }

        //update library title document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(libGuid, GuidRepresentation.Standard));

        var update = Builders<BsonDocument>.Update.Set("title", new BsonString(formModel.Title));
        await _mongoCollection.UpdateOneAsync(filter, update);

        return Ok(new { success = true });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDescription([FromForm] Library_SubmitDescription_FormModel formModel)
    {
        if (!Guid.TryParseExact(User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        if (!Guid.TryParseExact(formModel.LibraryId, "D", out Guid libGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _libraryManager.GetFirstBsonDoc(libGuid, ["ownerId"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("Library", "Library not found!");
            return BadRequest(ModelState);
        }

        Guid ownerId = bsonDoc.GetValue("ownerId").AsGuid;
        if (ownerId != userGuid)
        {
            ModelState.AddModelError("Library", "You are not the owner of the library!");
            return BadRequest(ModelState);
        }

        //update library description document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(libGuid, GuidRepresentation.Standard));

        var update = Builders<BsonDocument>.Update.Set("description", new BsonString(formModel.Description));
        await _mongoCollection.UpdateOneAsync(filter, update);

        return Ok(new { success = true });
    }




}