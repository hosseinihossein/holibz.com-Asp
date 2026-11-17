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