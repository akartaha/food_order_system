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
using food_order_system1.Modles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace food_order_system1.Service
{
    public interface IAuthService
    {
        Task<ServiceResult<string>> LoginWithGoogleService();
        Task<ServiceResult<string>> LocalRegisterService(CreateUserDTO request_user, IEmailService emailService);
        Task<ServiceResult<string>> LocalLoginService(LoginUserDTO request_user);
        Task<ServiceResult<string>> GetNewRefreshAccessTokenService(string token);
        Task<ServiceResult<bool>> ConfirmEmailService(string UserId, string token);
        Task<ServiceResult<string>> ForgetPasswordService(string email, IEmailService emailService);
        Task<ServiceResult<bool>> RestPasswordService(RestlocaLPasswordDTO request_password);

    }
    public class AuthService : IAuthService
    {
        private readonly AppUser _dbContext;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public AuthService(AppUser context,
         IAuthorizationService authorizationService,
         UserManager<ApplicationUser> userManager,
          IConfiguration configuration,
          SignInManager<ApplicationUser> signInManager,
          IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = context;
            _authorizationService = authorizationService;
            _userManager = userManager;
            _configuration = configuration;
            _signInManager = signInManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResult<string>> LoginWithGoogleService()
        {
            var GoogleUserInfo = await _signInManager.GetExternalLoginInfoAsync();

            if (GoogleUserInfo == null)
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "google authentication failed",
                    StatusCode = 403
                };

            var Name = GoogleUserInfo.Principal.FindFirstValue(ClaimTypes.Name);
            var Email = GoogleUserInfo.Principal.FindFirstValue(ClaimTypes.Email);
            if (Email == null || Name == null)
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "Failed to retrieve user information from Google.",
                    StatusCode = 400
                };

            var User = await _userManager.FindByEmailAsync(Email);

            if (User == null)
            // create new user 
            {
                User = new ApplicationUser
                {
                    UserName = Email,
                    Email = Email,
                    fullName = Name,
                    EmailConfirmed = true,
                    IsActive = false
                };

                var CreateUser = await _userManager.CreateAsync(User);
                if (!CreateUser.Succeeded)
                {
                    return new ServiceResult<string>
                    {
                        Success = false,
                        Message = "can not create user with google account",
                        StatusCode = 400
                    };
                }


                await _userManager.AddToRoleAsync(User, "Customer");
                await _userManager.AddLoginAsync(User, GoogleUserInfo);
            }

            if (!User.IsActive)
            {
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "please wait untill your account active by admin",
                    StatusCode = 400
                };
            }

            var token = await GenerateToken(User);
            var refToken = GenarateRefreshToken(User);
            _dbContext.refreshTokens.Add(refToken);
            await _dbContext.SaveChangesAsync();
            // genarate jwt token 

            if (token != string.Empty)
                return new ServiceResult<string>
                {
                    Success = true,
                    Data = token,
                    StatusCode = 200
                };
            else
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "faled to genarate token",
                    StatusCode = 400
                };
        }


        private async Task<string> GenerateToken(ApplicationUser user)
        {

            if (user == null) return string.Empty;
            var roles = await _userManager.GetRolesAsync(user);
            var claims = new List<Claim>
      {
        new Claim(ClaimTypes.NameIdentifier,user.Id),
        new Claim(ClaimTypes.Name,user.UserName),
        new Claim(ClaimTypes.Email,user.Email),
        new Claim("EmailConfirmed",user.EmailConfirmed.ToString())
      };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
             issuer: _configuration["jwt:Issuer"],
             audience: _configuration["jwt:Audience"],
             claims: claims,
             expires: DateTime.UtcNow.AddHours(2),
             signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        //--------------------------------------
        // generate refresh token 
        //--------------------------------------
        private RefreshToken GenarateRefreshToken(ApplicationUser user)
        {
            return new RefreshToken()
            {
                UserId = user.Id,
                User = user,
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                ExpiresAt = DateTime.UtcNow.AddDays(2),
            };

        }

        public async Task<ServiceResult<string>> LocalRegisterService(CreateUserDTO request_user, IEmailService emailService)
        {
            var isExist = await _userManager.FindByEmailAsync(request_user.Email);

            if (isExist != null)
            {
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "this user already exist",
                    StatusCode = 400
                };
            }
            var User = new ApplicationUser
            {
                userId = request_user.userId,
                UserName = request_user.UserName,
                Email = request_user.Email,
                fullName = request_user.fullName,
                PhoneNumber = request_user.PhoneNumber,
                IsActive = false
            };

            var CreateUser = await _userManager.CreateAsync(User, request_user.Password);
            if (!CreateUser.Succeeded)
            {
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "can not create user",
                    StatusCode = 400
                };
            }

            await _userManager.AddToRoleAsync(User, "Customer");

            var EmailConfirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(User);

            // URL encode the token because it can contain +, /, = characters
            var encoded_email_conf_token = System.Net.WebUtility.UrlEncode(EmailConfirmToken);

            var Request = _httpContextAccessor.HttpContext.Request;
            // Build the full URL manually
            var confirmationLink = $"{Request.Scheme}://{Request.Host}/MySYS/Auth/ConfirmEmail?userId={User.Id}&token={encoded_email_conf_token}";

            // Send confirmation email

            await emailService.SendEmailAsync(
                  User.Email,
                 "Confirm your email",
                 $"<h3>Welcome!</h3><p>Click <a href='{confirmationLink}'>here</a> to confirm your email.</p>"
             );



            return new ServiceResult<string>
            {
                Success = true,
                Message = $"{request_user.fullName}   is created  confirmation link   {confirmationLink}",
                StatusCode = 201,
                Data = User.Id
            };
        }

        public async Task<ServiceResult<string>> LocalLoginService(LoginUserDTO request_user)
        {
            var user = await _userManager.FindByEmailAsync(request_user.UserName);
            if (user == null)
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "Invalid username or password.",
                    StatusCode = 400
                };

            if (!user.EmailConfirmed)
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "Please confirm your email first.",
                    StatusCode = 403
                };

            var result = await _signInManager.PasswordSignInAsync(
                user,
                request_user.Password,
                isPersistent: false,
                lockoutOnFailure: true
            );

            if (result.IsLockedOut)
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "Account temporarily locked due to multiple failed attempts.",
                    StatusCode = 400
                };

            if (!result.Succeeded)
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "Invalid username or password.",
                    StatusCode = 400
                };

            if (!user.IsActive) return new ServiceResult<string>
            {
                Success = false,
                Message = "please wait untill your account active by admin",
                StatusCode = 400
            };

            var token = await GenerateToken(user);

            var refToken = GenarateRefreshToken(user);
            _dbContext.refreshTokens.Add(refToken);
            await _dbContext.SaveChangesAsync();


            return new ServiceResult<string>
            {
                Success = true,
                Data = $"access token = {token} refresh token = {refToken}",
                StatusCode = 200
            };

        }

        public async Task<ServiceResult<string>> GetNewRefreshAccessTokenService(string token)
        {
            var FindToken = await _dbContext.refreshTokens.
                Include(r => r.User)
               .FirstOrDefaultAsync(r => r.Token == token && !r.IsRevoked && r.ExpiresAt > DateTime.UtcNow);
            if (FindToken == null)
            {
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "no token found",
                    StatusCode = 404
                };
            }
            FindToken.IsRevoked = true;
            var AccesToken = await GenerateToken(FindToken.User);
            var RefreshToken = GenarateRefreshToken(FindToken.User);

            _dbContext.refreshTokens.Add(RefreshToken);
            await _dbContext.SaveChangesAsync();
            return new ServiceResult<string>
            {
                Success = true,
                Data = $"access token = {token} refresh token = {RefreshToken}",
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<bool>> ConfirmEmailService(string UserId, string token)
        {
            if (UserId == null || token == null)
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "User ID or token is missing",
                    StatusCode = 400
                };

            var user = await _userManager.FindByIdAsync(UserId);
            if (user == null)
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "User not found",
                    StatusCode = 404
                };




            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                return new ServiceResult<bool>
                {
                    Success = true,
                    Message = "Email confirmed successfully! You can now log in.",
                    StatusCode = 200,
                    Data = true
                };
                // OR redirect to login page:
                // return Redirect("https://yourfrontend.com/login");
            }
            else
            {
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "Email confirmation failed. Token may be invalid or expired.",
                    StatusCode = 400
                };
            }
        }

        public async Task<ServiceResult<string>> ForgetPasswordService(string email, IEmailService emailService)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !user.EmailConfirmed)
                return new ServiceResult<string>
                {
                    Success = false,
                    StatusCode = 200,
                    Message = "",
                };

            var PasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var EncodePasswordToken = WebUtility.UrlEncode(PasswordToken);
            var Request = _httpContextAccessor.HttpContext.Request;
            var PasswordTokenLink = $"{Request.Scheme}://{Request.Host}/MySYS/Auth/reset_password?userId={user.Id}&token={EncodePasswordToken}";
            await emailService.SendEmailAsync(
             email,
             "Reset Password",
             $"Click here to reset your password: {PasswordTokenLink}"
            );

            return new ServiceResult<string>
            {
                Success = true,
                Message = $"password rest userid = {user.Id} , toekn= {EncodePasswordToken}",
                StatusCode = 200,
                Data = EncodePasswordToken
            };


        }

        public async Task<ServiceResult<bool>> RestPasswordService(RestlocaLPasswordDTO request_password)
        {
            var user = await _userManager.FindByIdAsync(request_password.user_id);
            if (user == null)
            {
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "no user found ",
                    StatusCode = 404
                };

            }
            var result = await _userManager.ResetPasswordAsync(
               user,
               request_password.Token,
               request_password.New_Password
           );

            if (!result.Succeeded)
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "rest password filed try again layter",
                    StatusCode = 400
                };

            return new ServiceResult<bool>
            {
                Success = true,
                Message = "Password reset successful.",
                StatusCode = 200,
                Data = false
            };
        }
    }


}