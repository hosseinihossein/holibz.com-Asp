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
    [Authorize]
    [ValidateAntiForgeryToken]
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



}