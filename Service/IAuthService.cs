using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Linq.Expressions;
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using static food_order_system1.Controllers.OrderController;

namespace food_order_system1.Service
{
    public interface IAuthService
    {
        Task<ServiceResult<AuthResponseDTO>> LoginWithGoogleService(ExternalLoginInfo GoogleUserInfo);
        Task<ServiceResult<string>> LocalRegisterService(CreateUserDTO request_user, IEmailService emailService);
        Task<ServiceResult<AuthResponseDTO>> LocalLoginService(LoginUserDTO request_user);
        Task<ServiceResult<AuthResponseDTO>> GetNewRefreshAccessTokenService(string token);
        Task<ServiceResult<bool>> ConfirmEmailService(ConfirmEmailDTO dto);
        Task<ServiceResult<string>> ForgetPasswordService(string userName, IEmailService emailService);
        Task<ServiceResult<bool>> RestPasswordService(RestlocaLPasswordDTO request_password);
        Task<GetRefreshTokenDTO?> GetRefreshTokenFromDB(string token);

    }
    public class AuthService : IAuthService
    {
        private readonly AppUser _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuthService> _logger;


        public AuthService(AppUser context,
         UserManager<ApplicationUser> userManager,
          IConfiguration configuration,
          SignInManager<ApplicationUser> signInManager,
          IHttpContextAccessor httpContextAccessor,
          ILogger<AuthService> logger)
        {
            _dbContext = context;
            _userManager = userManager;
            _configuration = configuration;
            _signInManager = signInManager;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }
        public async Task<ServiceResult<AuthResponseDTO>> LoginWithGoogleService(ExternalLoginInfo info)
        {
            var name = info.Principal.FindFirstValue(ClaimTypes.Name);
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email))
            {
               _logger.LogWarning("Failed to retrieve user info from external provider");
                return new ServiceResult<AuthResponseDTO>
                {
                    Success = false,
                    Message = "Invalid external login data",
                    StatusCode = 400
                };
            }

            email = email.Trim().ToLower();

            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(email);

                if (user != null)
                {
                   _logger.LogWarning("Failed to link external login for user {UserId}", user.Id);

                    var logins = await _userManager.GetLoginsAsync(user);

                    if (!logins.Any(l => l.LoginProvider == info.LoginProvider))
                    {
                        var linkResult = await _userManager.AddLoginAsync(user, info);

                        if (!linkResult.Succeeded)
                        {
                            _logger.LogWarning("Failed to link external login for user {UserId}", user.Id);
                            return new ServiceResult<AuthResponseDTO>
                            {
                                Success = false,
                                Message = "Failed to link external login",
                                StatusCode = 400
                            };
                        }
                    }

                    if (!user.EmailConfirmed)
                    {
                        user.EmailConfirmed = true;
                        await _userManager.UpdateAsync(user);
                    }

                    if (!user.IsActive)
                    {
                        _logger.LogWarning("Inactive user {UserId} attempted Google login", user.Id);
                        return new ServiceResult<AuthResponseDTO>
                        {
                            Success = false,
                            Message = "Account is not active",
                            StatusCode = 403
                        };



                    }
                }
                else
                {
                    _logger.LogInformation("Creating new user via Google {Email}", email);

                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        fullName = name,
                        EmailConfirmed = true,
                        IsActive = true
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        _logger.LogError("Failed to create user via Google {Email}", email);
                        return new ServiceResult<AuthResponseDTO>
                        {
                            Success = false,
                            Message = "User creation failed",
                            StatusCode = 400
                        };

                    }

                    var roleResult = await _userManager.AddToRoleAsync(user, UserRolee.Customer.ToString());
                    if (!roleResult.Succeeded)
                    {
                        _logger.LogError("Failed to assign role to {UserId}", user.Id);
                        return new ServiceResult<AuthResponseDTO>
                        {
                            Success = false,
                            Message = "Role assignment failed",
                            StatusCode = 400
                        };

                    }

