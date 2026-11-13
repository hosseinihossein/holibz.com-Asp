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
    readonly ILogger<UserProfileController> _logger;
    readonly IMongoCollection<BsonDocument> _mongoCollection;
    readonly StorageManager _storageManager;
    readonly UserProfileManager _profileManager;

    public UserProfileController(
        ILogger<UserProfileController> logger,
        IMongoClient mongoClient,
        MongoDbConfigs mongoDbConfigs,
        StorageManager storageManager,
        UserProfileManager profileManager
    )
    {
        _logger = logger;

        IMongoDatabase userAccountDb = mongoClient.GetDatabase(mongoDbConfigs.UserAccountDb);
        _mongoCollection = userAccountDb.GetCollection<BsonDocument>(mongoDbConfigs.ProfileCollection);

        _storageManager = storageManager;

        _profileManager = profileManager;
    }



    [HttpGet]
    public async Task<IActionResult> GetProfileInfo([FromQuery][StringLength(36)] string userId)
    {
        if (!Guid.TryParseExact(userId, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _profileManager.GetFirstBsonDoc(userGuid);

        if (bsonDoc is null)
        {
            return NotFound(new { message = "User not found!" });
        }

        //return Content(_profileManager.BsonToJson(bsonDoc), "application/json");
        var json = BsonSerializer.Deserialize<object>(bsonDoc);
        return Ok(json);
    }

    [HttpGet]
    public async Task<IActionResult> GetUserAvatarUrl([FromQuery][StringLength(36)] string userId)
    {
        if (!Guid.TryParseExact(userId, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _profileManager.GetFirstBsonDoc(userGuid, ["avatarId"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("User", "User Profile not found!");
            return BadRequest(ModelState);
        }

        //delete the avatar file and update database
        if (bsonDoc.TryGetValue("avatarId", out BsonValue bsonValue) && bsonValue.IsString)
        {
            string avatarId = bsonValue.AsString;
            if (string.IsNullOrWhiteSpace(avatarId))
            {
                return Ok(new { url = $"/Storage/UserProfile/Avatar/{avatarId}" });
            }
        }

        return NotFound();
    }

    [HttpPost]
    [Authorize]
    [RequestSizeLimit(128 * 1024)]//128 KB
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitUserImage([FromForm] UserImageFile_FormModel formModel)
    {
        string? userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParseExact(userIdString, "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _profileManager.GetFirstBsonDoc(userGuid, ["avatarId"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("User", "User Profile not found!");
            return BadRequest(ModelState);
        }

        string avatarName = Guid.NewGuid().ToString("D") + ".webp";
        string avatarFileFullPath = Path.Combine(_storageManager.UserAvatarDirInfo.FullName, avatarName);
        using (FileStream fs = System.IO.File.Create(avatarFileFullPath))
        {
            await formModel.UserImageFile.CopyToAsync(fs);
        }

        //delete the old avatar file
        if (bsonDoc.TryGetValue("avatarId", out BsonValue bsonValue) && bsonValue.IsString)
        {
            string oldAvatarId = bsonValue.AsString;
            if (string.IsNullOrWhiteSpace(oldAvatarId))
            {
                DeleteAvatarFile(oldAvatarId);
            }
        }

        //update avatarId for user profile document on mongodb
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));

        string avatarLinkPath = Path.Combine(
            "/" +
            _storageManager.StorageDirInfo.Name,
            _storageManager.UserAvatarDirInfo.Name,
            avatarName
        );
        var update = Builders<BsonDocument>.Update.Set("avatarId", new BsonString(avatarLinkPath));
        await _mongoCollection.UpdateOneAsync(filter, update);

        return Ok(new { success = true });
    }
    public class UserImageFile_FormModel
    {
        public IFormFile UserImageFile { get; set; } = null!;
    }

    private void DeleteAvatarFile(string avatarId)
    {
        string avatarFilePath = Path.Combine(_storageManager.UserAvatarDirInfo.FullName, avatarId);

        if (System.IO.File.Exists(avatarFilePath))
        {
            System.IO.File.Delete(avatarFilePath);
        }
    }

    [HttpDelete]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserImage()
    {
        if (!Guid.TryParseExact(User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out Guid userGuid))
        {
            ModelState.AddModelError("User", "Couldn't parse user id!");
            return BadRequest(ModelState);
        }

        BsonDocument? bsonDoc = await _profileManager.GetFirstBsonDoc(userGuid, ["avatarId"]);
        if (bsonDoc is null)
        {
            ModelState.AddModelError("User", "User Profile not found!");
            return BadRequest(ModelState);
        }

        //delete the avatar file and update database
        if (bsonDoc.TryGetValue("avatarId", out BsonValue bsonValue) && bsonValue.IsString)
        {
            string avatarId = bsonValue.AsString;
            if (string.IsNullOrWhiteSpace(avatarId))
            {
                //delete file
                DeleteAvatarFile(avatarId);

                //update mongodb
                var filter = Builders<BsonDocument>.Filter
                .Eq("_id", new BsonBinaryData(userGuid, GuidRepresentation.Standard));
                var update = Builders<BsonDocument>.Update.Unset("avatarId");
                await _mongoCollection.UpdateOneAsync(filter, update);
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
        await _mongoCollection.UpdateOneAsync(filter, update);

        return Ok();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDescription([FromForm][StringLength(4000)] string description)
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
        await _mongoCollection.UpdateOneAsync(filter, update);

        return Ok();
    }



}