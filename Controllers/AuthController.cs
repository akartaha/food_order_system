using System;
using System.Security.Claims;
using food_order_system1.DTOs;
using food_order_system1.Modles;
using food_order_system1.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace food_order_system1.Controllers
{
  /// <summary>
  /// Handles authentication operations such as:
  /// - Local registration And login
  /// - Google login
  /// - Email confirmation
  /// - Password reset
  /// - Token refresh
  /// </summary>
  //[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
  [ProducesResponseType(typeof(ServiceResult<string>), 401)]
  [ApiController]
  [Route("api/[controller]")]
  public class AuthController : ControllerBase
  {
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuthService _authService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Constructor to inject dependencies
    /// </summary>
    public AuthController(IAuthService authService,
        SignInManager<ApplicationUser> signInManager,
        IEmailService emailService,
        ILogger<AuthController> logger)
    {
      _signInManager = signInManager;
      _authService = authService;
      _emailService = emailService;
      _logger = logger;
    }

    /// <summary>
    /// Step 1: Redirect user to Google login page
    /// </summary>
    /// <param name="provider">External provider (default: Google)</param>
    /// <returns>Redirect to Google authentication</returns>
    [AllowAnonymous]
    [HttpGet("google")]
    public IActionResult LoginGoogle(string provider = "Google")
    {
      // Build callback URL after Google authentication
      var redirectUrl = Url.Action(nameof(GoggleCallback), "Auth", null, Request.Scheme);

      // Configure authentication properties
      var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);

      // Redirect user to external provider (Google)
      return Challenge(properties, provider);
    }

    /// <summary>
    /// Step 2: Handle Google callback and login/register user
    /// </summary>
    /// <returns>JWT tokens and user info</returns>
    [AllowAnonymous]
[HttpGet("google/callback")]
    [ProducesResponseType(typeof(ServiceResult<string>), 200)]
    [ProducesResponseType(typeof(ServiceResult<string>), 400)]
    [ProducesResponseType(typeof(ServiceResult<string>), 403)]
public async Task<IActionResult> GoggleCallback()
{
    _logger.LogInformation("Google login callback triggered");

    var GoogleUserInfo = await _signInManager.GetExternalLoginInfoAsync();

    if (GoogleUserInfo == null)
    {
        _logger.LogWarning("Google authentication failed: external login info is null");
        return MapServiceResult(new ServiceResult<string>
        {
            Success = false,
            Message = "google authentication failed",
            StatusCode = 403
        });
    }

    var Name = GoogleUserInfo.Principal.FindFirstValue(ClaimTypes.Name);
    var Email = GoogleUserInfo.Principal.FindFirstValue(ClaimTypes.Email);

    if (Email == null || Name == null)
    {
        _logger.LogWarning("Google login failed: missing email or name");
        return MapServiceResult(new ServiceResult<string>
        {
            Success = false,
            Message = "Failed to retrieve user information from Google.",
            StatusCode = 400
        });
    }

    var result = await _authService.LoginWithGoogleService(GoogleUserInfo);

    _logger.LogInformation("Google login processed");

    return MapServiceResult(result);
}

    /// <summary>
    /// Register a new user using email And password
    /// Sends email confirmation after successful registration
    /// </summary>
    /// <param name="request_user">User registration data</param>
    /// <returns>Success or failure result</returns>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(ServiceResult<string>), 201)]
    [ProducesResponseType(typeof(ServiceResult<string>), 400)]
public async Task<IActionResult> Register([FromBody] CreateUserDTO request_user)
{
    _logger.LogInformation("Register request received for {Email}", request_user.Email);

    var result = await _authService.LocalRegisterService(request_user, _emailService);

    return MapServiceResult(result);
}

    /// <summary>
    /// Login user using email and password
    /// </summary>
    /// <param name="request_user">Login credentials</param>
    /// <returns>JWT access and refresh tokens</returns>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ServiceResult<string>), 200)]
    [ProducesResponseType(typeof(ServiceResult<string>), 400)]
public async Task<IActionResult> Login([FromBody] LoginUserDTO request_user)
{
    _logger.LogInformation("Login attempt for {Email}", request_user.UserName);

    var result = await _authService.LocalLoginService(request_user);

    return MapServiceResult(result);
}

    /// <summary>
    /// Generate new access token using refresh token
    /// </summary>
    /// <param name="token">Refresh token</param>
    /// <returns>New access and refresh tokens</returns>
    [AllowAnonymous]
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ServiceResult<string>), 200)]
    [ProducesResponseType(typeof(ServiceResult<string>), 400)]
    [ProducesResponseType(typeof(ServiceResult<string>), 404)]