                    var loginResult = await _userManager.AddLoginAsync(user, info);
                    if (!loginResult.Succeeded)
                    {
                        _logger.LogError("Failed to add external login for {UserId}", user.Id);
                        return new ServiceResult<AuthResponseDTO>
                        {
                            Success = false,
                            Message = "External login failed",
                            StatusCode = 400
                        };

                    }
                }
            }

            var tokenResult = await GenerateToken(user);
            if (!tokenResult.Success)
            {
               _logger.LogError("Token generation failed for user {UserId}", user.Id);
                return new ServiceResult<AuthResponseDTO>
                {
                    Success = false,
                    Message = "Token generation failed",
                    StatusCode = 400
                };

            }

            var refreshToken = GenarateRefreshToken(user);
            _dbContext.refreshTokens.Add(refreshToken);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {UserId} logged in via Google", user.Id);

            return new ServiceResult<AuthResponseDTO>
            {
                Success = true,
                Data = new AuthResponseDTO
                {
                    Token = tokenResult.Data,
                    refreshToken = refreshToken.Token
                },
                StatusCode = 200
            };
        }


        private async Task<ServiceResult<string>> GenerateToken(ApplicationUser user)
        {

            if (user == null)
            {
                _logger.LogWarning("user not found can not generate token");
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "user can not login",
                    StatusCode = 400
                };
            }

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

            return new ServiceResult<string>
            {
                Success = true,
                Message = "token created",
                Data = new JwtSecurityTokenHandler().WriteToken(token),
                StatusCode = 200

            };


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


        // 
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
                _logger.LogWarning("Failed to create local user with email {Email}", request_user.Email);
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "can not create user",
                    StatusCode = 400
                };
            }

            await _userManager.AddToRoleAsync(User, UserRolee.Customer.ToString());

            var EmailConfirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(User);

            // URL encode the token because it can contain +, /, = characters
            var encoded_email_conf_token = System.Net.WebUtility.UrlEncode(EmailConfirmToken);

            var Request = _httpContextAccessor.HttpContext.Request;
            // Build the full URL manually
            var confirmationLink = $"{Request.Scheme}://{Request.Host}/MySYS/Auth/ConfirmEmail?userId={User.Id}&token={encoded_email_conf_token}";

            // Send confirmation email
            try
            {
                await emailService.SendEmailAsync(
                      User.Email,
                     "Confirm your email",
                     $"<h3>Welcome!</h3><p>Click <a href='{confirmationLink}'>here</a> to confirm your email.</p>"
                 );

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to send email confirmation to {Email}", request_user.Email);
            }


          _logger.LogInformation("User {UserId} registered successfully, confirmation email sent to {Email}", User.Id, request_user.Email);
            return new ServiceResult<string>
            {
                Success = true,
                Message = $"new user created and send confirmation eimail to {request_user.fullName}  ",
                StatusCode = 201,
                Data = User.Id
            };
        }

        public async Task<ServiceResult<AuthResponseDTO>> LocalLoginService(LoginUserDTO request_user)
        {
            var user = await _userManager.FindByEmailAsync(request_user.UserName);
            if (user == null)
            {
               _logger.LogWarning("Login failed: invalid email {Email}", request_user.UserName);
                return new ServiceResult<AuthResponseDTO>
                {
                    Success = false,
                    Message = "Invalid username or password.",
                    StatusCode = 400
                };
            }
            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("User {UserId} attempted login without email confirmation", user.Id);
                return new ServiceResult<AuthResponseDTO>
                {
                    Success = false,
                    Message = "Confirm your email",
                    StatusCode = 403
                };



            }

            if (!user.IsActive)
            {
              _logger.LogWarning("Inactive user {UserId} attempted login", user.Id);
                return new ServiceResult<AuthResponseDTO>
                {
                    Success = false,
                    Message = "Account not active , wait for admin approve",
                    StatusCode = 400
                };

            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                request_user.Password,
                isPersistent: false,
                lockoutOnFailure: true
            );

            if (result.IsLockedOut)
                return new ServiceResult<AuthResponseDTO>
                {
                    Success = false,
                    Message = "Account temporarily locked due to multiple failed attempts.",
                    StatusCode = 400
                };

            if (!result.Succeeded)
                return new ServiceResult<AuthResponseDTO>
                {
                    Success = false,
                    Message = "Invalid username or password.",
                    StatusCode = 400
                };

            var token = await GenerateToken(user);
            if (token.Success)
            {
                var refToken = GenarateRefreshToken(user);
                _dbContext.refreshTokens.Add(refToken);
                await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {UserId} logged in successfully", user.Id);
                return new ServiceResult<AuthResponseDTO>
                {
                    Success = true,
                    Data = new AuthResponseDTO
                    {
                        Token = token.Data,
                        refreshToken = refToken.Token
                    },
                    StatusCode = 200
                };

            }

            return new ServiceResult<AuthResponseDTO>
            {
                Success = false,
                Message = "user can not login ,something wrong hapneed",
                StatusCode = 400
            };
        }

        public async Task<ServiceResult<AuthResponseDTO>> GetNewRefreshAccessTokenService(string token)
        { 
            _logger.LogInformation("Refresh token attempt started");
            var FindToken = await GetRefreshTokenFromDB(token);

            if (FindToken == null)
            {
                _logger.LogWarning("Invalid or expired refresh token");
                return new ServiceResult<AuthResponseDTO>
                {
                    Success = false,
                    Message = "token is invalidor expire",
                    StatusCode = 400
                };
            }
            if (!FindToken.user.IsActive)
            {
               _logger.LogWarning("Deactivated user {UserId} attempted token refresh", FindToken.user.Id);
                return new ServiceResult<AuthResponseDTO>
                {
                    Success = false,
                    Message = "user is deactivated",
                    StatusCode = 403
                };
            }
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                FindToken.token.IsRevoked = true;
                _dbContext.refreshTokens.Update(FindToken.token);
                var AccesToken = await GenerateToken(FindToken.user);
                if (!AccesToken.Success)
                {
                    _logger.LogError("Failed to generate access token for user {UserId}", FindToken.user.Id);
                    await transaction.RollbackAsync();
                    return new ServiceResult<AuthResponseDTO>
                    {
                        Success = false,
                        Message = "user can not login",
                        StatusCode = 400
                    };

                }
                var RefreshToken = GenarateRefreshToken(FindToken.user);
                _dbContext.refreshTokens.Add(RefreshToken);
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Refresh token rotated successfully for user {UserId}", FindToken.user.Id);
                return new ServiceResult<AuthResponseDTO>
                {
                    Success = true,
                    Data = new AuthResponseDTO
                    {
                        Token = AccesToken.Data,
                        refreshToken = RefreshToken.Token
                    },
                    StatusCode = 200
                };
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, "Error while refreshing token");
                await transaction.RollbackAsync();
                throw;
            }

        }

        public async Task<ServiceResult<bool>> ConfirmEmailService(ConfirmEmailDTO dto)
        {
            var user = await _userManager.FindByIdAsync(dto.userId);

            if (user == null)
            {
                _logger.LogWarning("Email confirmation failed: user {UserId} not found", dto.userId);

                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "User not found",
                    StatusCode = 404,
                    Data = false
                };
            }

            var result = await _userManager.ConfirmEmailAsync(user, dto.token);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Invalid or expired email token for user {UserId}", user.Id);

                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "Invalid or expired token",
                    StatusCode = 400,
                    Data = false
                };
            }

            _logger.LogInformation("User {UserId} confirmed email successfully", user.Id);

            return new ServiceResult<bool>
            {
                Success = true,
                Message = "Email confirmed successfully",
                StatusCode = 200,
                Data = true
            };
        }
        public async Task<ServiceResult<string>> ForgetPasswordService(string email, IEmailService emailService)
        {
            _logger.LogInformation("Password reset requested");
            var user = await _userManager.FindByEmailAsync(email);

            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = WebUtility.UrlEncode(token);

                var Request = _httpContextAccessor.HttpContext.Request;
                var PasswordTokenLink = $"{Request.Scheme}://{Request.Host}/MySYS/Auth/reset_password?userId={user.Id}&token={encodedToken}";
                try
                {
                    await emailService.SendEmailAsync(
                     email,
                     "Reset Password",
                     $"Click here to reset your password: {PasswordTokenLink}"
                    );
                    _logger.LogInformation("Password reset email sent to {Email}", email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending reset email to {Email}", email);
                    throw;
                }

            }
            else
            {
                _logger.LogInformation("Password reset requested (email may or may not exist)");
            }

            return new ServiceResult<string>
            {
                Success = true,
                Message = "If the email exists, a reset link has been sent.",
                StatusCode = 200
            };



        }


        public async Task<ServiceResult<bool>> RestPasswordService(RestlocaLPasswordDTO request_password)
        {
            var user = await _userManager.FindByIdAsync(request_password.user_id);
            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("Reset password requested for invalid or inactive user {UserId}", request_password.user_id);
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
            {
             _logger.LogWarning("Invalid token or password reset failed for user {UserId}", user.Id);
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "rest password filed try again layter",
                    StatusCode = 400
                };
            }
           _logger.LogInformation("Password reset successfully for user {UserId}", user.Id);
            return new ServiceResult<bool>
            {
                Success = true,
                Message = "Password reset successful.",
                StatusCode = 200,
                Data = true
            };
        }

        


      

        public async Task<GetRefreshTokenDTO?> GetRefreshTokenFromDB(string token)
        {
            var FindToken = await _dbContext.refreshTokens
            .Select(t => new GetRefreshTokenDTO
            {
                token = t,
                user = t.User
            })
           .FirstOrDefaultAsync(r => r.token.Token == token && !r.token.IsRevoked && r.token.ExpiresAt > DateTime.UtcNow);

            return FindToken;
        }
    }


}