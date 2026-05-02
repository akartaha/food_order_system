using System.Security.Claims;
using food_order_system1.DTOs;
using food_order_system1.Flters;
using food_order_system1.Modles;
using food_order_system1.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace food_order_system1.Controllers
{
    /// <summary>
    /// Manages user shopping cart operations such as:
    /// creating carts, adding/updating/removing items, and viewing cart contents.
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ServiceResult<string>), 401)]
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<CartController> _logger;

        public CartController(ICartService cartService, IAuthorizationService authorizationService, ILogger<CartController> logger)
        {
            _cartService = cartService;
            _authorizationService = authorizationService;
            _logger = logger;
        }

        /// <summary>
        /// Create a new cart for the authenticated customer.
        /// </summary>
        /// <param name="restaurantId">Target restaurant ID</param>
        /// <param name="request_cart">Cart creation data</param>
        /// <returns>Created cart</returns>
        [Authorize(Roles = "Customer")]
        [HttpPost("{restaurantId}")]
        [ProducesResponseType(typeof(ServiceResult<Cart>), 201)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]

        public async Task<IActionResult> CreateCart(int restaurantId, [FromBody] CreateCartDTO request_cart)
        {
            var user_id = await GetUserIdFromToken();


            var result = await _cartService.CreateCartService(user_id, restaurantId, request_cart);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Add a new item to a specific cart.
        /// </summary>
        /// <param name="cart_id">Cart ID</param>
        /// <param name="request_item">Item details (itemId, quantity, etc.)</param>
        /// <returns>Updated cart item</returns>
        [Authorize(Roles = "Customer")]
        [HttpPost("{cart_id}/items")]
        [ProducesResponseType(typeof(ServiceResult<object>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]

      public async Task<IActionResult> AddItemToCart(int cart_id, [FromBody] AddItemToCartDTO request_item)
{
    if (cart_id <= 0)
    {
        _logger.LogWarning("Invalid cart_id {CartId} provided", cart_id);
        return MapServiceResult(new ServiceResult<int>
        {
            Success = false,
            Message = "cart id must be greater than zero",
            StatusCode = 400
        });
    }

    var (Cart, Auth) = await _cartService.GetCartEntityAndAuth(cart_id);

    if (Cart == null || Auth == null)
    {
        _logger.LogWarning("Cart not found for cart_id {CartId}", cart_id);
        return MapServiceResult(new ServiceResult<int>
        {
            Success = false,
            Message = "Cart not found",
            StatusCode = 404
        });
    }

    var authResult = await AuthorizedOrFiled(Auth, "You are not authorized to modify this cart.");

    if (authResult != null)
    {
        _logger.LogWarning("Unauthorized access attempt on cart {CartId}", cart_id);
        return authResult;
    }

    var result = await _cartService.AddItemToCartService(Cart, request_item);
    return MapServiceResult(result);
}

        /// <summary>
        /// Update the quantity of a cart item.
        /// </summary>
        /// <param name="cartItemId">Cart item ID</param>
        /// <param name="dto">Updated quantity data</param>
        /// <returns>Updated cart item</returns>
        [Authorize(Roles = "Customer")]
        [HttpPatch("carts/items/{cartItemId}")]
        [ProducesResponseType(typeof(ServiceResult<object>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]

        [ProducesResponseType(typeof(ServiceResult<string>), 404)]

        public async Task<IActionResult> UpdateItemCart(int cartItemId, [FromBody] UpdateCartItem dto)
        {
              if (cartItemId <= 0)
            {
                _logger.LogWarning("update item requested with out restaurant id ");
                return MapServiceResult(new ServiceResult<string>
                {
                    Success = false,
                    Message = "item id is missing",
                    StatusCode = 400
                });
            }

            var (CartItem ,Auth) = await _cartService.GetCartItemEntityAndAuth(cartItemId);
            
            if (CartItem == null || Auth == null)
            {
                _logger.LogInformation("item with {item_id} not found", cartItemId);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "Cart item not found",
                    StatusCode = 404
                });
            }

            var authResult = await AuthorizedOrFiled(Auth , "You are not authorized to modify this cart item.");

            if (authResult != null)
            {
                _logger.LogWarning("user not authorized to update item with {item_id}", cartItemId);
                return authResult;
            }

      

            var result = await _cartService.UpdateItemCartService(CartItem, Auth.ownerId, dto);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Delete an item from a cart.
        /// </summary>
        /// <param name="cartItemId">Cart item ID</param>
        /// <returns>Deletion result</returns>
        [Authorize(Roles = "Customer")]
        [HttpDelete("carts/items/{cartItemId}")]
        [ProducesResponseType(typeof(ServiceResult<object>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]

        public async Task<IActionResult> DeleteItemCart(int cartItemId)
        {
  if (cartItemId <= 0)
            {
                _logger.LogWarning("delete item requested with out restaurant id ");
                return MapServiceResult(new ServiceResult<string>
                {
                    Success = false,
                    Message = "item id is missing",
                    StatusCode = 400
                });
            }

            var (CartItem ,Auth) = await _cartService.GetCartItemEntityAndAuth(cartItemId);
            
            if (CartItem == null || Auth == null)
            {
                _logger.LogInformation("item with {item_id} not found", cartItemId);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "Cart item not found",
                    StatusCode = 404
                });
            }

            var authResult = await AuthorizedOrFiled(Auth,"You are not authorized to delete this cart item.");

            if (authResult != null)
            {
                _logger.LogWarning("user not allowed to delete item with {item_id} ", cartItemId);
                return authResult;
            }
         

            var result = await _cartService.DeleteItemCartService(CartItem, Auth.ownerId);
            return MapServiceResult(result);
        }


        /// <summary>
        /// Retrieve all carts and items with filter by cart id, cart name and restaurant id (only Customer sees own)
        /// sort the result with acending or decending order by cart id cart name restaurant id 
        /// </summary>
        /// <param name="pagination">Pagination parameters</param>
        /// <param name="filter">Filtering and sorting parameters</param>
        /// <returns>List of carts with items</returns>
        [Authorize(Roles = "Customer")]
        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetCartItemDTO>>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        public async Task<IActionResult> ViewAllCartItems([FromQuery] PaginationParams pagination, [FromQuery] CartFilter filter)
        {
            string user_id = await GetUserIdFromToken();

            var result = await _cartService.ViewAllCartItemsService(pagination, filter, user_id);
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
                500 => StatusCode(500 , result),
                201 => Created("", result),
                _ => Ok(result)
            };
        }
        private async Task<IActionResult?> AuthorizedOrFiled(CartAuthorizationDTO resource , string message)
        {

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              resource,
              "CartOwnerShipPolicy");

            if (!authResult.Succeeded)
            {
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = message,
                    StatusCode = 403
                });

                
            }
            return null;

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