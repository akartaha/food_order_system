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
    /// Handles order operations such as:
    /// creating orders, viewing orders, updating status,
    /// and retrieving order statistics.
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ServiceResult<string>), 401)]
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IOrderSerivce _orderservice;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderSerivce orderservice,
           IAuthorizationService authorizationService,
           ILogger<OrderController> logger
           )
        {
            _authorizationService = authorizationService;
            _orderservice = orderservice;
            _logger = logger;
        }

        /// <summary>
        /// Create a new order from a cart.
        /// </summary>
        /// <param name="cart_id">Cart ID</param>
        /// <param name="dto">Order data</param>
        /// <returns>Created order ID</returns>
        [Authorize(Roles = "Customer")]
        [HttpPost("{cart_id}")]
        [ProducesResponseType(typeof(ServiceResult<int>), 201)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        public async Task<IActionResult> CreateOrder(int cart_id, [FromBody] CreateOrderDTO dto)
        {
            if(cart_id <= 0)
            {
                 return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "please enter a valid cart id ",
                    StatusCode = 400
                }); 
            }
            var (cart, Auth) = await _orderservice.GetCartEntityAndAuth(cart_id);

            if (cart == null || Auth == null)
            {
                _logger.LogInformation("Cart not found cartid {cart_id}", cart_id);
                return MapServiceResult(new ServiceResult<int>
                {
                    Success = false,
                    Message = "Cart not found",
                    StatusCode = 404
                });
            }
            var authResult = await OwnerAuthorizeOrFalid(Auth, "you are not authorized to create order");

            if (authResult != null)
                return authResult;

            var result = await _orderservice.CreateOderService(cart, dto);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Retrieve all order and items with filter by order status, order id,username, restaurant id and restaurant name 
        /// customer and restaurant manager only can see its own orders and admin can see specific or all restaurant orders
        /// sort the result with acending or decending order by order id ,restaurant name , user name and restaurant id 
        /// </summary>
        /// <param name="pagination">Pagination parameters</param>
        /// <param name="filter">Filtering and sorting parameters</param>
        /// <returns>List of orders with items</returns>
        [Authorize(Roles = "Customer,RestaurantManager,Admin")]
        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<ViewOrderOrderItemDTO>>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        public async Task<IActionResult> GetOrders([FromQuery] PaginationParams pagination, [FromQuery] OrderFilter filter)
        {

            var user_id = await GetUserIdFromToken();
            UserRolee role;

            if (User.IsInRole("Customer"))
                role = UserRolee.Customer;
            else if (User.IsInRole("RestaurantManager"))
                role = UserRolee.RestaurantManager;
            else
                role = UserRolee.Admin;

            var result = await _orderservice.ViewAllOrdersService(pagination, filter, user_id, role);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Change order status (e.g., Pending → Completed).
        /// </summary>
        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("{order_id}/status")]
        [ProducesResponseType(typeof(ServiceResult<OrderStatuss>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 403)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        public async Task<IActionResult> UpdateOrderStatus(int order_id)
        {
            var (Order, Auth) = await _orderservice.GetOrderEntityAndAuth(order_id);

            if (Order == null || Auth == null)
                return MapServiceResult(new ServiceResult<OrderStatuss>
                {
                    Success = false,
                    Message = "Order not found",
                    StatusCode = 404
                });


            var authResult = await RestaurantAuthorizeOrFalid(Auth, $"you are not allowed to cahnge this order status {order_id}");

            if (authResult != null)
                return authResult;

            var result = await _orderservice.ChangeOrderStatusService(Order);
            return MapServiceResult(result);
        }

        /// <summary>
        /// Retrieve order statistic information such as order number and total revent between start and end date 
        /// customer and restaurant manager only can see its own  orders and admin can se all restaurant orders
        /// </summary>
        /// <returns>return ViewOrderStatisticDTO object</returns>
        [Authorize(Roles = "Customer,RestaurantManager,Admin")]
        [HttpGet("statistic")]
        [ProducesResponseType(typeof(ServiceResult<List<ViewOrderOrderItemDTO>>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 400)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        public async Task<IActionResult> viewOrderStatistics([FromQuery] int number_days)
        {
            if (number_days <= 0)
            {
                return MapServiceResult(new ServiceResult<bool>
                {
                    Success = false,
                    Message = "number_days must be greater than 0",
                    StatusCode = 400
                });
            }
            var user_id = await GetUserIdFromToken();
            UserRolee role = await GetUserRole();

            var result = await _orderservice.GetOrderStatisticService(number_days, user_id, role);
            return MapServiceResult(result);


        }

        /// <summary>
        /// show most selling items form high to low
        ///customer  restaurant manager can see its own items that most ordered for high to low
        /// admin can see all item
        /// </summary>
        [Authorize(Roles = "Customer,RestaurantManager,Admin")]
        [HttpGet("most-selling-items")]
        [ProducesResponseType(typeof(ServiceResult<OrderStatuss>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        public async Task<IActionResult> GetMostSellingItem()
        {
            var user_id = await GetUserIdFromToken();
            UserRolee role = await GetUserRole();

            var result = await _orderservice.GetMostSelinItemService(user_id, role);
            return MapServiceResult(result);
        }

          /// <summary>
        /// show order numbers per day  
        ///  restaurant manager can see its own restaurant orders per day
        /// admin can see all restaurant orders
        /// </summary>
        [Authorize(Roles = "RestaurantManager,Admin")]
        [HttpGet("daily-count")]
        [ProducesResponseType(typeof(ServiceResult<OrderStatuss>), 200)]
        [ProducesResponseType(typeof(ServiceResult<string>), 404)]
        public async Task<IActionResult> GetOrderNumberPerDay([FromQuery]int number_days , [FromQuery] int? restaurant_id)
        {
               var user_id = await GetUserIdFromToken();
            UserRolee role = await GetUserRole();


             var result = await _orderservice.ViewOrderNumberPerDayService(number_days, restaurant_id,user_id, role);
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
        private async Task<IActionResult?> OwnerAuthorizeOrFalid(CartAuthorizationDTO resource, string message)
        {

            var authResult = await _authorizationService.AuthorizeAsync(
            User,
            resource,
            "CartOwnerShipPolicy");

            if (!authResult.Succeeded)
            {
                _logger.LogWarning("User {userId} not authorized for cart {cartId}",
                           await GetUserIdFromToken(), resource.cartId);
                return MapServiceResult(new ServiceResult<bool>
                {
                    Success = false,
                    Message = message,
                    StatusCode = 403
                });
            }
            return null;
        }

        private async Task<IActionResult?> RestaurantAuthorizeOrFalid(RestaurantAuthorizationDTO resource, string message)
        {

            var authResult = await _authorizationService.AuthorizeAsync(
            User,
            resource,
            "RestaurantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
            {
                _logger.LogWarning("User {userId} not authorized for restaurant {Restaurant_id}",
                           await GetUserIdFromToken(), resource.RestaurantId);
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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("User ID claim missing in token.");

            return userIdClaim;

        }



        public enum UserRolee
        {
            Customer,
            RestaurantManager,
            Admin
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