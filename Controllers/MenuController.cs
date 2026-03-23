using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
    public class MenuController : ControllerBase
    {
        private readonly AppUser _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly IMenuService _menuService;
        public MenuController(
            AppUser dbContext,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IAuthorizationService authorizationService,
            IMenuService menuService)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _signInManager = signInManager;
            _authorizationService = authorizationService;
            _menuService = menuService;
        }



        [Authorize(Roles = "RestaurantManager")]
        [HttpPost("create_menu")]
        public async Task<IActionResult> CreaetMenu([FromBody] CreateMenuDTO request_menu)
        {
            string user_id = await GetUserIdFromToken();
            var result = await _menuService.CreaetMenuService(user_id, request_menu);
            return MapServiceResult(result);


        }


        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("update_menu/{menu_id}")]
        public async Task<IActionResult> update_menu(int menu_id, [FromBody] UpdateMenuCategoryDTO dto)
        {
            var result = await _menuService.UpdateMenuService(menu_id, dto, User);
            return MapServiceResult(result);

        }


        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("delete_menu/{menu_id}")]
        public async Task<IActionResult> delete_menu(int menu_id)
        {
            var result = await _menuService.DeleteMenuService(menu_id, User);
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

        private async Task<string> GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User ID claim missing in token.");

            return userIdClaim;

        }
    }
}