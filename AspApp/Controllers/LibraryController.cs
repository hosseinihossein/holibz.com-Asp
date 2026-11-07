using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace AspApp.Controllers;

[ApiController]
[Route("/api/[controller]/[action]")]
public class LibraryController : ControllerBase
{
    public LibraryController()
    {

    }

    [HttpGet]
    public async Task<IActionResult> GetLibraryCardInfo([FromQuery][StringLength(36)] string libId)
    {

    }

}