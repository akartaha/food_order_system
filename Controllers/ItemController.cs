using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Exceptions;
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
    public class ItemController : ControllerBase
    {
        private readonly AppUser _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAuthorizationService _authorizationService;

        private readonly ItemService _itemservice;

        public ItemController(
            AppUser dbContext,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IAuthorizationService authorizationService,
            ItemService itemservice)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _signInManager = signInManager;
            _authorizationService = authorizationService;
            _itemservice = itemservice;
        }



        [Authorize(Roles = "RestaurantManager")]
        [HttpPost("create_item/menu_category/{MenuCategoryId}")]
        public async Task<IActionResult> create_item(int MenuCategoryId, [FromBody] CreateItemDTO request_item)
        {

            var result = await _itemservice.CreateItemService(MenuCategoryId, request_item, User);

            return MapServiceResult(result);



        }


        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("update_item/{item_id}")]
        public async Task<IActionResult> update_item(int item_id, [FromBody] UpdateItemDTO dto)
        {
            var result = await _itemservice.UpdateItemService(item_id, dto, User);
            return MapServiceResult(result);



        }



        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("activate_deactivate_item/{item_id}")]
        public async Task<IActionResult> ActivateDeactivateItem(int item_id)
        {
            var result = await _itemservice.ActivateDeactivateItemService(item_id, User);
            return MapServiceResult(result);

        }


        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("delete_item/{item_id}")]
        public async Task<IActionResult> delete_item(int item_id)
        {
            var result = await _itemservice.DeleteItemService(item_id, User);
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