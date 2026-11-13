using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AspApp.Models;

public class Library_DbContext : DbContext
{
    public Library_DbContext(DbContextOptions<Library_DbContext> options)
    : base(options) { }

    public DbSet<Library_User_DbModel> Users { get; set; }
    public DbSet<Library_Library_DbModel> Libraries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //*************************** Relationships *********************************
        //********************************** Owner ***********************************
        //*********** User-Libraries One-To-Many *********
        modelBuilder.Entity<Library_User_DbModel>()
        .HasMany(u => u.Libraries)
        .WithOne(l => l.Owner)
        .IsRequired(true);
    }
}

public class Library_User_DbModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid Id { get; set; }
    public ICollection<Library_Library_DbModel> Libraries { get; set; } = [];
}
public class Library_Library_DbModel
{
    [Key]
    public Guid Id { get; set; }
    public Library_User_DbModel Owner { get; set; } = null!;
}
/*public class Library_Shelf_DbModel
{
    [Key]
    public Guid Id {get;set;}
}
public class Library_Document_DbModel
{
    [Key]
    public Guid Id {get;set;}
}
public class Library_Element_DbModel
{
    [Key]
    public Guid Id {get;set;}
}*/