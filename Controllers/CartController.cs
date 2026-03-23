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
    public class CartController : ControllerBase
    {
        private readonly AppUser _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICartService _cartService;

        public CartController(
            AppUser dbContext,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IAuthorizationService authorizationService,
           ICartService cartService)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _signInManager = signInManager;
            _authorizationService = authorizationService;

            _cartService = cartService;

        }


        [Authorize(Roles = "Customer")]
        [HttpPost("create_cart/{restaurantId}")]
        public async Task<IActionResult> CreateCart(int restaurantId, [FromBody] CreateCartDTO request_cart)
        {
            var userId = await GetUserIdFromToken();

            var result = await _cartService.CreateCartService(userId, restaurantId, request_cart);

            return MapServiceResult(result);

        }

        [Authorize(Roles = "Customer")]
        [HttpPost("add_item_to_cart/{cart_id}")]
        public async Task<IActionResult> add_item_to_cart(int cart_id, [FromBody] AddItemToCartDTO request_item)
        {

            var result = await _cartService.AddItemToCartService(cart_id, request_item, User);
            return MapServiceResult(result);

        }



        [Authorize(Roles = "Customer")]
        [HttpPatch("update_cart_item/{cartItemId}")]
        public async Task<IActionResult> update_item_cart(int cartItemId, [FromBody] UpdateCartItem dto)
        {
            var result = await _cartService.UpdateItemCartService(cartItemId, dto, User);

            return MapServiceResult(result);


        }



        [Authorize(Roles = "Customer")]
        [HttpDelete("delete_item_cart/{cartItemId}")]
        public async Task<IActionResult> delete_item_cart([FromRoute] int cartItemId)
        {
            var result = await _cartService.DeleteItemCartService(cartItemId, User);

            return MapServiceResult(result);
        }




        [Authorize(Roles = "Customer")]
        [HttpGet("view_my_cart_items/{cart_id}")]
        public async Task<IActionResult> view_cart_items([FromRoute] int cart_id)
        {

            var result = await _cartService.ViewCartItemsService(cart_id, User);

            return MapServiceResult(result);


        }

        [Authorize(Roles = "Customer,Admin")]
        [HttpGet("view_all_my_cart_and_items")]
        public async Task<IActionResult> view_all_cart_items()
        {
            string user_id = await GetUserIdFromToken();
            string role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var result = await _cartService.ViewAllCartItemsService(user_id, role);

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