public async Task<IActionResult> GenerateNewTokens([FromBody] string token)
{
    if (string.IsNullOrWhiteSpace(token))
    {
        _logger.LogWarning("Refresh token request with empty token");
        return MapServiceResult(new ServiceResult<string>
        {
            Success = false,
            Message = "token is empty"
        });
    }

    _logger.LogInformation("Refresh token request received");

    var result = await _authService.GetNewRefreshAccessTokenService(token);

    return MapServiceResult(result);
}

    /// <summary>
    /// Confirm user email using token sent via email
    /// </summary>
    /// <param name="dto">Confirm email informations</param>
    /// <returns>Confirmation result</returns>
    [AllowAnonymous]
    [HttpGet("confirm-email")]
    [ProducesResponseType(typeof(ServiceResult<string>), 200)]
    [ProducesResponseType(typeof(ServiceResult<string>), 400)]
    [ProducesResponseType(typeof(ServiceResult<string>), 404)]
public async Task<IActionResult> ConfirmEmail([FromQuery] ConfirmEmailDTO dto)
{
    if (!ModelState.IsValid || string.IsNullOrWhiteSpace(dto.userId) || string.IsNullOrWhiteSpace(dto.token))
    {
        _logger.LogWarning("Confirm email request missing userId or token");
        return MapServiceResult(new ServiceResult<bool>
        {
            Success = false,
            Message = "User ID or token is missing",
            StatusCode = 400
        });
    }

    var result = await _authService.ConfirmEmailService(dto);

    if (!result.Success)
    {
        _logger.LogWarning("Email confirmation failed for user {UserId}", dto.userId);

        return new ContentResult
        {
            Content = "<h2 style='color:red;'>Email confirmation failed</h2>",
            ContentType = "text/html",
            StatusCode = result.StatusCode
        };
    }

    _logger.LogInformation("Email confirmed successfully for user {UserId}", dto.userId);

    return new ContentResult
    {
        Content = "<h2 style='color:green;'>Email confirmed successfully!</h2>",
        ContentType = "text/html",
        StatusCode = 200
    };
}
    /// <summary>
    /// Send password reset email to user
    /// </summary>
    /// <returns>Status of email sending</returns>
    [AllowAnonymous]
    [HttpPost("forget-password")]
    [ProducesResponseType(typeof(ServiceResult<string>), 200)]
    [ProducesResponseType(typeof(ServiceResult<string>), 400)]
    [ProducesResponseType(typeof(ServiceResult<string>), 404)]
public async Task<IActionResult> ForgetPassword([FromBody] ChangeEmailDTO dto)
{
    if (string.IsNullOrWhiteSpace(dto.Email))
    {
        _logger.LogWarning("Forget password request with empty email");
        return MapServiceResult(new ServiceResult<bool>
        {
            Success = false,
            Message = "Email is required",
            StatusCode = 400
        });
    }

    _logger.LogInformation("Forget password requested");

    var result = await _authService.ForgetPasswordService(dto.Email, _emailService);

    return MapServiceResult(result);
}

    /// <summary>
    /// Endpoint used by frontend to receive reset password data
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="token">Reset password token</param>
    /// <returns>Returns token and userId</returns>
    [AllowAnonymous]
   [HttpGet("reset-password")]
   public async Task<IActionResult> ResetPasswordpage(
    [FromQuery] string userId,
    [FromQuery] string token)
{
    if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
    {
        _logger.LogWarning("Reset password page accessed with missing parameters");

        return new ContentResult
        {
            Content = "<h2 style='color:red;'>Rest Password failed</h2>",
            ContentType = "text/html",
            StatusCode = 400
        };
    }

    _logger.LogInformation("Reset password page accessed for user {UserId}", userId);

    return new ContentResult
    {
        Content = $"<h2>Reset Password for user {userId}</h2>",
        ContentType = "text/html",
        StatusCode = 200
    };
}

    /// <summary>
    /// Reset user password using token
    /// </summary>
    /// <param name="request_password">Reset password data</param>
    /// <returns>Success or failure result</returns>
    [AllowAnonymous]
    [ProducesResponseType(typeof(ServiceResult<string>), 200)]
    [ProducesResponseType(typeof(ServiceResult<string>), 400)]
    [ProducesResponseType(typeof(ServiceResult<string>), 404)]

   [HttpPost("reset-password")]
public async Task<IActionResult> RestPassword([FromBody] RestlocaLPasswordDTO request_password)
{
    _logger.LogInformation("Reset password attempt for user {UserId}", request_password.user_id);

    var result = await _authService.RestPasswordService(request_password);

    return MapServiceResult(result);
}

    /// <summary>
    /// Maps service result to appropriate HTTP response
    /// </summary>
    /// <typeparam name="T">Type of response data</typeparam>
    /// <param name="result">Service result</param>
    /// <returns>HTTP response</returns>
   private IActionResult MapServiceResult<T>(ServiceResult<T> result)
        {
            return result.StatusCode switch
            {
                404 => NotFound(result),
                400 => BadRequest(result),
                403 => StatusCode(403, result),
                420 => BadRequest(result),
                500 => StatusCode(500, result),
                201 => Created("", result),
                _ => Ok(result)
            };
        }



  }
}