using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AspApp.Database;
using AspApp.Helpers;
using AspApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AspApp.Controllers;

[ApiController]
[Route("/api/[controller]/[action]")]
public class DocumentController : ControllerBase
{
    readonly IMongoCollection<BsonDocument> _documentCollection;
    readonly BsonManager _bsonManager;
    readonly Application_DbContext _appDb;
    readonly DocumentManager _docManager;
    public DocumentController(
        IMongoDatabase mongoAppDb,
        MongoDbConfigs mongoDbConfigs,
        Application_DbContext appDb,
        BsonManager bsonManager,
        DocumentManager docManager
    )
    {
        _documentCollection = mongoAppDb.GetCollection<BsonDocument>(mongoDbConfigs.DocumentCollection);
        _appDb = appDb;
        _bsonManager = bsonManager;
        _docManager = docManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetDocIdsOfShelf([FromQuery][StringLength(36)] string shelfId)
    {
        if (!Guid.TryParseExact(shelfId, "D", out Guid shelfGuid))
        {
            ModelState.AddModelError("Shelf", "Couldn't parse the Shelf id!");
            return BadRequest(ModelState);
        }

        // postgres
        Guid[] docIds = await _appDb.Shelves.Where(sh => sh.Id == shelfGuid)
        .SelectMany(sh => sh.Documents)
        .Select(pShchD => pShchD.DocumentId)
        .ToArrayAsync();

        return Ok(new { docIds });
    }

    [HttpGet]
    public async Task<IActionResult> GetDocMiniCardInfo([FromQuery][StringLength(36)] string docId)
    {
        if (!Guid.TryParseExact(docId, "D", out Guid docGuid))
        {
            ModelState.AddModelError("Document", "Couldn't parse the Document id!");
            return BadRequest(ModelState);
        }

        var bsonDoc = await _bsonManager.GetFirstBsonDoc(_documentCollection, docGuid, ["title", "description", "imageUrl"]);

        if (bsonDoc is null)
        {
            return NotFound(new { message = "Document not found!" });
        }

        return Ok(_bsonManager.BsonToJson(bsonDoc));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    [RequestSizeLimit(256 * 1024)]//120 KB for image and some extra for other data like title, description ...
    public async Task<IActionResult> SubmitNewDocument([FromForm] Document_NewDoc_FormModel formModel)
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

        var parentShelf = await _appDb.Shelves
        .FirstOrDefaultAsync(sh => sh.Id == formModel.ParentShelfId && sh.OwnerId == userGuid);
        if (parentShelf is null)
        {
            ModelState.AddModelError("ParentShelf", "The parent shelf not found!");
            return BadRequest(ModelState);
        }

        DocumentManager_NewDoc_Model docManager_NewShelf_Model = new(formModel, user, parentShelf);
        await _docManager.CreateNewDoc(docManager_NewShelf_Model);

        return Ok(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> GetDocCardInfo([FromQuery][StringLength(36)] string docId)
    {
        if (!Guid.TryParseExact(docId, "D", out Guid docGuid))
        {
            ModelState.AddModelError("Document", "Couldn't parse the Document id!");
            return BadRequest(ModelState);
        }

        var bsonDoc = await _bsonManager.GetFirstBsonDoc(_documentCollection, docGuid);

        if (bsonDoc is null)
        {
            return NotFound(new { message = "Document not found!" });
        }

        return Ok(_bsonManager.BsonToJson(bsonDoc));
    }

}