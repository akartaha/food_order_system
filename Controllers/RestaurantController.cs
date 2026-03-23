using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Modles;
using food_order_system1.Service;
using food_order_system1.Service.RestaurantService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace food_order_system1.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("MySYS/[controller]")]
    public class RestaurantController : ControllerBase
    {
        private readonly AppUser _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly IAuthorizationService _authorizationService;
        private readonly IRestauantService _restauantService;

        public RestaurantController(
            AppUser dbContext,
            UserManager<ApplicationUser> userManager,
            IAuthorizationService authorizationService,
            IRestauantService restauantService)
        {
            _dbContext = dbContext;
            _userManager = userManager;

            _authorizationService = authorizationService;
            _restauantService = restauantService;
        }

        [Authorize(Roles = "RestaurantManager")]
        [HttpPost("create_restaurant")]
        public async Task<IActionResult> CreateRestaurant([FromBody] CreateRestaurantDTO request_restaurant)
        {

            string user_id = await GetUserIdFromToken();

            var result = await _restauantService.CreateRestaurantService(user_id, request_restaurant);

            return MapServiceResult(result);


        }


        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("update_restaurant/{res_id}")]
        public async Task<IActionResult> update_restaurant(int res_id, [FromBody] UpdateRestaurantDTO dto)
        {
            var result = await _restauantService.UpdateRestaurantService(res_id, dto, User);

            return MapServiceResult(result);

        }


        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("open_close_restaurant/{res_id}")]
        public async Task<IActionResult> OpenCloseRestaurant(int res_id)
        {

            var result = await _restauantService.OpenCloseRestaurantService(res_id, User);

            return MapServiceResult(result);



        }


        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("delete_restaurant/{res_id}")]
        public async Task<IActionResult> delete_restaurant(int res_id)
        {
            var result = await _restauantService.DeleteRestaurantService(res_id, User);

            return MapServiceResult(result);
        }

        [Authorize(Roles = "Admin,Customer,RestaurantManager")]
        [HttpGet("view_all_restaurants")]
        public async Task<IActionResult> GetAllRestauants()
        {

            string user_id = await GetUserIdFromToken();
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

            var result = await _restauantService.GetAllRestaurantsService(user_id, role);

            return MapServiceResult(result);

        }

        [Authorize(Roles = "Admin,Customer")]
        [HttpGet("view_restaurant_by_name/{name}")]
        public async Task<IActionResult> GetRestauantByName(string name)
        {

            var result = await _restauantService.GetRestauantByNameService(name);

            return MapServiceResult(result);


        }


        [Authorize(Roles = "Admin,Customer")]
        [HttpGet("view_restaurant/{name}/menu_items")]
        public async Task<IActionResult> GetRestauantMenuAndItmes(string name)
        {
            var result = await _restauantService.GetRestauantMenuAndItmesService(name);
            return MapServiceResult(result);
        }



        [Authorize(Roles = "Customer")]
        [HttpPost("request/restaurant_manager")]
        public async Task<IActionResult> create_restaurant_manager()
        {
            string user_id = await GetUserIdFromToken();

            var result = await _restauantService.CreateResturantManagerServise(user_id);


            return MapServiceResult(result);

        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("accept/request/to_restaurant_manager/{userId}")]
        public async Task<IActionResult> AcceptRequest(string userId)
        {

            var result = await _restauantService.AcceptRequestToManagerService(userId);

            return MapServiceResult(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("Accept/regect/restaurant/{resid}")]
        public async Task<IActionResult> approve_res(int resid)
        {
            var result = await _restauantService.AproveRegectRestaurantService(resid);

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