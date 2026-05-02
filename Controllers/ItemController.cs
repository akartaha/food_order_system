using System;
using System.Security.Claims;
using food_order_system1.DTOs;
using food_order_system1.Flters;
using food_order_system1.Modles;
using food_order_system1.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using static food_order_system1.Controllers.OrderController;


namespace food_order_system1.Controllers
{
    [Authorize]
    [ProducesResponseType(typeof(ServiceResult<string>), 401)]
    [ApiController]
    [Route("api/[controller]")]
    public class ItemController : ControllerBase
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IItemService _itemservice;
        private readonly ILogger<ItemController> _logger;

        public ItemController(IItemService itemservice, IAuthorizationService authorizationService, ILogger<ItemController> logger)
        {
            _authorizationService = authorizationService;
            _itemservice = itemservice;
            _logger = logger;
        }

        [Authorize(Roles = "RestaurantManager")]
        [HttpPost("menu-categoris/{MenuCategoryId}")]
        public async Task<IActionResult> CreateItem(int MenuCategoryId, [FromBody] CreateItemDTO request_item)
        {
            _logger.LogInformation("Create item request for {ItemName} in menu {MenuCategoryId}", request_item.ItemName, MenuCategoryId);

            if (MenuCategoryId <= 0)
            {
                _logger.LogWarning("Invalid menu id {MenuCategoryId}", MenuCategoryId);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "menu id must be greater than zero",
                    StatusCode = 400,
                });
            }

            var (Menu, Auth) = await _itemservice.GetMenuEntityAndAuth(MenuCategoryId);

            if (Menu == null || Auth == null)
            {
                _logger.LogWarning("Menu not found {MenuCategoryId}", MenuCategoryId);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "menu not found",
                    StatusCode = 404,
                });
            }

            var authResult = await AuthorizedOrFalid(Auth, "You are not authorized");

            if (authResult != null)
            {
                _logger.LogWarning("Unauthorized create item for menu {MenuCategoryId}", MenuCategoryId);
                return authResult;
            }

            var result = await _itemservice.CreateItemService(MenuCategoryId, request_item);
            return MapServiceResult(result);
        }

        [HttpPatch("{item_id}")]
        public async Task<IActionResult> UpdateItem(int item_id, [FromBody] UpdateItemDTO dto)
        {
            if (item_id <= 0)
            {
                _logger.LogWarning("Invalid item id {ItemId}", item_id);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "item id must be greater than zero",
                    StatusCode = 400,
                });
            }

            var (Item, Auth) = await _itemservice.GetItemEntityAndAuth(item_id);

            if (Item == null || Auth == null)
            {
                _logger.LogWarning("Item not found {ItemId}", item_id);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "item not found",
                    StatusCode = 404
                });
            }

            var authResult = await AuthorizedOrFalid(Auth, "Unauthorized");

            if (authResult != null)
            {
                _logger.LogWarning("Unauthorized update for item {ItemId}", item_id);
                return authResult;
            }

            var result = await _itemservice.UpdateItemService(Item, dto);
            return MapServiceResult(result);
        }

        [HttpPatch("/{item_id}/status")]
        public async Task<IActionResult> ActivateDeactivateItem(int item_id)
        {
            if (item_id <= 0)
            {
                _logger.LogWarning("Invalid item id {ItemId}", item_id);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "invalid id",
                    StatusCode = 400,
                });
            }

            var (Item, Auth) = await _itemservice.GetItemEntityAndAuth(item_id);

            if (Item == null || Auth == null)
            {
                _logger.LogWarning("Item not found {ItemId}", item_id);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "item not found",
                    StatusCode = 404
                });
            }

            var authResult = await AuthorizedOrFalid(Auth, "Unauthorized");
            if (authResult != null)
            {
                _logger.LogWarning("Unauthorized activate/deactivate {ItemId}", item_id);
                return authResult;
            }

            var result = await _itemservice.ActivateDeactivateItemService(Item);
            return MapServiceResult(result);
        }

        [HttpPatch("{item_id}/bloack")]
        public async Task<IActionResult> DeleteItem(int item_id)
        {
            if (item_id <= 0)
            {
                _logger.LogWarning("Invalid item id {ItemId}", item_id);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "invalid id",
                    StatusCode = 400,
                });
            }

            var (Item, Auth) = await _itemservice.GetItemEntityAndAuth(item_id);

            if (Item == null || Auth == null)
            {
                _logger.LogWarning("Item not found {ItemId}", item_id);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "item not found",
                    StatusCode = 404
                });
            }

            var authResult = await AuthorizedOrFalid(Auth, "Unauthorized");
            if (authResult != null)
            {
                _logger.LogWarning("Unauthorized delete item {ItemId}", item_id);
                return authResult;
            }

            var result = await _itemservice.DeleteItemService(Item);
            return MapServiceResult(result);
        }

        [HttpGet]
        [Authorize(Roles = "Customer,Admin,RestaurantManager")]
        public async Task<IActionResult> GetAllItems([FromQuery] PaginationParams pagination, [FromQuery] ItemFilter filter)
        {
            var user_id = await GetUserIdFromToken();
            var role = await GetUserRole();

            _logger.LogInformation("Get items request by user {UserId} role {Role}", user_id, role);

            var items = await _itemservice.GetAllItemsService(pagination, filter, user_id, role);
            return MapServiceResult(items);
        }

        private IActionResult MapServiceResult<T>(ServiceResult<T> result)
        {
          
            return result.StatusCode switch
            {
                404 => NotFound(result),
                400 => BadRequest(result),
                403 => StatusCode(result.StatusCode, result),
                420 =>BadRequest(result),
                201 => Created("", result),
                _ => Ok(result)
            };
        }

        private async Task<IActionResult?> AuthorizedOrFalid(RestaurantAuthorizationDTO resource, string message)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, resource, "RestaurantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
            {
                _logger.LogWarning("Authorization failed");
                return MapServiceResult(new ServiceResult<bool>
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
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogError("User ID missing in token");
                throw new UnauthorizedAccessException();
            }
            return id;
        }

        private async Task<UserRolee> GetUserRole()
        {
            if (User.IsInRole(UserRolee.Customer.ToString()))
                return UserRolee.Customer;

            if (User.IsInRole(UserRolee.RestaurantManager.ToString()))
                return UserRolee.RestaurantManager;

            return UserRolee.Admin;
        }
    }
}
