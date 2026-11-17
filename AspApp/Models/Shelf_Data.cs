namespace AspApp.Models;

public class ShelfCard_DataModel
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int TotalNumberOfDocs { get; set; }
    public Guid OwnerId { get; set; }
}