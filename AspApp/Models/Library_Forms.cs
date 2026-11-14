using System.ComponentModel.DataAnnotations;

namespace AspApp.Models;

public class Library_NewLibrary_FormModel
{
    [StringLength(60, MinimumLength = 3)]
    public string Title { get; set; } = null!;

    [StringLength(400)]
    public string? Description { get; set; }

    public IFormFile? Image { get; set; }
}

public class Library_SubmitImage_FormModel
{
    [StringLength(36)]
    public string LibraryId { get; set; } = null!;
    public IFormFile ImageFile { get; set; } = null!;
}
public class Library_SubmitTitle_FormModel
{
    [StringLength(36)]
    public string LibraryId { get; set; } = null!;

    [StringLength(60, MinimumLength = 3)]
    public string Title { get; set; } = null!;
}
public class Library_SubmitDescription_FormModel
{
    [StringLength(36)]
    public string LibraryId { get; set; } = null!;

    [StringLength(400)]
    public string Description { get; set; } = null!;
}