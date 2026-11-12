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