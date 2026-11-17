using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AspApp.Database;
using AspApp.Helpers;
using AspApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AspApp.Controllers;

[ApiController]
[Route("/api/[controller]/[action]")]
public class ShelfController : ControllerBase
{
    readonly IMongoCollection<BsonDocument> _shelfCollection;
    readonly Application_DbContext _appDb;
    readonly ShelfManager _shelfManager;
    readonly BsonManager _bsonManager;
    public ShelfController(
        IMongoDatabase mongoAppDb,
        MongoDbConfigs mongoDbConfigs,
        Application_DbContext appDb,
        ShelfManager shelfManager,
        BsonManager bsonManager
    )
    {
        _shelfCollection = mongoAppDb.GetCollection<BsonDocument>(mongoDbConfigs.ShelfCollection);
        _appDb = appDb;
        _shelfManager = shelfManager;
        _bsonManager = bsonManager;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    [RequestSizeLimit(256 * 1024)]//120 KB for image and some extra for other data like title, description ...
    public async Task<IActionResult> SubmitNewShelf([FromForm] Shelf_NewShelf_FormModel formModel)
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

        var parentLib = await _appDb.Libraries
        .FirstOrDefaultAsync(l => l.Id == formModel.ParentLibId && l.OwnerId == userGuid);
        if (parentLib is null)
        {
            ModelState.AddModelError("ParentLib", "The parent library not found!");
            return BadRequest(ModelState);
        }

        ShelfManager_NewShelf_Model shelfManager_NewShelf_Model = new(formModel, user, parentLib);
        await _shelfManager.CreateNewShelf(shelfManager_NewShelf_Model);

        return Ok(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> GetShelfIdsOfLibrary([FromQuery][StringLength(36)] string libId)
    {
        if (!Guid.TryParseExact(libId, "D", out Guid libGuid))
        {
            ModelState.AddModelError("Library", "Couldn't parse the Library id!");
            return BadRequest(ModelState);
        }

        // postgres
        Guid[] shelfIds = await _appDb.Libraries.Where(l => l.Id == libGuid)
        .SelectMany(l => l.Shelves)
        .Select(lsh => lsh.ShelfId)
        .ToArrayAsync();

        return Ok(new { shelfIds });
    }

    [HttpGet]
    public async Task<IActionResult> GetShelfCardInfo([FromQuery][StringLength(36)] string shelfId)
    {
        if (!Guid.TryParseExact(shelfId, "D", out Guid shelfGuid))
        {
            ModelState.AddModelError("Shelf", "Couldn't parse the Shelf id!");
            return BadRequest(ModelState);
        }

        var bsonDoc = await _bsonManager.GetFirstBsonDoc(_shelfCollection, shelfGuid);

        if (bsonDoc is null)
        {
            return NotFound(new { message = "Shelf not found!" });
        }

        return Ok(_bsonManager.BsonToJson(bsonDoc));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> SubmitImage([FromForm] Shelf_SubmitImage_FormModel formModel)
    {
        string? userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParseExact(userIdString, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        if (!Guid.TryParseExact(formModel.ShelfId, "D", out Guid shelfGuid))
        {
            ModelState.AddModelError("Shelf", "Couldn't parse the Shelf id!");
            return BadRequest(ModelState);
        }

        Dictionary<string, object> conditions = new()
        {
            {"_id",shelfGuid},
            {"ownerId", userGuid}
        };
        BsonDocument? bsonDoc = await _bsonManager.GetFirstBsonDoc(_shelfCollection, conditions, ["imageUrl"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("Shelf", "Shelf not found!");
            return BadRequest(ModelState);
        }

        //delete the old avatar file
        if (bsonDoc.TryGetValue("imageUrl", out BsonValue imageUrlBsonValue) && imageUrlBsonValue.IsString)
        {
            string imageUrl = imageUrlBsonValue.AsString;
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                _shelfManager.DeleteImage(imageUrl);
            }
        }

        // save new image and get it's url here because the lib's owner needs to be checked first
        string imageLinkPath = await _shelfManager.SaveImageAndGetUrl(formModel.ImageFile);

        //update avatarId for user profile document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(shelfGuid, GuidRepresentation.Standard));
        var update = Builders<BsonDocument>.Update.Set("imageUrl", new BsonString(imageLinkPath));
        await _shelfCollection.UpdateOneAsync(filter, update);

        return Ok(new { success = true });
    }

    [HttpDelete]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage([FromQuery][StringLength(36)] string shelfId)
    {
        if (!Guid.TryParseExact(User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        if (!Guid.TryParseExact(shelfId, "D", out Guid shelfGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        Dictionary<string, object> conditions = new()
        {
            {"_id",shelfGuid},
            {"ownerId", userGuid}
        };
        BsonDocument? bsonDoc = await _bsonManager.GetFirstBsonDoc(_shelfCollection, conditions, ["imageUrl"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("Shelf", "Shelf not found!");
            return BadRequest(ModelState);
        }

        //delete the image file and update database
        if (bsonDoc.TryGetValue("imageUrl", out BsonValue imageUrlBsonValue) && imageUrlBsonValue.IsString)
        {
            string imageUrl = imageUrlBsonValue.AsString;
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                //delete file
                _shelfManager.DeleteImage(imageUrl);

                //update mongodb
                var filter = Builders<BsonDocument>.Filter
                .Eq("_id", new BsonBinaryData(shelfGuid, GuidRepresentation.Standard));
                var update = Builders<BsonDocument>.Update.Unset("imageUrl");
                await _shelfCollection.UpdateOneAsync(filter, update);
            }
        }

        return Ok(new { success = true });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitTitle([FromForm] Shelf_SubmitTitle_FormModel formModel)
    {
        if (!Guid.TryParseExact(User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        if (!Guid.TryParseExact(formModel.ShelfId, "D", out Guid shelfGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        /*bool libExist = await _appDb.Libraries.AnyAsync(l => l.Id == libGuid && l.OwnerId == userGuid);
        if (!libExist)
        {
            ModelState.AddModelError("Shelf", "Your Shelf Not Found!");
            return BadRequest(ModelState);
        }*/

        //update shelf title document on mongodb
        var idFilter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(shelfGuid, GuidRepresentation.Standard));

        var ownerFilter = Builders<BsonDocument>.Filter
        .Eq("ownerId", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        var filter = Builders<BsonDocument>.Filter.And(idFilter, ownerFilter);

        var update = Builders<BsonDocument>.Update.Set("title", new BsonString(formModel.Title));
        var result = await _shelfCollection.UpdateOneAsync(filter, update);

        if (result.IsAcknowledged && result.IsModifiedCountAvailable && result.ModifiedCount > 0)
        {
            return Ok(new { success = true });
        }
        return Ok(new { success = false });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDescription([FromForm] Shelf_SubmitDescription_FormModel formModel)
    {
        if (!Guid.TryParseExact(User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        if (!Guid.TryParseExact(formModel.ShelfId, "D", out Guid shelfGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        //update shelf description document on mongodb
        var idFilter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(shelfGuid, GuidRepresentation.Standard));

        var ownerFilter = Builders<BsonDocument>.Filter
        .Eq("ownerId", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        var filter = Builders<BsonDocument>.Filter.And(idFilter, ownerFilter);

        var update = Builders<BsonDocument>.Update.Set("description", new BsonString(formModel.Description));
        var result = await _shelfCollection.UpdateOneAsync(filter, update);

        if (result.IsAcknowledged && result.IsModifiedCountAvailable && result.ModifiedCount > 0)
        {
            return Ok(new { success = true });
        }
        return Ok(new { success = false });
    }


}