using AspApp.Database;
using AspApp.Models;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AspApp.Helpers;

public class LibraryManager
{
    readonly Application_DbContext _appDb;
    readonly IMongoCollection<BsonDocument> _libraryCollection;
    readonly IWebHostEnvironment _env;
    readonly StorageManager _storageManager;
    public LibraryManager(
        Application_DbContext appDb,
        IMongoDatabase mongoAppDb,
        MongoDbConfigs mongoDbConfigs,
        IWebHostEnvironment env,
        StorageManager storageManager
    )
    {
        _appDb = appDb;
        _libraryCollection = mongoAppDb.GetCollection<BsonDocument>(mongoDbConfigs.LibraryCollection);
        _env = env;
        _storageManager = storageManager;
    }

    public async Task CreateNewLib(LibraryManager_NewLib_Model model)
    {
        // postgres
        App_Library_DbModel newLib = new()
        {
            Owner = model.Owner,
        };

        _appDb.Libraries.Add(newLib);
        await _appDb.SaveChangesAsync();

        // mongodb
        var bsonDoc = new BsonDocument()
        {
            { "_id", new BsonBinaryData(newLib.Id, GuidRepresentation.Standard) },
            { "ownerId", new BsonBinaryData(model.Owner.Id, GuidRepresentation.Standard) },
            { "title", new BsonString(model.Title) },
            { "createdAt", new BsonDateTime(DateTime.UtcNow) },
        };
        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            bsonDoc.Add("description", new BsonString(model.Description));
        }

        if (model.Image is not null)
        {
            string imageLinkPath = await SaveImageAndGetUrl(model.Image);
            bsonDoc.Add("imageUrl", new BsonString(imageLinkPath));
        }

        await _libraryCollection.InsertOneAsync(bsonDoc);
    }

    public async Task<string> SaveImageAndGetUrl(IFormFile imageFile)
    {
        string imageName = Guid.NewGuid().ToString("D") + ".webp";
        string imageFileFullPath = Path.Combine(_storageManager.LibraryImageDirInfo.FullName, imageName);
        using (FileStream fs = File.Create(imageFileFullPath))
        {
            await imageFile.CopyToAsync(fs);
        }

        return Path.Combine(
            "/" +
            _storageManager.StorageDirInfo.Name,
            _storageManager.LibraryImageDirInfo.Name,
            imageName
        );
    }

    public void DeleteImage(string imageUrl)
    {
        string imageFullPath = Path.Combine(_env.ContentRootPath, imageUrl);

        if (File.Exists(imageFullPath))
        {
            File.Delete(imageFullPath);
        }
    }

}

public class LibraryManager_NewLib_Model : Library_NewLibrary_FormModel
{
    public App_User_DbModel Owner { get; set; } = null!;

    public LibraryManager_NewLib_Model(
        Library_NewLibrary_FormModel formModel,
        App_User_DbModel owner
    )
    {
        Title = formModel.Title;
        Description = formModel.Description;
        Image = formModel.Image;
        Owner = owner;
    }
}
