using System.ComponentModel.DataAnnotations;
using AspApp.Database;
using AspApp.Helpers;
using AspApp.Models;
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
    public DocumentController(
        IMongoDatabase mongoAppDb,
        MongoDbConfigs mongoDbConfigs,
        Application_DbContext appDb,
        BsonManager bsonManager
    )
    {
        _documentCollection = mongoAppDb.GetCollection<BsonDocument>(mongoDbConfigs.DocumentCollection);
        _appDb = appDb;
        _bsonManager = bsonManager;
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
}