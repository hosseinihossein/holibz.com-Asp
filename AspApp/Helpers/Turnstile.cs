using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using AspApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace AspApp.Helpers;

public class TurnstileService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _secretKey;
    private const string SiteverifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    private readonly bool _turnstileEnabled;


    public TurnstileService(
        IHttpClientFactory httpClientFactory,
        IOptions<TurnstileConfigs> turnstileOptions
    )
    {
        _httpClientFactory = httpClientFactory;
        _secretKey = turnstileOptions.Value.SecretKey;
        _turnstileEnabled = turnstileOptions.Value.Enabled;
    }

    public async Task<TurnstileResponse?> ValidateTokenAsync(HttpContext httpContext, string? token = null)
    {
        if (!_turnstileEnabled)
        {
            return new TurnstileResponse()
            {
                Success = true,
            };
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new TurnstileResponse()
            {
                Success = false,
                ErrorCodes = new[] { "token-null" }
            };
        }

        var remoteip = httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ??
        httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
        httpContext.Connection.RemoteIpAddress?.ToString();

        var parameters = new Dictionary<string, string>
        {
            { "secret", _secretKey },
            { "response", token }
        };

        if (!string.IsNullOrEmpty(remoteip))
        {
            parameters.Add("remoteip", remoteip);
        }

        var postContent = new FormUrlEncodedContent(parameters);
        /*var formData = new
        {
            secret = _secretKey,
            response = token
        };*/

        try
        {
            var _httpClient = _httpClientFactory.CreateClient();
            var response = await _httpClient.PostAsync(SiteverifyUrl, postContent);
            //var response = await _httpClient.PostAsJsonAsync(SiteverifyUrl, formData);
            var stringContent = await response.Content.ReadAsStringAsync();

            TurnstileResponse? turnstileResponse =
            JsonSerializer.Deserialize<TurnstileResponse>(stringContent);

            return turnstileResponse;
        }
        catch (Exception)
        {
            return new TurnstileResponse
            {
                Success = false,
                ErrorCodes = new[] { "server-internal-error" }
            };
        }
    }
}

public class TurnstileResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error-codes")]
    public string[] ErrorCodes { get; set; } = [];
}

// ************************* Validator *************************
public class ValidateTurnstileAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        string? cfTurnstileResponse = null;

        if (context.ActionArguments.TryGetValue("CfTurnstileResponse", out object? cfRes) &&
            cfRes is string cfResArg
        )
        {
            cfTurnstileResponse = cfResArg;
        }
        else
        {
            foreach (var argument in context.ActionArguments)
            {
                if (argument.Value is ITurnstileModel model)
                {
                    cfTurnstileResponse = model.CfTurnstileResponse;
                    break;
                }
            }
        }

        if (cfTurnstileResponse is null)
        {
            context.ModelState.AddModelError("TurnstileError", $"Client's CfTurnstileResponse is null in action '{context.ActionDescriptor.DisplayName}'");
            context.Result = new BadRequestObjectResult(context.ModelState);
            return;
        }

        // inject the turnstile service
        TurnstileService turnstileService = context.HttpContext.RequestServices.GetRequiredService<TurnstileService>();

        TurnstileResponse? turnstileResponse =
        await turnstileService.ValidateTokenAsync(context.HttpContext, cfTurnstileResponse);

        if (turnstileResponse is null || turnstileResponse.Success is false)
        {
            context.ModelState.AddModelError("TurnstileError",
            turnstileResponse is null ? "response null!" : string.Join(", ", turnstileResponse.ErrorCodes));
            context.Result = new BadRequestObjectResult(context.ModelState);
            return;
        }

        // execute action
        await next();
    }
}

public interface ITurnstileModel
{
    [StringLength(2048)]
    string? CfTurnstileResponse { get; set; }
}