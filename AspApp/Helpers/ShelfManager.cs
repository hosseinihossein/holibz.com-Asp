using AspApp.Database;
using AspApp.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AspApp.Helpers;

public class ShelfManager
{
    readonly Application_DbContext _appDb;
    readonly IMongoCollection<BsonDocument> _shelfCollection;
    readonly IWebHostEnvironment _env;
    readonly StorageManager _storageManager;
    readonly BsonManager _bsonManager;
    public ShelfManager(
        Application_DbContext appDb,
        IMongoDatabase mongoAppDb,
        MongoDbConfigs mongoDbConfigs,
        IWebHostEnvironment env,
        StorageManager storageManager,
        BsonManager bsonManager
    )
    {
        _appDb = appDb;
        _shelfCollection = mongoAppDb.GetCollection<BsonDocument>(mongoDbConfigs.ShelfCollection);
        _env = env;
        _storageManager = storageManager;
        _bsonManager = bsonManager;
    }

    public async Task CreateNewShelf(ShelfManager_NewShelf_Model model)
    {
        // postgres
        App_Shelf_DbModel newShelf = new()
        {
            Owner = model.Owner,
        };
        App_ParentLibrary_ChildShelf_DbModel newParentLibChildShelfRel = new()
        {
            ParentLib = model.ParentLib,
            Shelf = newShelf,
        };

        _appDb.Shelves.Add(newShelf);
        _appDb.ParentLib_ChildShelf_Rels.Add(newParentLibChildShelfRel);
        await _appDb.SaveChangesAsync();

        // mongodb
        var bsonDoc = new BsonDocument()
        {
            { "_id", new BsonBinaryData(newShelf.Id, GuidRepresentation.Standard) },
            { "ownerId", new BsonBinaryData(model.Owner.Id, GuidRepresentation.Standard) },
            { "title", new BsonString(model.Title) },
            { "totalNumberOfDocs", new BsonInt32(0) },
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

        await _shelfCollection.InsertOneAsync(bsonDoc);
    }

    public async Task<string> SaveImageAndGetUrl(IFormFile imageFile)
    {
        string imageName = Guid.NewGuid().ToString("D") + ".webp";
        string imageFileFullPath = Path.Combine(_storageManager.ShelfImageDirInfo.FullName, imageName);
        using (FileStream fs = System.IO.File.Create(imageFileFullPath))
        {
            await imageFile.CopyToAsync(fs);
        }

        return Path.Combine(
            "/" +
            _storageManager.StorageDirInfo.Name,
            _storageManager.ShelfImageDirInfo.Name,
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

    public async Task IncreaseTotalNumberOfDocs(Guid shelfId)
    {
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(shelfId, GuidRepresentation.Standard));
        var update = Builders<BsonDocument>.Update.Inc("totalNumberOfDocs", new BsonInt32(1));
        await _shelfCollection.UpdateOneAsync(filter, update);
    }
    public async Task DecreaseTotalNumberOfDocs(Guid shelfId)
    {
        var filter = Builders<BsonDocument>.Filter
        .Eq("_id", new BsonBinaryData(shelfId, GuidRepresentation.Standard));
        var update = Builders<BsonDocument>.Update.Inc("totalNumberOfDocs", new BsonInt32(-1));
        await _shelfCollection.UpdateOneAsync(filter, update);
    }

}
public class ShelfManager_NewShelf_Model : Shelf_NewShelf_FormModel
{
    public App_User_DbModel Owner { get; set; } = null!;
    public App_Library_DbModel ParentLib { get; set; } = null!;

    public ShelfManager_NewShelf_Model(
        Shelf_NewShelf_FormModel formModel,
        App_User_DbModel owner,
        App_Library_DbModel parentLib
    )
    {
        ParentLibId = formModel.ParentLibId;
        Title = formModel.Title;
        Description = formModel.Description;
        Image = formModel.Image;
        Owner = owner;
        ParentLib = parentLib;
    }
}