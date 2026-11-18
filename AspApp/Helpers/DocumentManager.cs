using AspApp.Database;
using AspApp.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AspApp.Helpers;

public class DocumentManager
{
    readonly Application_DbContext _appDb;
    readonly IMongoCollection<BsonDocument> _docCollection;
    readonly IWebHostEnvironment _env;
    readonly StorageManager _storageManager;
    public DocumentManager(
        Application_DbContext appDb,
        IMongoDatabase mongoAppDb,
        MongoDbConfigs mongoDbConfigs,
        IWebHostEnvironment env,
        StorageManager storageManager
    )
    {
        _appDb = appDb;
        _docCollection = mongoAppDb.GetCollection<BsonDocument>(mongoDbConfigs.DocumentCollection);
        _env = env;
        _storageManager = storageManager;
    }

    public async Task CreateNewDoc(DocumentManager_NewDoc_Model model)
    {
        // postgres
        App_Document_DbModel newDoc = new()
        {
            Owner = model.Owner,
        };
        App_ParentShelf_ChildDocument_DbModel newParentShelfChildDoc = new()
        {
            ParentShelf = model.ParentShelf,
            Document = newDoc,
        };

        _appDb.Documents.Add(newDoc);
        _appDb.ParentShelf_ChildDoc_Rels.Add(newParentShelfChildDoc);
        await _appDb.SaveChangesAsync();

        // mongodb
        var bsonDoc = new BsonDocument()
        {
            { "_id", new BsonBinaryData(newDoc.Id, GuidRepresentation.Standard) },
            { "ownerId", new BsonBinaryData(model.Owner.Id, GuidRepresentation.Standard) },
            { "title", new BsonString(model.Title) },
            //{ "totalNumberOfElements", new BsonInt32(0) },
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

        await _docCollection.InsertOneAsync(bsonDoc);
    }

    public async Task<string> SaveImageAndGetUrl(IFormFile imageFile)
    {
        string imageName = Guid.NewGuid().ToString("D") + ".webp";
        string imageFileFullPath = Path.Combine(_storageManager.DocumentImageDirInfo.FullName, imageName);
        using (FileStream fs = System.IO.File.Create(imageFileFullPath))
        {
            await imageFile.CopyToAsync(fs);
        }

        return Path.Combine(
            "/" +
            _storageManager.StorageDirInfo.Name,
            _storageManager.DocumentImageDirInfo.Name,
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
public class DocumentManager_NewDoc_Model : Document_NewDoc_FormModel
{
    public App_User_DbModel Owner { get; set; } = null!;
    public App_Shelf_DbModel ParentShelf { get; set; } = null!;

    public DocumentManager_NewDoc_Model(
        Document_NewDoc_FormModel formModel,
        App_User_DbModel owner,
        App_Shelf_DbModel parentLib
    )
    {
        ParentShelfId = formModel.ParentShelfId;
        Title = formModel.Title;
        Description = formModel.Description;
        Image = formModel.Image;
        Owner = owner;
        ParentShelf = parentLib;
    }
}