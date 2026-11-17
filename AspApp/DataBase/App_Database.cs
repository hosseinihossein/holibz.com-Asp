using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AspApp.Database;

public class Application_DbContext : DbContext
{
    public Application_DbContext(DbContextOptions<Application_DbContext> options)
    : base(options) { }

    //*********** main tables ***********
    public DbSet<App_User_DbModel> Users { get; set; }
    public DbSet<App_Library_DbModel> Libraries { get; set; }
    public DbSet<App_Shelf_DbModel> Shelves { get; set; }
    public DbSet<App_Document_DbModel> Documents { get; set; }

    //*********** join tables ***********
    public DbSet<App_ParentLibrary_ChildShelf_DbModel> ParentLib_ChildShelf_Rels { get; set; }
    public DbSet<App_ParentShelf_ChildDocument_DbModel> ParentShelf_ChildDoc_Rels { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //*************************** Relationships *********************************
        //************************** User ***************************
        //*********** User-Libraries One-To-Many *********
        modelBuilder.Entity<App_User_DbModel>()
        .HasMany(u => u.Libraries)
        .WithOne(l => l.Owner)
        .HasForeignKey(l => l.OwnerId)
        .IsRequired(true);
        //*********** User-Shelves One-To-Many *********
        modelBuilder.Entity<App_User_DbModel>()
        .HasMany(u => u.Shelves)
        .WithOne(sh => sh.Owner)
        .HasForeignKey(sh => sh.OwnerId)
        .IsRequired(true);
        //*********** User-Docs One-To-Many *********
        modelBuilder.Entity<App_User_DbModel>()
        .HasMany(u => u.Documents)
        .WithOne(d => d.Owner)
        .HasForeignKey(d => d.OwnerId)
        .IsRequired(true);

        //************************** Library ***************************
        //*********** Library-ChildShelves One-To-Many *********
        modelBuilder.Entity<App_Library_DbModel>()
        .HasMany(l => l.Shelves)
        .WithOne(chSh => chSh.ParentLib)
        .HasForeignKey(chSh => chSh.ParentLibId)
        .IsRequired(true);

        //************************** Shelf ***************************
        //*********** Shelf-ParentLibs One-To-Many *********
        modelBuilder.Entity<App_Shelf_DbModel>()
        .HasMany(sh => sh.ParentLibraries)
        .WithOne(pL => pL.Shelf)
        .HasForeignKey(pL => pL.ShelfId)
        .IsRequired(true);
        //*********** Shelf-ChildDocs One-To-Many *********
        modelBuilder.Entity<App_Shelf_DbModel>()
        .HasMany(sh => sh.Documents)
        .WithOne(chD => chD.ParentShelf)
        .HasForeignKey(chD => chD.ParentShelfId)
        .IsRequired(true);

        //************************** Document ***************************
        //*********** Document-ParentShelves One-To-Many *********
        modelBuilder.Entity<App_Document_DbModel>()
        .HasMany(d => d.ParentShelves)
        .WithOne(pSh => pSh.Document)
        .HasForeignKey(pSh => pSh.DocumentId)
        .IsRequired(true);

        //************************** ParentLib_ChildShelf ***************************
        modelBuilder.Entity<App_ParentLibrary_ChildShelf_DbModel>()
        .HasKey(p => new { p.ParentLibId, p.ShelfId });

        //************************** ParentLib_ChildShelf ***************************
        modelBuilder.Entity<App_ParentShelf_ChildDocument_DbModel>()
        .HasKey(p => new { p.ParentShelfId, p.DocumentId });


        //*************************** Indexes *********************************
    }
}

public class App_User_DbModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<App_Library_DbModel> Libraries { get; set; } = [];
    public ICollection<App_Shelf_DbModel> Shelves { get; set; } = [];
    public ICollection<App_Document_DbModel> Documents { get; set; } = [];
}
public class App_Library_DbModel
{
    [Key]
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid OwnerId { get; set; }
    public App_User_DbModel Owner { get; set; } = null!;

    public ICollection<App_ParentLibrary_ChildShelf_DbModel> Shelves { get; set; } = [];
}
public class App_Shelf_DbModel
{
    [Key]
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid OwnerId { get; set; }
    public App_User_DbModel Owner { get; set; } = null!;

    public ICollection<App_ParentLibrary_ChildShelf_DbModel> ParentLibraries { get; set; } = [];
    public ICollection<App_ParentShelf_ChildDocument_DbModel> Documents { get; set; } = [];
}

public class App_Document_DbModel
{
    [Key]
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid OwnerId { get; set; }
    public App_User_DbModel Owner { get; set; } = null!;

    public ICollection<App_ParentShelf_ChildDocument_DbModel> ParentShelves { get; set; } = [];
}

/*public class Library_Element_DbModel
{
    [Key]
    public Guid Id {get;set;}
}*/

// ********** join tables **********
public class App_ParentLibrary_ChildShelf_DbModel
{
    public Guid ParentLibId { get; set; }
    public App_Library_DbModel ParentLib { get; set; } = null!;

    public Guid ShelfId { get; set; }
    public App_Shelf_DbModel Shelf { get; set; } = null!;
}
public class App_ParentShelf_ChildDocument_DbModel
{
    public Guid ParentShelfId { get; set; }
    public App_Shelf_DbModel ParentShelf { get; set; } = null!;

    public Guid DocumentId { get; set; }
    public App_Document_DbModel Document { get; set; } = null!;
}