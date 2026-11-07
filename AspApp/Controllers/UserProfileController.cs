using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace AspApp.Controllers;

[ApiController]
[Route("/api/[controller]/[action]")]
public class UserProfileController : ControllerBase
{
    public UserProfileController()
    {

    }


    // ********** Profile **********
    [HttpGet]
    public async Task<IActionResult> GetProfileInfo([FromQuery][StringLength(36)] string userId)
    {

    }

    [HttpGet]
    public async Task<IActionResult> GetUserAvatarUrl([FromQuery][StringLength(36)] string userId)
    {

    }

    [HttpPost]
    [Authorize]
    [RequestSizeLimit(128 * 1024)]//128 KB
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitUserImage([FromForm] UserImageFile_FormModel formModel)
    {

        return Ok(new { success = true });
    }
    public class UserImageFile_FormModel
    {
        public IFormFile UserImageFile { get; set; } = null!;
    }

    [HttpDelete]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserImage()
    {

    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitProfileName([FromForm][StringLength(60)] string name)
    {

    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDescription([FromForm][StringLength(4000)] string description)
    {

    }



}