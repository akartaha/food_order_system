using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Exceptions;
using food_order_system1.Modles;
using food_order_system1.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace food_order_system1.Controllers
{
  [ApiController]
  [Route("MySYS/[controller]")]
  public class AuthController : ControllerBase
  {
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppUser _dbContext;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly IAuthService _authService;
    public AuthController(
      UserManager<ApplicationUser> userManager,
      AppUser dbContext,
      SignInManager<ApplicationUser> signInManager,
      IConfiguration Configuration,
      IAuthService authService
    )
    {
      _userManager = userManager;
      _dbContext = dbContext;
      _signInManager = signInManager;
      _configuration = Configuration;
      _authService = authService;
    }

    [AllowAnonymous]
    [HttpGet("login_google_call")]
    public IActionResult LoginGoogle(string provider = "Google")
    {
      // The callback URL after Google login
      var redirectUrl = Url.Action(nameof(LoginWithGoogle), "Auth", null, Request.Scheme);
      var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
      return Challenge(properties, provider);
    }

    //-------------------------------------------
    // login with google 
    //------------------------------------------
    [AllowAnonymous]
    [HttpGet("login_with_google")]
    public async Task<IActionResult> LoginWithGoogle()
    {

      var result = await _authService.LoginWithGoogleService();
      return MapServiceResult(result);

    }

    // --------------------------------------
    // local user register
    //---------------------------------------
    [AllowAnonymous]
    [HttpPost("register_user")]
    public async Task<IActionResult> local_register([FromBody] CreateUserDTO request_user, [FromServices] IEmailService emailService)
    {
      var result = await _authService.LocalRegisterService(request_user, emailService);
      return MapServiceResult(result);
    }

    //--------------------------------
    // local user login
    //--------------------------------
    [AllowAnonymous]
    [HttpPost("local-login")]
    public async Task<IActionResult> LocalLogin([FromBody] LoginUserDTO request_user)
    {
      var result = await _authService.LocalLoginService(request_user);
      return MapServiceResult(result);

    }


    //------------------------------------------
    // get new accesstoken and refresh token 
    //-----------------------------------------
    [AllowAnonymous]
    [HttpPost("new_refreshtoken")]
    public async Task<IActionResult> get_refresh_token([FromBody] string token)
    {
      var result = await _authService.GetNewRefreshAccessTokenService(token);
      return MapServiceResult(result);

    }


    //--------------------------------
    // confirmation email endpoint
    //--------------------------------
    [AllowAnonymous]
    [HttpGet("ConfirmEmail")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string UserId, [FromQuery] string token)
    {
      var result = await _authService.ConfirmEmailService(UserId, token);
      return MapServiceResult(result);

    }

    //-------------------------------------------------
    // forget password
    //-------------------------------------------------
    [AllowAnonymous]
    [HttpPost("forget_password/email")]
    public async Task<IActionResult> ForgetPassword(string email, [FromServices] IEmailService emailService)
    {

      var result = await _authService.ForgetPasswordService(email, emailService);
      return MapServiceResult(result);


    }

    [AllowAnonymous]
    [HttpGet("reset_password")]
    public IActionResult ResetPasswordpage([FromQuery] string userId, [FromQuery] string token)
    {
      return Ok(new
      {
        userId = userId,
        token = token
      });
    }

    [AllowAnonymous]
    [HttpPost("reset_password")]
    public async Task<IActionResult> RestPassword([FromBody] RestlocaLPasswordDTO request_password)
    {
      var result = await _authService.RestPasswordService(request_password);
      return MapServiceResult(result);

    }

    private IActionResult MapServiceResult<T>(ServiceResult<T> result)
    {
      return result.StatusCode switch
      {
        404 => NotFound(result),
        400 => BadRequest(result),
        403 => Unauthorized(result),
        _ => Ok(result)
      };

    }


  }
}