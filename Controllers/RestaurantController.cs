using System;
using System.Security.Claims;
using food_order_system1.DTOs;
using food_order_system1.Flters;
using food_order_system1.Modles;
using food_order_system1.Service;
using food_order_system1.Service.RestaurantService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static food_order_system1.Controllers.OrderController;

namespace food_order_system1.Controllers
{
    /// <summary>
    /// Handles restaurant management operations such as:
    /// creating, updating, deleting restaurants,
    /// managing restaurant status, and admin approvals.
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ServiceResult<string>), 401)]
    [ApiController]
    [Route("api/[controller]")]
    public class RestaurantController : ControllerBase
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IRestauantService _restauantService;
        private readonly ILogger<RestaurantController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public RestaurantController(IRestauantService restauantService,
        UserManager<ApplicationUser> userManager,
            IAuthorizationService authorizationService,
            ILogger<RestaurantController> logger
            )
        {
            _authorizationService = authorizationService;
            _restauantService = restauantService;
            _logger = logger;
            _userManager = userManager;

        }

        /// <summary>
        /// Create a new restaurant for the logged-in manager.
        /// </summary>
        [Authorize(Roles = "RestaurantManager,Admin")]
        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult<int>), 201)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        public async Task<IActionResult> CreateRestaurant([FromBody] CreateRestaurantDTO request_restaurant)
        {

            string user_id = await GetUserIdFromToken();

            var role = await GetUserRole();

            if (role == UserRolee.RestaurantManager)
            {
                request_restaurant.ManagerId = user_id;
            }
            else if (role == UserRolee.Admin && String.IsNullOrEmpty(request_restaurant.ManagerId))
            {
                _logger.LogWarning(
                    "Admin {UserId} attempted to create restaurant without selecting manager",
                    user_id);
                return MapServiceResult(
               new ServiceResult<int>
               {
                   Success = false,
                   Message = "Admin can not create restaurant with out select restaurant manager",
                   StatusCode = 400
               });

            }

            var result = await _restauantService.CreateRestaurantService(request_restaurant);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Update restaurant information.
        /// </summary>
        [Authorize(Roles = "RestaurantManager,Admin")]
        [HttpPatch("{res_id}")]
        [ProducesResponseType(typeof(ServiceResult<bool>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        public async Task<IActionResult> UpdateRestaurant(int res_id, [FromBody] UpdateRestaurantDTO dto)
        {
            if (res_id <= 0)
            {
                _logger.LogWarning("Update restaurant requested without restaurant id");
                return MapServiceResult(new ServiceResult<string>
                {
                    Success = false,
                    Message = "restaurant id is missing",
                    StatusCode = 400
                });
            }
            if (string.IsNullOrEmpty(dto.Restaurant_Name) && string.IsNullOrEmpty(dto.Address) && string.IsNullOrEmpty(dto.Description))
            {
                _logger.LogWarning("Update restaurant requested without any data to update");
                return MapServiceResult(new ServiceResult<string>
                {
                    Success = false,
                    Message = "nothing to update",
                    StatusCode = 400
                });
            }
            var (Restaurant, Auth) = await _restauantService.GetRestaurantEntityAndAuth(res_id);

            if (Restaurant == null || Auth == null)
            {
                _logger.LogWarning("Restaurant not found. RestaurantId: {RestaurantId}", res_id);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "Restaurant not found",
                    StatusCode = 404
                });
            }

            var authResult = await RestaurantAuthorizeOrFalid(Auth, "you are not authorized to update restaurant");

            if (authResult != null) return authResult;

            var result = await _restauantService.UpdateRestaurantService(dto, Restaurant);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Toggle restaurant open/close status.
        /// </summary>
        [Authorize(Roles = "RestaurantManager,Admin")]
        [HttpPatch("{res_id}/status")]
        [ProducesResponseType(typeof(ServiceResult<string>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        public async Task<IActionResult> OpenCloseRestaurant(int res_id)
        {
            if (res_id <= 0)
            {
                _logger.LogWarning("Open/close restaurant requested without restaurant id");
                return MapServiceResult(new ServiceResult<string>
                {
                    Success = false,
                    Message = "restaurant id is missing",
                    StatusCode = 400
                });
            }
            var (Restaurant, Auth) = await _restauantService.GetRestaurantEntityAndAuth(res_id);

            if (Restaurant == null || Auth == null)
            {
                _logger.LogWarning("Restaurant not found. RestaurantId: {RestaurantId}", res_id);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "Restaurant not found",
                    StatusCode = 404
                });
            }

            var authResult = await RestaurantAuthorizeOrFalid(Auth, "You are not authorized to modify open or close for this restaurant");

            if (authResult != null) return authResult;

            var result = await _restauantService.OpenCloseRestaurantService(Restaurant);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Delete a restaurant (soft or hard delete depending on service logic).
        /// </summary>
        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("{res_id}/bloack")]
        [ProducesResponseType(typeof(ServiceResult<bool>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        public async Task<IActionResult> DeleteRestaurant(int res_id)
        {
            if (res_id <= 0)
            {
                _logger.LogWarning("delete restaurant requested with out restaurant id");
                return MapServiceResult(new ServiceResult<string>
                {
                    Success = false,
                    Message = "restaurant id is missing",
                    StatusCode = 400
                });
            }
            var (Restaurant, Auth) = await _restauantService.GetRestaurantEntityAndAuth(res_id);

            if (Restaurant == null || Auth == null)
            {
                _logger.LogInformation("restaurant not found. restaurantId: {res_id}", res_id);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "Restaurant not found",
                    StatusCode = 404
                });
            }

            var authResult = await RestaurantAuthorizeOrFalid(Auth, "You are not authorized to delete this restaurant");

            if (authResult != null) return authResult;

            var result = await _restauantService.DeleteRestaurantService(Restaurant);
            return MapServiceResult(result);
        }


        // get all restaurant with items 
        /// <summary>
        /// get all restaurant with menu items with filter by restaurant name , id and adress ,  and sort by restaurant id name or adress  pagination.
        /// </summary>
        /// <param name="p">Pagination parameters</param>
        /// <param name="filter">Filtering and sorting parameters</param>
        /// <returns>List of viewRestauantAndMenuDTO </returns>
        [Authorize(Roles = "Admin,RestaurantManager,Customer")]
        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<viewRestaurantAndMenuDTO>>), 200)]
        public async Task<IActionResult> GetRestauantMenuAndItmes([FromQuery] PaginationParams p,[FromQuery] RestaurantFilter filter)
        {
            var userId = await GetUserIdFromToken();
            UserRolee role = await GetUserRole();
            var result = await _restauantService.GetRestauantMenuAndItmesService(p, filter, userId, role);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Customer requests to become a restaurant manager.
        /// </summary>
        [Authorize(Roles = "Customer")]
        [HttpPost("manager-request")]
        [ProducesResponseType(typeof(ServiceResult<string>), 200)]
        public async Task<IActionResult> CreateRestaurantManager()
        {
            string user_id = await GetUserIdFromToken();

            var result = await _restauantService.CreateRequestResturantManagerServise(user_id);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Admin accepts restaurant manager request.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPatch("manager-request/{userId}/approval")]
        [ProducesResponseType(typeof(ServiceResult<string>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 500)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        public async Task<IActionResult> AcceptRequest(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("accept restaurant manager request called with out userId ");
                return MapServiceResult(new ServiceResult<string>
                {
                    Success = false,
                    Message = "user id is missing",
                    StatusCode = 400
                });
            }

            var result = await _restauantService.AcceptRequestToManagerService(userId);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Admin approves or rejects a restaurant request.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPatch("{resid}/approval")]
        [ProducesResponseType(typeof(ServiceResult<string>), 200)]
        public async Task<IActionResult> ApproveRestaurant(int resid)
        {
            if (resid <= 0)
            {
                _logger.LogWarning("Approve restaurant requested with out restaurant id ");
                return MapServiceResult(new ServiceResult<string>
                {
                    Success = false,
                    Message = "restaurant id is missing",
                    StatusCode = 400
                });
            }
            var (Restaurant, _) = await _restauantService.GetRestaurantEntityAndAuth(resid);
            if (Restaurant == null)
            {
                _logger.LogInformation("restaurant not found . RestaurantId {res_id}", resid);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "Restaurant not found",
                    StatusCode = 404
                });
            }

            var result = await _restauantService.AproveRegectRestaurantService(Restaurant);
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

        private async Task<IActionResult?> RestaurantAuthorizeOrFalid(RestaurantAuthorizationDTO resource, string message)
        {

            var authResult = await _authorizationService.AuthorizeAsync(
            User,
            resource,
            "RestaurantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
            {
                var userId = await GetUserIdFromToken();

                _logger.LogWarning(
                    "User {UserId} is not authorized for restaurant {RestaurantId}",
                    userId,
                    resource.RestaurantId);
                return MapServiceResult(new ServiceResult<bool>
                {
                    Success = false,
                    Message = message,
                    StatusCode = 403
                });
            }
            return null;
        }

        private async Task<UserRolee> GetUserRole()
        {
            UserRolee role;
            if (User.IsInRole(UserRolee.Customer.ToString()))
            {
                role = UserRolee.Customer;
            }
            else if (User.IsInRole(UserRolee.RestaurantManager.ToString()))
                role = UserRolee.RestaurantManager;
            else
                role = UserRolee.Admin;

            return role;

        }


    }

}