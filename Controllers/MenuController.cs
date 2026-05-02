
using System.Security.Claims;
using food_order_system1.DTOs;
using food_order_system1.Flters;
using food_order_system1.Modles;
using food_order_system1.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static food_order_system1.Controllers.OrderController;


namespace food_order_system1.Controllers
{
    /// <summary>
    /// Manages menu categories for restaurants.
    /// Allows restaurant managers to create, update, and delete menus.
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ServiceResult<string>), 401)]
    [ApiController]
    [Route("api/[controller]")]
    public class MenuController : ControllerBase
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IMenuService _menuService;
        private readonly ILogger<MenuController> _logger;

        public MenuController(IMenuService menuService, IAuthorizationService authorizationService, ILogger<MenuController> logger)
        {
            _authorizationService = authorizationService;
            _menuService = menuService;
            _logger = logger;
        }

        /// <summary>
        /// Create a new menu category for the authenticated restaurant manager.
        /// </summary>
        /// <param name="request_menu">Menu data</param>
        /// <returns>Created menu ID</returns>
        [Authorize(Roles = "RestaurantManager")]
        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult<int>), 201)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        public async Task<IActionResult> CreaetMenu([FromBody] CreateMenuDTO request_menu)
        {
            string user_id = await GetUserIdFromToken();
         

            var result = await _menuService.CreaetMenuService( request_menu , user_id );
            return MapServiceResult(result);
        }

        /// <summary>
        /// Update an existing menu category.
        /// </summary>
        /// <param name="menu_id">Menu ID</param>
        /// <param name="dto">Updated menu data</param>
        /// <returns>Update result</returns>
        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("{menu_id}")]
        [ProducesResponseType(typeof(ServiceResult<bool>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        public async Task<IActionResult> UpdateMenu(int menu_id, [FromBody] UpdateMenuCategoryDTO dto)
        {

            if (menu_id <= 0)
            {
             _logger.LogWarning("Update menu requested with invalid menu id {MenuId}", menu_id);
                return MapServiceResult(new ServiceResult<bool>
                {
                    Success = false,
                    Message = "menu id musT be grate than zero",
                    StatusCode = 400,
                    Data = false
                });
            }
            var (Menu, Auth) = await _menuService.GetMenuEntityAndAuth(menu_id);

            if (Menu == null || Auth ==null)
            {
               _logger.LogWarning("Menu category not found for id {MenuId}", menu_id);
                return MapServiceResult(new ServiceResult<bool>
                {
                    Success = false,
                    Message = "Menu category not found",
                    StatusCode = 404,
                    Data = false
                });
            }

            var authResult = await AuthorizedOrFalid(Auth , "You are not authorized to update this menu category");

            if (authResult != null)
            {
_logger.LogWarning("User not authorized to update menu {CategoryName}", Menu.CategoryName);
                return authResult;
            }
            var result = await _menuService.UpdateMenuService(Menu, dto);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Soft delete a menu category.
        /// </summary>
        /// <param name="menu_id">Menu ID</param>
        /// <returns>Deletion result</returns>
        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("{menu_id}/status")]
        [ProducesResponseType(typeof(ServiceResult<bool>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        public async Task<IActionResult> DeleteMenu(int menu_id)
        {
              if (menu_id <= 0)
            {
_logger.LogWarning("Delete menu requested with invalid menu id {MenuId}", menu_id);                return MapServiceResult(new ServiceResult<bool>
                {
                    Success = false,
                    Message = "menu id musT be grate than zero",
                    StatusCode = 400,
                    Data = false
                });
            }
            var (Menu, Auth) = await _menuService.GetMenuEntityAndAuth(menu_id);

            if (Menu == null || Auth == null)
            {
                _logger.LogInformation("Menu not found {menuId}", menu_id);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "Menu not found",
                    StatusCode = 404
                });
            }
          
            var authResult = await AuthorizedOrFalid(Auth , "You are not authorized to delete this menu category");

            if (authResult != null)
            {
_logger.LogWarning("User not authorized to delete menu {CategoryName}", Menu.CategoryName);
                return authResult;
            }
            var result = await _menuService.DeleteMenuService(Menu);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Retrieve all menu and items with filter by menu id, menu name and restaurant id 
        /// sort the result with acending or decending order by menu id ,meny name and restaurant id 
        /// </summary>
        /// <param name="pagination">Pagination parameters</param>
        /// <param name="filter">Filtering and sorting parameters</param>
        /// <returns>List of menus with items</returns>
        [Authorize(Roles = "Customer,Admin,RestaurantManager")]
        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetCartItemDTO>>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        public async Task<IActionResult> ViewAllCartItems([FromQuery] PaginationParams pagination, [FromQuery] MenuFilter filter)
        {
            var user_id = await GetUserIdFromToken();

            UserRolee role= await GetUserRole();

            var result = await _menuService.GetAllMenusService(pagination, filter, user_id, role);


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
                201 => Created("", result),
                _ => Ok(result)
            };
        }

        private async Task<IActionResult?> AuthorizedOrFalid(RestaurantAuthorizationDTO resource ,string message)
        {

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              resource,
              "RestauantOwnerShipAndAdminPolicy");

              if (!authResult.Succeeded)
            {
               return  MapServiceResult(new ServiceResult<bool>
                {
                    Success = false,
                    Message = message,
                    Data=false,
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
          private async Task<UserRolee> GetUserRole()
        { 
             UserRolee role;

         if (User.IsInRole(UserRolee.Customer.ToString()))
            {
                role = UserRolee.Customer;
            }
            else if (User.IsInRole(UserRolee.RestaurantManager.ToString()))
            {
                role = UserRolee.RestaurantManager;
            }
         else
           {
           role = UserRolee.Admin;
           }

        return role;
            
        }
    }
}