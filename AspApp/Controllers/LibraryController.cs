using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AspApp.Database;
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
    readonly IMongoCollection<BsonDocument> _libraryCollection;
    readonly Application_DbContext _appDb;
    readonly LibraryManager _libraryManager;
    readonly BsonManager _bsonManager;

    public LibraryController(
        IMongoDatabase mongoAppDb,
        MongoDbConfigs mongoDbConfigs,
        Application_DbContext appDb,
        LibraryManager libraryManager,
        BsonManager bsonManager
    )
    {
        _libraryCollection = mongoAppDb.GetCollection<BsonDocument>(mongoDbConfigs.LibraryCollection);
        _appDb = appDb;
        _libraryManager = libraryManager;
        _bsonManager = bsonManager;
    }


    [HttpGet]
    public async Task<IActionResult> GetLibraryCardInfo([FromQuery][StringLength(36)] string libId)
    {
        if (!Guid.TryParseExact(libId, "D", out Guid libGuid))
        {
            ModelState.AddModelError("Library", "Couldn't parse library id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _bsonManager.GetFirstBsonDoc(_libraryCollection, libGuid);

        if (bsonDoc is null)
        {
            return NotFound(new { message = "Library not found!" });
        }

        return Ok(_bsonManager.BsonToJson(bsonDoc));
    }

    [HttpGet]
    public async Task<IActionResult> GetLibraryIdsOfUser([FromQuery][StringLength(36)] string userId)
    {
        if (!Guid.TryParseExact(userId, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse the user id!");
            return BadRequest(ModelState);
        }

        // postgres
        Guid[] libIds = await _appDb.Users.Where(u => u.Id == userGuid)
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
        var user = await _appDb.Users.FirstOrDefaultAsync(u => u.Id == userGuid);
        if (user is null)
        {
            ModelState.AddModelError("User", $"Couldn't find the user! userId: {userGuid}");
            return BadRequest(ModelState);
        }

        LibraryManager_NewLib_Model model = new(formModel, user);
        await _libraryManager.CreateNewLib(model);

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

        Dictionary<string, object> conditions = new()
        {
            {"_id",libGuid},
            {"ownerId", userGuid}
        };
        BsonDocument? bsonDoc = await _bsonManager.GetFirstBsonDoc(_libraryCollection, conditions, ["imageUrl"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("Library", "Library not found!");
            return BadRequest(ModelState);
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

        // save new image and get it's url here because the lib's owner needs to be checked first
        string imageLinkPath = await _libraryManager.SaveImageAndGetUrl(formModel.ImageFile);

        //update avatarId for user profile document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(libGuid, GuidRepresentation.Standard));
        var update = Builders<BsonDocument>.Update.Set("imageUrl", new BsonString(imageLinkPath));
        await _libraryCollection.UpdateOneAsync(filter, update);

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

        Dictionary<string, object> conditions = new()
        {
            {"_id",libGuid},
            {"ownerId", userGuid}
        };
        BsonDocument? bsonDoc = await _bsonManager.GetFirstBsonDoc(_libraryCollection, conditions, ["imageUrl"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("Library", "Library not found!");
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
                await _libraryCollection.UpdateOneAsync(filter, update);
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

        /*bool libExist = await _appDb.Libraries.AnyAsync(l => l.Id == libGuid && l.OwnerId == userGuid);
        if (!libExist)
        {
            ModelState.AddModelError("Library", "Your Library Not Found!");
            return BadRequest(ModelState);
        }*/

        //update library title document on mongodb
        var idFilter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(libGuid, GuidRepresentation.Standard));

        var ownerFilter = Builders<BsonDocument>.Filter
        .Eq("ownerId", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        var filter = Builders<BsonDocument>.Filter.And(idFilter, ownerFilter);

        var update = Builders<BsonDocument>.Update.Set("title", new BsonString(formModel.Title));
        var result = await _libraryCollection.UpdateOneAsync(filter, update);

        if (result.IsAcknowledged && result.IsModifiedCountAvailable && result.ModifiedCount > 0)
        {
            return Ok(new { success = true });
        }
        return Ok(new { success = false });
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

        //update library description document on mongodb
        var idFilter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(libGuid, GuidRepresentation.Standard));

        var ownerFilter = Builders<BsonDocument>.Filter
        .Eq("ownerId", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        var filter = Builders<BsonDocument>.Filter.And(idFilter, ownerFilter);

        var update = Builders<BsonDocument>.Update.Set("description", new BsonString(formModel.Description));
        var result = await _libraryCollection.UpdateOneAsync(filter, update);

        if (result.IsAcknowledged && result.IsModifiedCountAvailable && result.ModifiedCount > 0)
        {
            return Ok(new { success = true });
        }
        return Ok(new { success = false });
    }




}