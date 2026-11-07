using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using AspApp.Helpers;
using AspApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenIddict.Abstractions;
using OpenIddict.Client.AspNetCore;
using OpenIddict.Client.WebIntegration;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AspApp.Controllers;

[ApiController]
[Route("/api/[controller]/[action]")]
public class IdentityController : ControllerBase
{
    private readonly SignInManager<IdentityUser<Guid>> _signInManager;
    private readonly UserManager<IdentityUser<Guid>> _userManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UsernameGenerator _usernameGenerator;

    private readonly string[] _googleScopeEmail = [Scopes.Email];
    private readonly string[] _gitHubScopes = ["read:user", "user:email"];

    private readonly IEmailSender _emailSender;
    private readonly bool _turnstileEnabled;

    private readonly MainConfigs _mainConfigs;

    private readonly UserProfileManager _userProfileManager;


    public IdentityController(
        SignInManager<IdentityUser<Guid>> signInManager,
        UserManager<IdentityUser<Guid>> userManager,
        IHttpClientFactory httpClientFactory,
        UsernameGenerator usernameGenerator,
        IEmailSender emailSender,
        IOptions<TurnstileConfigs> turnstileOptions,
        MainConfigs mainConfigs,
        UserProfileManager userAccountManager
    )
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _httpClientFactory = httpClientFactory;
        _usernameGenerator = usernameGenerator;
        _emailSender = emailSender;
        _turnstileEnabled = turnstileOptions.Value.Enabled;
        _mainConfigs = mainConfigs;
        _userProfileManager = userAccountManager;
    }



    // **********    Check Authentication    **********
    [HttpGet]
    public async Task<IActionResult> CheckAuthentication()
    {
        if ((User.Identity?.IsAuthenticated ?? false) &&
            (await _userManager.GetUserAsync(User)) is IdentityUser<Guid> user &&
            user is not null)
        {
            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.EmailConfirmed,
                roles = (await _userManager.GetRolesAsync(user)).ToArray(),
            });
        }
        return Ok(null);
    }


    // **********    Is Turnstile Enabled    **********
    [HttpGet]
    public IActionResult IsTurnstileEnabled()
    {
        return Ok(new { turnstileEnabled = _turnstileEnabled });
    }


    // **********    AntiForgery    **********
    [HttpGet]
    [GenerateAntiforgeryTokenCookie]
    public IActionResult GetAntiForgeryToken()
    {
        return Ok();
    }



    // ************** logins ************
    [HttpPost]
    [ValidateTurnstile]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] Identity_Login_FormModel formModel)
    {
        IdentityUser<Guid>? user;
        if (formModel.UsernameOrEmail.Contains('@'))
        {
            user = await _userManager.FindByEmailAsync(formModel.UsernameOrEmail);
        }
        else
        {
            user = await _userManager.FindByNameAsync(formModel.UsernameOrEmail);
        }

        if (user is null)
        {
            //ModelState.AddModelError("UsernameOrEmail", "Couldn't find any user with the specified Username or Email");
            //return BadRequest(ModelState);
            return NotFound(new { message = "User not found." });
        }

        Microsoft.AspNetCore.Identity.SignInResult result =
        await _signInManager.PasswordSignInAsync(user, formModel.Password, true, false);

        if (result.Succeeded)
        {
            return Redirect(formModel.ReturnUrl ?? "/");
        }

        ModelState.AddModelError("Password", "Invalid credentials");
        return BadRequest(ModelState);
    }

    [HttpPost]
    //[ValidateTurnstile]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWithGitHub([FromForm] Identity_LoginWithProvider_FormModel formModel)
    {
        var properties = new AuthenticationProperties
        {
            // Only allow local return URLs to prevent open redirect attacks.
            RedirectUri = Url.IsLocalUrl(formModel.ReturnUrl) ? formModel.ReturnUrl : "~/",
        };
        properties.SetParameter("scope", _gitHubScopes);

        // Ask the OpenIddict client middleware to redirect the user agent to GitHub.
        return Challenge(properties, OpenIddictClientWebIntegrationConstants.Providers.GitHub);
    }

    [HttpPost]
    //[ValidateTurnstile]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWithGoogle([FromForm] Identity_LoginWithProvider_FormModel formModel)
    {
        var properties = new AuthenticationProperties
        {
            // Only allow local return URLs to prevent open redirect attacks.
            RedirectUri = Url.IsLocalUrl(formModel.ReturnUrl) ? formModel.ReturnUrl : "~/",
        };
        properties.SetParameter("scope", _googleScopeEmail);

        // Ask the OpenIddict client middleware to redirect the user agent to Google.
        return Challenge(properties, OpenIddictClientWebIntegrationConstants.Providers.Google);
    }



    // ************** callbacks ************
    [HttpGet, HttpPost, IgnoreAntiforgeryToken]
    public async Task<ActionResult> GitHubLoginCallback()
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);

        if (result is not { Succeeded: true, Principal.Identity.IsAuthenticated: true })
        {
            throw new InvalidOperationException("The external authorization data cannot be used for authentication.");
        }
        if (result.Principal.GetClaim(ClaimTypes.NameIdentifier) is null)
        {
            throw new Exception("NameIdentifier can NOT be null!");
        }

        var accessToken = result.Properties.GetTokenValue("access_token");
        //Console.WriteLine($"accessToken: {accessToken}");

        var backchannelAccessToken = result.Properties.GetTokenValue(
            OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken
        );
        //Console.WriteLine($"backChannelAccessToken: {backchannelAccessToken}");

        var chosenAccessToken = string.IsNullOrWhiteSpace(accessToken) ?
        backchannelAccessToken : accessToken;

        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", chosenAccessToken);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MyAspNetApp");

        string res = await httpClient.GetStringAsync("https://api.github.com/user/emails");
        //Console.WriteLine($"\n***** res = {res}");
        var jsonDoc = JsonDocument.Parse(res);

        string? userEmail = null;
        bool emailVerified = false;
        //string? emailVisibility = "private";
        foreach (var emailEntry in jsonDoc.RootElement.EnumerateArray())
        {
            if (emailEntry.GetProperty("primary").GetBoolean())
            {
                userEmail = emailEntry.GetProperty("email").GetString();
                emailVerified = emailEntry.GetProperty("verified").GetBoolean();
                //emailVisibility = emailEntry.GetProperty("visibility").GetString();
                break;
            }
        }

        if (userEmail is null)
        {
            ModelState.AddModelError("Email", "Couldn't get the user's email from github!");
            return BadRequest(ModelState);
        }

        string loginProvider = "GitHub";
        string providerKey = result.Principal.GetClaim(ClaimTypes.NameIdentifier)!;
        UserLoginInfo userLoginInfo = new(loginProvider, providerKey, loginProvider);
        var user = await _userManager.FindByLoginAsync(loginProvider, providerKey);
        if (user is null)
        {
            var userByEmail = await _userManager.FindByEmailAsync(userEmail!);
            if (userByEmail is null)
            {
                var usernameHint = result.Principal.GetClaim("login");
                if (usernameHint is not null)
                {
                    usernameHint = Regex.Replace(usernameHint, @"[^a-zA-Z0-9._]", "");
                }
                string username = _usernameGenerator.GetUniqueInMilliSeconds(usernameHint);

                user = new()
                {
                    UserName = username,
                    Email = userEmail,
                    EmailConfirmed = emailVerified,
                };
                var userCreationResult = await _userManager.CreateAsync(user);
                if (!userCreationResult.Succeeded)
                {
                    foreach (var error in userCreationResult.Errors)
                    {
                        ModelState.AddModelError("User", error.Description);
                    }
                    return BadRequest(ModelState);
                }

                if (!user.EmailConfirmed)
                {
                    //send email validation link
                    await SendEmailValidationLink(user);
                }

                // send new user to user account manager
                NewUserAccountModel newUserAccountModel = new()
                {
                    Id = user.Id,
                    AvatarUrl = result.Principal.GetClaim("avatar_url"),
                };
                await _userProfileManager.SubmitNewUserAccount(newUserAccountModel);
            }
            else
            {
                user = userByEmail;
            }
            var addLoginResult = await _userManager.AddLoginAsync(user, userLoginInfo);
            //Console.WriteLine($"***** addLoginResult: {addLoginResult}");
        }

        var exSigninResult = await _signInManager.ExternalLoginSignInAsync(loginProvider, providerKey, true);
        //Console.WriteLine($"***** exSigninResult: {exSigninResult}");

        return Redirect(result.Properties.RedirectUri ?? "/");
    }


    [HttpGet, HttpPost, IgnoreAntiforgeryToken]
    public async Task<ActionResult> GoogleLoginCallback()
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);

        if (result is not { Succeeded: true, Principal.Identity.IsAuthenticated: true })
        {
            ModelState.AddModelError("GitHubLogin", "The external authorization data cannot be used for authentication.");
            return BadRequest(ModelState);
            //throw new InvalidOperationException("The external authorization data cannot be used for authentication.");
        }
        if (result.Principal.GetClaim(ClaimTypes.NameIdentifier) is null)
        {
            ModelState.AddModelError("NameIdentifier", "NameIdentifier can NOT be null!");
            return BadRequest(ModelState);
            //throw new Exception("NameIdentifier can NOT be null!");
        }
        if (result.Principal.GetClaim(ClaimTypes.Email) is null)
        {
            ModelState.AddModelError("Email", "Email can NOT be null!");
            return BadRequest(ModelState);
            //throw new Exception("Email can NOT be null!");
        }

        string userEmail = result.Principal.GetClaim(ClaimTypes.Email)!;
        string loginProvider = "Google";
        string providerKey = result.Principal.GetClaim(ClaimTypes.NameIdentifier)!;
        UserLoginInfo userLoginInfo = new(loginProvider, providerKey, loginProvider);
        var user = await _userManager.FindByLoginAsync(loginProvider, providerKey);
        if (user is null)
        {
            var userByEmail = await _userManager.FindByEmailAsync(userEmail);
            if (userByEmail is null)
            {
                string username = _usernameGenerator.GetUniqueInMilliSeconds();
                user = new()
                {
                    UserName = username,
                    Email = userEmail,
                    // why gmail needs to be verified!
                    EmailConfirmed = bool.Parse(result.Principal.GetClaims("email_verified")[0].ToLower())
                };
                var userCreationResult = await _userManager.CreateAsync(user);
                if (!userCreationResult.Succeeded)
                {
                    foreach (var error in userCreationResult.Errors)
                    {
                        ModelState.AddModelError("User", error.Description);
                    }
                    return BadRequest(ModelState);
                }

                if (!user.EmailConfirmed)
                {
                    //send email validation link
                    await SendEmailValidationLink(user);
                }

                // send new user to user account manager
                NewUserAccountModel newUserAccountModel = new()
                {
                    Id = user.Id,
                    AvatarUrl = result.Principal.GetClaim("picture"),
                };
                await _userProfileManager.SubmitNewUserAccount(newUserAccountModel);
            }
            else
            {
                user = userByEmail;
            }
            var addLoginResult = await _userManager.AddLoginAsync(user, userLoginInfo);
            //Console.WriteLine($"addLoginResult: {addLoginResult}");
        }

        var exSigninResult = await _signInManager.ExternalLoginSignInAsync(loginProvider, providerKey, true);
        //Console.WriteLine($"exSigninResult: {JsonSerializer.Serialize(exSigninResult)}");

        return Redirect(result.Properties.RedirectUri ?? "/api/identity/Authorization/Authorize");
    }



    // **********    Signup    **********
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutAsync()
    {
        await _signInManager.SignOutAsync();
        return Ok();
    }



    // **********    Signup    **********
    [HttpPost]
    [ValidateTurnstile]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Signup([FromForm] Identity_SignupFormModel signupModel)
    {
        IdentityUser<Guid> user = new IdentityUser<Guid>()
        {
            UserName = signupModel.Username,
            Email = signupModel.Email,
            EmailConfirmed = false,
        };
        IdentityResult result = await _userManager.CreateAsync(user, signupModel.Password);
        if (result.Succeeded)
        {
            //send email validation link
            _ = SendEmailValidationLink(user);

            // send new user to user account manager
            NewUserAccountModel newUserAccountModel = new()
            {
                Id = user.Id,
            };
            await _userProfileManager.SubmitNewUserAccount(newUserAccountModel);

            // signin to asign the authorization cookie to the agant
            await _signInManager.SignInAsync(user, true);

            return Ok(new { success = true });
        }

        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError("Signup", error.Description);
        }
        return BadRequest(ModelState);
    }



    // **********    Email    **********
    private async Task SendEmailValidationLink(IdentityUser<Guid> user)
    {
        //***** Generate Email Validation Token *****
        string token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        //***** Sending Email *****
        string emailMessage = $"<h4>Hi dear {user.UserName}</h4>" +
        "<p>Please click " +
        $"<a href='{_mainConfigs.MainDomainName}/api/Identity/ConfirmEmail?token={Uri.EscapeDataString(token)}' " +
        "target='_blank'>'Here'</a>" +
        " to confirm your email address.</p>";

        await _emailSender.SendEmailAsync(user.UserName ?? "User", user.Email!, "Email Validation", emailMessage);
    }

    [HttpGet]
    [Authorize]//if user entered a wrong email address, the attacker can not confirm the account
    public async Task<IActionResult> ConfirmEmail([FromQuery][StringLength(2048)] string token)
    {
        //IdentityUser<Guid>? user = await _userManager.FindByEmailAsync(email);
        IdentityUser<Guid>? user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            //return error to warning page about user not found
            //return Redirect("~/Warning?status=danger&msg=User Not found!");
            ModelState.AddModelError("ConfirmEmail", "User not found!");
            return BadRequest(ModelState);
        }

        if (user.EmailConfirmed)
        {
            //return info to warning page about user already been confirmed
            //return Redirect("~/Warning?status=danger&msg=Your Email has already been confirmed!");
            ModelState.AddModelError("ConfirmEmail", "Your Email has already been confirmed!");
            return BadRequest(ModelState);
        }

        // don't need to use Uri.UnescapeStrinData(token), it's done automatically in binding step
        IdentityResult result = await _userManager.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            //return Redirect("~/UserProfile");
            // force the user to authorize again to refresh the oidc tokens
            //return Redirect("/angular/logout/page/route");//that handles request to ~/Api/Authorization/Logout
            return Ok(new { success = true });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("ConfirmEmail", error.Description);
        }

        return BadRequest(ModelState);
    }

    [HttpGet]
    public async Task<IActionResult> CheckEmail([FromQuery][StringLength(128, MinimumLength = 3)] string email)
    {
        bool userExist = await _userManager.Users
        .AnyAsync(u => u.NormalizedEmail == _userManager.NormalizeEmail(email));

        return Ok(new { isTaken = userExist });
    }

    [HttpPost]
    [Authorize]
    [ValidateTurnstile]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendEmailValidation()
    {
        IdentityUser<Guid>? user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            ModelState.AddModelError("User", "User Not found!");
            return BadRequest(ModelState);
        }

        if (user.EmailConfirmed)
        {
            ModelState.AddModelError("ResendEmailValidation", "Your Email has already been confirmed!");
            return BadRequest(ModelState);
        }

        await SendEmailValidationLink(user);

        return Ok(new { success = true });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeEmail([FromForm][StringLength(128)] string newEmail)
    {
        IdentityUser<Guid>? user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            //return Redirect("~/Warning?status=danger&msg=User Not found!");
            ModelState.AddModelError("User", "User Not Found! You need to authenticate.");
            return BadRequest(ModelState);
        }

        if (user.Email == newEmail)
        {
            ModelState.AddModelError("Email", "The current and the new email addresses are the same!");
        }
        else
        {
            await SendChangeEmailValidationLink(user, newEmail);

            return Ok(new { success = true });
        }
        return BadRequest(ModelState);
    }

    private async Task SendChangeEmailValidationLink(IdentityUser<Guid> user, string newEmail)
    {
        //***** Generate Email Validation Token *****
        string token = await _userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        //***** Sending Email *****
        string emailMessage = $"<h4>Hi dear {user.UserName}</h4>" +
        "<p>Please click " +
        $"<a href='{_mainConfigs.MainDomainName}/api/Identity/ConfirmNewEmail?token={Uri.EscapeDataString(token)}&newEmail={Uri.EscapeDataString(newEmail)}' " +
        "target='_blank'>'Here'</a>" +
        " to confirm your new email.</p>";

        await _emailSender.SendEmailAsync(user.UserName ?? "User", newEmail, "Confirm New Email", emailMessage);
    }

    [HttpGet]
    [Authorize]//if user entered a wrong email address, the attacker can not confirm the account
    public async Task<IActionResult> ConfirmNewEmail([FromQuery][StringLength(2048)] string token,
    [FromQuery][StringLength(128)] string newEmail)
    {
        IdentityUser<Guid>? user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            //return error to warning page about user not found
            //return Redirect("~/Warning?status=danger&msg=User Not found!");
            ModelState.AddModelError("ConfirmNewEmail", "User not found!");
            return BadRequest(ModelState);
        }

        if (user.Email == newEmail)
        {
            //return info to warning page about user already been confirmed
            //return Redirect("~/Warning?status=danger&msg=Your Email has already been confirmed!");
            ModelState.AddModelError("ConfirmNewEmail", "Your new Email has already been confirmed!");
            return BadRequest(ModelState);
        }

        IdentityResult result = await _userManager.ChangeEmailAsync(user, newEmail, token);
        if (result.Succeeded)
        {
            //return Redirect("~/UserProfile");
            // force the user to authorize again to refresh the oidc tokens
            //return Redirect("/angular/logout/page/route");//that handles request to ~/Api/Authorization/Logout
            return Ok(new { success = true });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("ConfirmNewEmail", error.Description);
        }

        return BadRequest(ModelState);
    }



    // **********    Password    **********
    [HttpPost]
    [ValidateTurnstile]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgetPassword([FromForm][StringLength(128)] string email)
    {
        IdentityUser<Guid>? user = await _userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            await SendResetPasswordLink(user);

            return Ok(new { success = true });
        }
        //ModelState.AddModelError("Email", "Email Not found!");
        //return BadRequest(ModelState);
        return NotFound(new { message = "Email not found." });
    }

    private async Task SendResetPasswordLink(IdentityUser<Guid> user)
    {
        //***** Generate Email Validation Token *****
        string token = await _userManager.GeneratePasswordResetTokenAsync(user);

        //***** Sending Email *****
        string emailMessage = $"<h4>Hi dear {user.UserName}</h4>" +
        "<p>Please click " +
        $"<a href='{_mainConfigs.MainDomainName}/Identity/ResetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}' " +
        "target='_blank'>'Here'</a>" +
        " to proceed resetting your password.</p>";

        await _emailSender.SendEmailAsync(user.UserName ?? "User", user.Email!, "Reset Password", emailMessage);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitResetPassword(Identity_ResetPasswordFormModel formModel)
    {
        IdentityUser<Guid>? user = await _userManager.FindByEmailAsync(formModel.Email);
        if (user is null)
        {
            //ModelState.AddModelError("User", "User Not Found! Email is invalid!");
            //return BadRequest(ModelState);
            return NotFound(new { message = "User not found. Email is invalid!" });
        }

        var result =
        await _userManager.ResetPasswordAsync(user, formModel.Token, formModel.NewPassword);
        if (result.Succeeded)
        {
            return Ok(new { success = true });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("SubmitResetPassword", error.Description);
        }
        return BadRequest(ModelState);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitChangePassword(Identity_ChangePasswordFormModel formModel)
    {
        IdentityUser<Guid>? user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            ModelState.AddModelError("User", "User Not Found! You need to authenticate.");
            return BadRequest(ModelState);
        }

        var result =
        await _userManager.ChangePasswordAsync(user, formModel.CurrentPassword, formModel.NewPassword);
        if (result.Succeeded)
        {
            // force the user to authorize again to refresh the oidc tokens
            //return Redirect("/angular/logout/page/route");//that handles request to ~/Api/Authorization/Logout
            return Ok(new { success = true });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("SubmitResetPassword", error.Description);
        }
        return BadRequest(ModelState);
    }



    // **********    UserName    **********
    [HttpGet]
    public async Task<IActionResult> CheckUsername([FromQuery][StringLength(60, MinimumLength = 3)] string username)
    {
        bool userExist = await _userManager.Users
        .AnyAsync(u => u.NormalizedUserName == _userManager.NormalizeName(username));

        return Ok(new { isTaken = userExist });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitUsername([FromForm][StringLength(60, MinimumLength = 3)] string username)
    {
        IdentityUser<Guid>? user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            ModelState.AddModelError("User", "User Not Found! You need to login.");
            return BadRequest(ModelState);
        }

        var result = await _userManager.SetUserNameAsync(user, username);
        if (result.Succeeded)
        {
            return Ok(new { success = true });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("Username", error.Description);
        }
        return BadRequest(ModelState);
    }

    [HttpGet]
    public async Task<IActionResult> GetUsername([FromQuery] Guid userId)
    {
        // Queries only the UserName column from the database, minimizing overhead
        var username = await _userManager.Users
        .Where(u => u.Id == userId)
        .Select(u => u.UserName)
        .FirstOrDefaultAsync();

        if (username == null)
        {
            // Return 404 Not Found for a missing resource
            return NotFound(new { message = "User not found." });
        }

        return Ok(new { username });
    }

}