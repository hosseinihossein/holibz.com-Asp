using System.ComponentModel.DataAnnotations;

namespace AspApp.Models;

public class Shelf_NewShelf_FormModel
{
    public Guid ParentLibId { get; set; }

    [StringLength(60, MinimumLength = 3)]
    public string Title { get; set; } = null!;

    [StringLength(400)]
    public string? Description { get; set; }

    public IFormFile? Image { get; set; }
}

public class Shelf_SubmitImage_FormModel
{
    [StringLength(36)]
    public string ShelfId { get; set; } = null!;
    public IFormFile ImageFile { get; set; } = null!;
}
public class Shelf_SubmitTitle_FormModel
{
    [StringLength(36)]
    public string ShelfId { get; set; } = null!;

    [StringLength(60, MinimumLength = 3)]
    public string Title { get; set; } = null!;
}
public class Shelf_SubmitDescription_FormModel
{
    [StringLength(36)]
    public string ShelfId { get; set; } = null!;

    [StringLength(400)]
    public string Description { get; set; } = null!;
}