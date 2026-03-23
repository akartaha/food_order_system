using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Modles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Ocsp;

namespace food_order_system1.Service
{
    public interface IUserService
    {
        Task<ServiceResult<int>> UpdateProfileService(string userId, UpdateProfileDTO request, ClaimsPrincipal User);
        Task<ServiceResult<string>> ChangeEmailService(string email, ChangeEmailDTO request, IEmailService emailService, ClaimsPrincipal User);

        Task<ServiceResult<bool>> ConfirmEmailChangeService(string userId, string newEmail, string token);
        Task<ServiceResult<List<GetUserRoleDTO>>> GetUserWithRolesService();

        Task<ServiceResult<bool>> ActiveDeactiveUserService(string userid);
        Task<ServiceResult<List<GetUserDTO>>> GetAllActiveUsers();
    }
    public class UserService : IUserService
    {
        private readonly AppUser _dbContext;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public UserService(AppUser context,
         IAuthorizationService authorizationService,
         UserManager<ApplicationUser> userManager,
         IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = context;
            _authorizationService = authorizationService;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResult<bool>> ActiveDeactiveUserService(string userid)
        {

            var user = await _userManager.FindByIdAsync(userid);
            if (user == null) return new ServiceResult<bool>
            {
                Success = false,
                Message = "user not found",
                StatusCode = 404
            };

            user.IsActive = !user.IsActive;

            await _userManager.UpdateAsync(user);

            string result = user.IsActive ? "Actived" : "deactived";

            return new ServiceResult<bool>
            {
                Success = true,
                Message = $"{user.fullName} is {result} sucessfully",
                StatusCode = 200,
                Data = true
            };
        }

        public async Task<ServiceResult<string>> ChangeEmailService(string email, ChangeEmailDTO request, IEmailService emailService, ClaimsPrincipal User)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "User not found",
                    StatusCode = 404
                };
            }

            var authResult = await _authorizationService.AuthorizeAsync(
               User,
               user,
              "UserOwnerShipPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "you are not allowed to do this action",
                    StatusCode = 403
                };

            // confirm eimail 
            var token = await _userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);

            var encodedToken = WebUtility.UrlEncode(token);
            var Request = _httpContextAccessor.HttpContext.Request;

            var link = $"{Request.Scheme}://{Request.Host}/MySYS/Customer/confirm_email_change?userId={user.Id}&newEmail={request.NewEmail}&token={encodedToken}";

            // Send confirmation email

            await emailService.SendEmailAsync(
                request.NewEmail,
               "Confirm your email",
               $"<h3>Welcome!</h3><p>Click <a href='{link}'>here</a> to confirm your new email address.</p>"
             );

            return new ServiceResult<string>
            {
                Success = true,
                Message = "Confirmation email sent to new address",
                Data = user.Id,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<bool>> ConfirmEmailChangeService(string userId, string newEmail, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "User not found",
                    StatusCode = 404
                };
            }

            // var decodedToken = WebUtility.UrlDecode(token);

            var result = await _userManager.ChangeEmailAsync(user, newEmail, token);
            if (result.Succeeded)
            {
                user.UserName = newEmail;
                await _userManager.UpdateAsync(user);
                return new ServiceResult<bool>
                {
                    Success = true,
                    Message = "Email changed successfully",
                    StatusCode = 200,
                    Data = true
                };
            }
            else
            {
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "Failed to change email",
                    StatusCode = 400
                };
            }

        }

        public async Task<ServiceResult<List<GetUserDTO>>> GetAllActiveUsers()
        {
            var activ_users = await _userManager.Users
             .Where(u => u.IsActive)
              .Select(u => new GetUserDTO
              {
                  full_name = u.fullName,
                  user_name = u.UserName,
                  email = u.Email,
                  phone_number = u.PhoneNumber,

              }).ToListAsync();

            if (activ_users.Count() <= 0) return new ServiceResult<List<GetUserDTO>>
            {
                Success = false,
                Message = "Active user not found",
                StatusCode = 404,
                Data = null
            };

            return
                new ServiceResult<List<GetUserDTO>>
                {
                    Success = true,
                    Message = $"number of active users =  {activ_users.Count()}",
                    Data = activ_users,
                    StatusCode = 200
                };
        }

        public async Task<ServiceResult<List<GetUserRoleDTO>>> GetUserWithRolesService()
        {
            var users = _userManager.Users.ToList();

            if (!users.Any()) return new ServiceResult<List<GetUserRoleDTO>>
            {
                Success = false,
                Message = "user not found",
                StatusCode = 404
            };

            var result = new List<GetUserRoleDTO>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Count == 0) return new ServiceResult<List<GetUserRoleDTO>>
                {
                    Success = false,
                    Message = "user not found",
                    StatusCode = 404
                };

                result.Add(new GetUserRoleDTO
                {
                    UserID = user.Id,
                    UserName = user.UserName ?? "",
                    Email = user.Email ?? "",
                    IsConfirmEmail = user.EmailConfirmed ? "yse" : "no",
                    PhoneNumber = user.PhoneNumber ?? "",
                    Role = roles.ToList(),
                    IsActivied = user.IsActive ? "yes" : "no",
                });
            }

            return new ServiceResult<List<GetUserRoleDTO>>
            {
                Success = true,
                Data = result,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<int>> UpdateProfileService(string userId, UpdateProfileDTO request, ClaimsPrincipal User)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "User not found",
                    StatusCode = 404
                };
            }

            var authResult = await _authorizationService.AuthorizeAsync(
                 User,
                 user,
                "UserOwnerShipPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "You are not authorized to update this profile",
                    StatusCode = 403
                };

            if (!string.IsNullOrEmpty(request.fullName))
            {
                user.fullName = request.fullName;
            }

            if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                user.PhoneNumber = request.PhoneNumber;
            }


            if (string.IsNullOrEmpty(request.fullName) && !string.IsNullOrEmpty(request.PhoneNumber))
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "nothing to update",
                    StatusCode = 400
                };
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                return new ServiceResult<int>
                {
                    Success = true,
                    Message = "Profile updated successfully",
                    StatusCode = 200
                };
            }
            else
            {
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "Failed to update profile",
                    StatusCode = 400
                };
            }
        }


    }
}