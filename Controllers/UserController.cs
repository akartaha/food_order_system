using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Modles;
using food_order_system1.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace food_order_system1.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("MySYS/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppUser _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly IUserService _userService;

        public UserController(
            AppUser dbContext,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IAuthorizationService authorizationService,
            IUserService userService)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _signInManager = signInManager;
            _authorizationService = authorizationService;
            _userService = userService;
        }




        // update profile
        [Authorize(Roles = "Customer,Admin,RestaurantManager")]
        [HttpPatch("update_profile/{userId}")]
        public async Task<IActionResult> UpdateProfile([FromRoute] string userId, [FromBody] UpdateProfileDTO request)
        {

            var result = await _userService.UpdateProfileService(userId, request, User);
            return MapServiceResult(result);

        }

        [Authorize(Roles = "Customer,Admin,RestaurantManager")]
        [HttpPatch("change_email/{email}")]
        public async Task<IActionResult> ChangeEmail([FromRoute] string email, [FromBody] ChangeEmailDTO request, [FromServices] IEmailService emailService)
        {

            var result = await _userService.ChangeEmailService(email, request, emailService, User);

            return MapServiceResult(result);


        }

        [Authorize(Roles = "Customer,Admin,RestaurantManager")]
        [HttpGet("confirm_email_change")]
        public async Task<IActionResult> ConfirmEmailChange([FromQuery] string userId, [FromQuery] string newEmail, [FromQuery] string token)
        {
            var result = await _userService.ConfirmEmailChangeService(userId, newEmail, token);

            return MapServiceResult(result);

        }

        [Authorize(Roles = "Admin")]
        [HttpGet("view/all/users")]
        public async Task<IActionResult> GetUsersWithRoles()
        {
            var result = await _userService.GetUserWithRolesService();

            return MapServiceResult(result);
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("active/deactive/user/{userid}")]
        public async Task<IActionResult> active_deactive_User(string userid)
        {
            var result = await _userService.ActiveDeactiveUserService(userid);

            return MapServiceResult(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("view/all/active/customers")]
        public async Task<IActionResult> get_all_active_users()
        {
            var result = await _userService.GetAllActiveUsers();

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
