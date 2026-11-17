using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using AspApp.Helpers;
using AspApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
//using OpenIddict.Abstractions;

namespace AspApp.Controllers;

[ApiController]
[Route("/api/[controller]/[action]")]
public class UserProfileController : ControllerBase
{
    //readonly ILogger<UserProfileController> _logger;
    readonly IMongoCollection<BsonDocument> _profileCollection;
    readonly UserAccountManager _userAccManager;
    readonly BsonManager _bsonManager;

    public UserProfileController(
        //ILogger<UserProfileController> logger,
        IMongoDatabase mongoDb,
        MongoDbConfigs mongoDbConfigs,
        UserAccountManager userAccManager,
        BsonManager bsonManager
    )
    {
        //_logger = logger;
        _profileCollection = mongoDb.GetCollection<BsonDocument>(mongoDbConfigs.ProfileCollection);
        _userAccManager = userAccManager;
        _bsonManager = bsonManager;
    }



    [HttpGet]
    public async Task<IActionResult> GetProfileInfo([FromQuery][StringLength(36)] string userId)
    {
        if (!Guid.TryParseExact(userId, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _bsonManager.GetFirstBsonDoc(_profileCollection, userGuid);

        if (bsonDoc is null)
        {
            return NotFound(new { message = "User not found!" });
        }

        return Ok(_bsonManager.BsonToJson(bsonDoc));
    }

    [HttpGet]
    public async Task<IActionResult> GetUserAvatarUrl([FromQuery][StringLength(36)] string userId)
    {
        if (!Guid.TryParseExact(userId, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _bsonManager.GetFirstBsonDoc(_profileCollection, userGuid, ["avatarUrl"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("User", "User Profile not found!");
            return BadRequest(ModelState);
        }

        //delete the avatar file and update database
        if (bsonDoc.TryGetValue("avatarUrl", out BsonValue bsonValue) && bsonValue.IsString)
        {
            string avatarUrl = bsonValue.AsString;
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                return Ok(new { avatarUrl });
            }
        }

        return Ok(null);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    [RequestSizeLimit(128 * 1024)]//128 KB
    public async Task<IActionResult> SubmitUserImage([FromForm] UserImageFile_FormModel formModel)
    {
        string? userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParseExact(userIdString, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        string avatarLinkPath = await _userAccManager.SaveAvatarAndGetUrl(formModel.UserImageFile);

        BsonDocument? bsonDoc = await _bsonManager.GetFirstBsonDoc(_profileCollection, userGuid, ["avatarUrl"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("User", "User Profile not found!");
            return BadRequest(ModelState);
        }

        //delete the old avatar file
        if (bsonDoc.TryGetValue("avatarUrl", out BsonValue bsonValue) && bsonValue.IsString)
        {
            string oldAvatarUrl = bsonValue.AsString;
            if (string.IsNullOrWhiteSpace(oldAvatarUrl))
            {
                _userAccManager.DeleteAvatarFile(oldAvatarUrl);
            }
        }

        //update avatarId for user profile document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        var update = Builders<BsonDocument>.Update.Set("avatarUrl", new BsonString(avatarLinkPath));

        await _profileCollection.UpdateOneAsync(filter, update);

        return Ok(new { success = true });
    }
    public class UserImageFile_FormModel
    {
        public IFormFile UserImageFile { get; set; } = null!;
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> DeleteUserImage()
    {
        if (!Guid.TryParseExact(User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _bsonManager.GetFirstBsonDoc(_profileCollection, userGuid, ["avatarUrl"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("User", "User Profile not found!");
            return BadRequest(ModelState);
        }

        //delete the avatar file and update database
        if (bsonDoc.TryGetValue("avatarUrl", out BsonValue bsonValue) && bsonValue.IsString)
        {
            string avatarId = bsonValue.AsString;
            if (string.IsNullOrWhiteSpace(avatarId))
            {
                //delete file
                _userAccManager.DeleteAvatarFile(avatarId);

                //update mongodb
                var filter = Builders<BsonDocument>.Filter
                .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));
                var update = Builders<BsonDocument>.Update.Unset("avatarUrl");
                await _profileCollection.UpdateOneAsync(filter, update);
            }
        }

        return Ok(new { success = true });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitProfileName([FromForm][StringLength(60)] string name)
    {
        if (!Guid.TryParseExact(User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        //update avatarId for user profile document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        var update = Builders<BsonDocument>.Update.Set("name", new BsonString(name));
        await _profileCollection.UpdateOneAsync(filter, update);

        return Ok(new { success = true });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDescription([FromForm][StringLength(400)] string description)
    {
        if (!Guid.TryParseExact(User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        //update avatarId for user profile document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        var update = Builders<BsonDocument>.Update.Set("description", new BsonString(description));
        await _profileCollection.UpdateOneAsync(filter, update);

        return Ok(new { success = true });
    }



}