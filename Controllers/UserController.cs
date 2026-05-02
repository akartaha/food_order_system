using System;
using System.Security.Claims;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Flters;
using food_order_system1.Modles;
using food_order_system1.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static food_order_system1.Controllers.OrderController;

namespace food_order_system1.Controllers
{/// <summary>
 /// Handles user-related operations such as:
 /// profile updates, email changes, and admin user management.
 /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ServiceResult<string>), 401)]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {

        private readonly IAuthorizationService _authorizationService;
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger,IAuthorizationService authorizationService )
        {

            _authorizationService = authorizationService;
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// Update user profile information.
        /// User can only update their own profile.
        /// </summary>
        [Authorize(Roles = "Customer,Admin,RestaurantManager")]
        [HttpPatch("{userId}")]
        [ProducesResponseType(typeof(ServiceResult<bool>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        public async Task<IActionResult> UpdateProfile(string userId, [FromBody] UpdateProfileDTO request)
        {
            _logger.LogInformation("UpdateProfile requested for userId: {UserId}", userId);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("update user requested with out userId {userId}", userId);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "user id is missing",
                    StatusCode = 400
                }); 
            }

           
            if (string.IsNullOrEmpty(request.fullName) && string.IsNullOrEmpty(request.PhoneNumber))
            {
                _logger.LogInformation("update profile for user {userId} failed because nothing to update", userId);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "nothing to update",
                    StatusCode = 400
                });
            }
           

            var authResult = await OwnerAuthorizeOrFalid(userId, "you are not authorizde to update profile");

            if (authResult != null)
                return authResult;

            _logger.LogInformation("User {UserId} authorized to update profile", userId);


            var result = await _userService.UpdateProfileService(userId, request);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Request email change (sends confirmation link).
        /// </summary>
        /// <param name="request">change email object</param>
        /// <param name="emailService">email service ovject</param>
        /// <returns>userId</returns>
        [Authorize(Roles = "Customer,Admin,RestaurantManager")]
        [HttpPost("change-email")]
        [ProducesResponseType(typeof(ServiceResult<string>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        public async Task<IActionResult> ChangeEmail(
            [FromBody] ChangeEmailDTO request,
            [FromServices] IEmailService emailService)
        {


            var user_id = await GetUserIdFromToken();
            _logger.LogInformation("User {UserId} requested email change", user_id);

            var result = await _userService.ChangeEmailService(request, emailService, user_id);
            _logger.LogInformation("email change request peocessed for user {userId}", user_id);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Confirm email change using token sent via email.
        /// </summary>
        /// <param name="request">change eimali datas</param>
        /// <returns>userId</returns>
        [AllowAnonymous]
        [HttpGet("confirm-email-change")]
        [ProducesResponseType(typeof(ServiceResult<string>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        public async Task<IActionResult> ConfirmEmailChange(
            [FromQuery] ConfirmChangeEmailDTO request)
        {
_logger.LogInformation("ConfirmEmailChange requested for user {UserId}", request.userId);
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.newEmail) || string.IsNullOrWhiteSpace(request.token) || string.IsNullOrWhiteSpace(request.userId))
            {
                _logger.LogWarning("cange email informations is missing");
                return MapServiceResult(new ServiceResult<bool>
                {
                    Success = false,
                    Message = "please enter all information for confirm eimail ",
                    StatusCode = 404
                });

            }
         
            var result = await _userService.ConfirmEmailChangeService(request.userId, request.newEmail, request.token);
            return MapServiceResult(result);
        }


        /// <summary>
        /// Get all users with their roles (Admin only).
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetUserRoleDTO>>), 200)]
        [ProducesResponseType(typeof(ServiceResult<List<GetUserRoleDTO>>), 404)]
        [ProducesResponseType(typeof(ServiceResult<List<GetUserRoleDTO>>), 403)]
        public async Task<IActionResult> GetUsers([FromQuery] PaginationParams pagination, [FromQuery] UserFilter filter)
        {
           var currentUser = await GetUserIdFromToken();
       _logger.LogInformation("Admin {UserId} requested users list with filters", currentUser);
            var result = await _userService.GetUsersService(pagination, filter,currentUser);
           
            return MapServiceResult(result);
        }

        /// <summary>
        /// Activate or deactivate a user account (Admin only).
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPatch("{userid}/deactivate")]
        [ProducesResponseType(typeof(ServiceResult<bool>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        public async Task<IActionResult> DeactiveUser([FromRoute] string userid)
        {
            if (string.IsNullOrEmpty(userid))
            {
                 _logger.LogWarning("deactive user requested without userId {userid}", userid);
                return MapServiceResult(new ServiceResult<bool>
                {
                    Success = false,
                    Message = "User id is missimg",
                    StatusCode = 400
                });  
            }
          var currentUserId=await GetUserIdFromToken();
            _logger.LogInformation("User {AdminId} requested deactivation of user {TargetUserId}", 
    currentUserId, userid);
           
            var role = await _userService.GetUserRoles(userid);
            if (role == null)
            {
_logger.LogWarning("Cannot determine role for user {TargetUserId}", userid);
                return MapServiceResult(new ServiceResult<bool>
                {
                    Success = false,
                    Message = "user role not found",
                    StatusCode = 404
                });

            }

            UserRolee userrole;
            switch (role)
            {
                case "Customer":
                    userrole = UserRolee.Customer;
                    break;
                case "RestaurantManager":
                    userrole = UserRolee.RestaurantManager;
                    break;
                default:
                    return MapServiceResult(new ServiceResult<bool>
                    {
                        Success = false,
                        Message = "you can not  Deactive Admin",
                        StatusCode = 400
                    });

            }
  
           

            _logger.LogInformation("Proceeding to deactivate user {TargetUserId}", userid);

            var result = await _userService.DeactiveUserService(userid,currentUserId, userrole);
            return MapServiceResult(result);
        }



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


        private async Task<string> GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User ID claim missing in token.");

            return userIdClaim;

        }
        private async Task<IActionResult?> OwnerAuthorizeOrFalid(string userId, string message)
        {

            var authResult = await _authorizationService.AuthorizeAsync(
            User,
            userId,
            "UserOwnerShipPolicy");

            if (!authResult.Succeeded)
            {
                _logger.LogWarning("User {userId} not authorized for this action", userId);
                return MapServiceResult(new ServiceResult<bool>
                {
                    Success = false,
                    Message = message,
                    StatusCode = 403
                });
            }
            return null;
        }

    }
}



