using System.ComponentModel.DataAnnotations;
using AspApp.Helpers;

namespace AspApp.Models;

public class Identity_Login_FormModel : ITurnstileModel
{
    public string? ReturnUrl { get; set; }

    [StringLength(128, MinimumLength = 3)]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [StringLength(60, MinimumLength = 3)]
    public string Password { get; set; } = string.Empty;

    [StringLength(2048)]
    public string? CfTurnstileResponse { get; set; }
}

public class Identity_LoginWithProvider_FormModel //: ITurnstileModel
{
    public string? ReturnUrl { get; set; }

    /*[StringLength(2048)]
    public string? CfTurnstileResponse { get; set; }*/
}

public class Identity_SignupFormModel : ITurnstileModel
{
    [StringLength(60, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [StringLength(128, MinimumLength = 3)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(60, MinimumLength = 3)]
    public string Password { get; set; } = string.Empty;

    [StringLength(2048)]
    public string? CfTurnstileResponse { get; set; }
}

public class Identity_ChangePasswordFormModel
{
    [StringLength(60, MinimumLength = 3)]
    public string CurrentPassword { get; set; } = string.Empty;

    [StringLength(60, MinimumLength = 3)]
    public string NewPassword { get; set; } = string.Empty;
}

public class Identity_ResetPasswordFormModel
{
    public string Token { get; set; } = string.Empty;

    [StringLength(60, MinimumLength = 3)]
    public string NewPassword { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(128, MinimumLength = 3)]
    public string Email { get; set; } = string.Empty;
}

public class Identity_EmailValidationFormModel : ITurnstileModel
{

    [StringLength(60)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(2048)]
    public string? CfTurnstileResponse { get; set; } = string.Empty;
}

