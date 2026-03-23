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
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace food_order_system1.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("MySYS/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly AppUser _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly IOrderSerivce _orderservice;

        public OrderController(
            AppUser dbContext,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IAuthorizationService authorizationService,
            IOrderSerivce orderservice)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _signInManager = signInManager;
            _authorizationService = authorizationService;
            _orderservice = orderservice;
        }




        [Authorize(Roles = "Customer")]
        [HttpPost("create_order/{cart_id}")]
        public async Task<IActionResult> create_oder(int cart_id, [FromBody] CreateOrderDTO dto)
        {

            var result = await _orderservice.CreateOderService(cart_id, dto, User);

            return MapServiceResult(result);

            // var cart = await _dbContext.carts
            //     .Include(c => c.User)
            //    .FirstOrDefaultAsync(i => i.CartId == cart_id);

            // if (cart == null) return NotFound(new
            // {
            //     Success = false,
            //     Message = "cart not found for this user"
            // });

            // if (cart.User == null)
            // {
            //     return NotFound(new
            //     {
            //         Success = false,
            //         Message = "cart user is null"
            //     });
            // }

            // var authResult = await _authorizationService.AuthorizeAsync(
            //     User,
            //     cart.User,
            //    "UserOwnerShipPolicy");

            // if (authResult.Succeeded == false)
            // {
            //     return Unauthorized(new
            //     {
            //         Success = false,
            //         Message = "You are not authorized to create an order for this cart."
            //     });
            // }


            // var restaurant = await _dbContext.restaurants.AnyAsync(r => r.RestaurantId == cart.RestaurantId && !r.IsDeleted && r.IsOpen && r.RestaurantStatus == RestaurantStatuss.Accepted);
            // var menu = await _dbContext.menu_category.AnyAsync(m => m.RestaurantId == cart.RestaurantId && !m.IsDeleted);
            // var cart_item = await _dbContext.cart_items.Where(i => i.CartId == cart.CartId).ToListAsync();

            // if (!restaurant) return BadRequest(new
            // {
            //     Success = false,
            //     Message = "can not create order for this restaurant"
            // });

            // if (!menu) return BadRequest(new
            // {
            //     Success = false,
            //     Message = "can not create order for this menu"
            // });

            // if (cart_item.Count <= 0) return NotFound(new
            // {
            //     Success = false,
            //     Message = "no item in this cart"
            // });

            // var new_order = new Orders
            // {
            //     CartId = cart.CartId,
            //     UserId = cart.UserId,
            //     Status = OrderStatuss.Pending,
            //     CreateAt = DateTime.UtcNow,
            //     RestaurantId = cart.RestaurantId,
            //     Address = dto.addrees
            // };

            // decimal total_prcie = 0;


            // foreach (var item in cart_item)
            // {
            //     var item_price = await _dbContext.items.FirstOrDefaultAsync(i => i.ItemId == item.ItemId && i.IsActive && !i.IsDeleted);
            //     if (item_price == null) return NotFound(new
            //     {
            //         Success = false,
            //         Message = "can not found item for this order"
            //     });
            //     var new_order_item = new OrderItem
            //     {
            //         Quantity = item.Quantity,
            //         ItemId = item.ItemId,
            //         PriceAtPurchase = item_price.ItemPrice // snapshot  
            //     };
            //     total_prcie += (decimal)item_price.ItemPrice * item.Quantity;
            //     new_order.OrderItems.Add(new_order_item);// this add FK order id to order items 

            // }
            // new_order.TotalPrice = total_prcie;
            // _dbContext.orders.Add(new_order);


            // await _dbContext.SaveChangesAsync();

            // var new_order_status = new OrderStatus
            // {
            //     StatusName = OrderStatuss.Pending,
            //     OrderId = new_order.OrderId,
            //     OrderTime = DateTime.UtcNow
            // };

            // _dbContext.order_statuses.Add(new_order_status);

            // await _dbContext.SaveChangesAsync();
            // total_prcie = 0;
            // return Ok(new
            // {
            //     Success = true,
            //     Message = "new order is created",
            //     Data = new_order
            // });
        }




        [Authorize(Roles = "RestaurantManager,Admin,Customer")]
        [HttpGet("view_all/orders")]
        public async Task<IActionResult> view_all_orders()
        {

            string user_id = await GetUserIdFromToken();

            var result = await _orderservice.ViewAllOrdersService(user_id, User);

            return MapServiceResult(result);

            // var query = _dbContext.orders.AsQueryable();
            //

            // if (User.IsInRole("Customer"))
            // {
            //     query = query.Where(o => o.UserId == user_id);

            // }
            // else if (User.IsInRole("RestaurantManager"))
            // {

            //     var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.UserId == user_id);
            //     if (restaurant == null) return NotFound(new
            //     {
            //         Success = false,
            //         Message = "restaurant not found"
            //     });

            //     query = query.Where(o => o.RestaurantId == restaurant.RestaurantId);

            // }
            // var orders = await query
            //     .Select(o => new
            //     {
            //         user = o.User.fullName,
            //         cart = o.CartId,
            //         restaurant = o.Cart.Restaurant.Restaurant_Name,
            //         statis = o.Status,
            //         address = o.Address,
            //         time = o.CreateAt,
            //         items = o.OrderItems.Select(oi => new
            //         {
            //             item_name = oi.Item.ItemName,
            //             quantity = oi.Quantity,
            //             price = oi.PriceAtPurchase,
            //         }).ToList(),
            //         totalPrice = o.TotalPrice

            //     })
            //     .ToListAsync();

            // return Ok(new
            // {
            //     Success = true,
            //     Data = orders
            // });

        }



        [Authorize(Roles = "RestaurantManager,Admin")]
        [HttpGet("view/restaurant/{res_id}/orders")]
        public async Task<IActionResult> view_orders(int res_id)
        {
            var result = await _orderservice.ViewOrdersService(res_id, User);

            return MapServiceResult(result);

            // var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.RestaurantId == res_id);
            // if (restaurant == null) return NotFound(new
            // {
            //     Success = false,
            //     Message = "restaurant not found"
            // });

            // var authResult = await _authorizationService.AuthorizeAsync(
            //   User,
            //   restaurant,
            //  "RestauantOwnerShipAndAdminPolicy");

            // if (!authResult.Succeeded)
            //     return Unauthorized(new
            //     {
            //         Success = false,
            //         Message = "You are not authorized to view orders for this restaurant"
            //     }
            //         );

            // var orders = await _dbContext.orders
            //     .Where(o => o.RestaurantId == res_id)
            //     .Select(o => new
            //     {
            //         user = o.User.fullName,
            //         cart = o.CartId,
            //         restaurant = o.Cart.Restaurant.Restaurant_Name,
            //         statis = o.Status,
            //         address = o.Address,
            //         time = o.CreateAt,
            //         items = o.OrderItems.Select(oi => new
            //         {
            //             item_name = oi.Item.ItemName,
            //             quantity = oi.Quantity,
            //             price = oi.PriceAtPurchase,
            //         }).ToList(),
            //         totalPrice = o.TotalPrice

            //     }).ToListAsync();

            // return Ok(new
            // {
            //     Success = true,
            //     Data = orders
            // });
        }





        [Authorize(Roles = "RestaurantManager,Admin")]
        [HttpGet("view/pending/orders/fro_restaurant/{res_id}")]
        public async Task<IActionResult> view_pending_orders(int res_id)
        {

            var result = await _orderservice.ViewOrdersService(res_id, User);

            return MapServiceResult(result);
            // var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.RestaurantId == res_id);
            // if (restaurant == null) return NotFound(new
            // {
            //     Success = false,
            //     Message = "restaurant not found"
            // });

            // var authResult = await _authorizationService.AuthorizeAsync(
            //   User,
            //   restaurant,
            //  "RestauantOwnerShipAndAdminPolicy");

            // if (!authResult.Succeeded)
            //     return Unauthorized(new
            //     {
            //         Success = false,
            //         Message = "You are not authorized to view orders for this restaurant"
            //     }


            //        );

            // var orders = await _dbContext.orders
            //     .Where(o => o.RestaurantId == res_id && o.Status == OrderStatuss.Pending)
            //     .Select(o => new
            //     {
            //         user = o.User.fullName,
            //         cart = o.CartId,
            //         restaurant = o.Cart.Restaurant.Restaurant_Name,
            //         statis = o.Status,
            //         address = o.Address,
            //         time = o.CreateAt,
            //         items = o.OrderItems.Select(oi => new
            //         {
            //             item_name = oi.Item.ItemName,
            //             quantity = oi.Quantity,
            //             price = oi.PriceAtPurchase,
            //         }).ToList(),
            //         totalPrice = o.TotalPrice

            //     })
            //     .ToListAsync();

            // return Ok(new
            // {
            //     Success = true,
            //     Data = orders
            // });
        }



        [Authorize(Roles = "RestaurantManager")]
        [HttpPatch("change/order/status/{order_id}")]
        public async Task<IActionResult> change_order_status(int order_id)
        {

            var result = await _orderservice.ChangeOrderStatusService(order_id, User);

            return MapServiceResult(result);

            // var order = await _dbContext.orders
            // .Include(o => o.Cart)
            // .FirstOrDefaultAsync(o => o.OrderId == order_id);
            // if (order == null) return NotFound(new
            // {
            //     Success = false,
            //     Message = "order not found"
            // });

            // var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.RestaurantId == order.Cart.RestaurantId && !r.IsDeleted);
            // if (restaurant == null) return NotFound(new
            // {
            //     Success = false,
            //     Message = "Restaurant not found"
            // });

            // var authResult = await _authorizationService.AuthorizeAsync(
            //   User,
            //   restaurant,
            //   "RestauantOwnerShipAndAdminPolicy");

            // if (!authResult.Succeeded)
            //     return Unauthorized(new
            //     {
            //         Success = false,
            //         Message = "You are not authorized to view orders for this restaurant"
            //     }

            //         );

            // if (order.Status == OrderStatuss.Delivered)
            //     return BadRequest(new
            //     {
            //         Success = false,
            //         Message = "order already completed"
            //     });

            // switch (order.Status)
            // {
            //     case OrderStatuss.Pending:
            //         order.Status = OrderStatuss.Accepted;
            //         break;

            //     case OrderStatuss.Accepted:
            //         order.Status = OrderStatuss.Delivered;
            //         break;

            //     default:
            //         return BadRequest(new
            //         {
            //             Success = false,
            //             Message = "invalid status transition"
            //         });
            // }

            // _dbContext.order_statuses.Add(new OrderStatus
            // {
            //     StatusName = order.Status,
            //     OrderId = order.OrderId,
            //     OrderTime = DateTime.UtcNow
            // });

            // await _dbContext.SaveChangesAsync();
            // return Ok(new
            // {
            //     Success = true,
            //     message = $"Order status updated successfully. Order is now {order.Status.ToString()}"
            // });

        }


        [HttpGet("view/order/statistics/{numberDays}")]
        public async Task<IActionResult> GetOrderStatistic(int numberDays)
        {


            var result = await _orderservice.GetOrderStatisticService(numberDays);

            return MapServiceResult(result);
            // var end_date = DateTime.UtcNow.Date;
            // var start_date = DateTime.UtcNow.AddDays(-numberDays).Date;

            // //    var result = new List<object>();

            // var orders = await _dbContext.orders
            //         .Where(o => o.CreateAt.Date >= start_date && o.CreateAt.Date <= end_date)
            //         .Select(o => new
            //         {
            //             username = o.User.fullName,
            //             restaurantname = o.Restaurant.Restaurant_Name,
            //             price = o.TotalPrice,
            //             time = o.CreateAt.Date
            //         })
            //         .ToListAsync();

            // return Ok(new
            // {
            //     Success = true,
            //     orders = orders,
            //     from = start_date,
            //     to = end_date,
            //     count = orders.Count,
            //     message = $"Number of orders between the dates is {orders.Count}"
            // });
        }

        [Authorize(Roles = "Admin , RestaurantManager")]
        [HttpGet("view/total/orders")]
        public async Task<IActionResult> View_totalNumber_orders()
        {
            var user_id = await GetUserIdFromToken();

            var result = await _orderservice.ViewAllOrderNumbersService(user_id, User);
            return MapServiceResult(result);

            // var query = _dbContext.orders.AsQueryable();

            // if (User.IsInRole("RestaurantManager"))
            // {
            //     var user_id = await GetUserIdFromToken();

            //     var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.UserId == user_id);
            //     if (restaurant == null) return NotFound(new
            //     {
            //         Success = false,
            //         Message = "Restaurant not found"
            //     });

            //     //   Console.WriteLine(restaurant.User.UserName);
            //     var authResult = await _authorizationService.AuthorizeAsync(
            //       User,
            //       restaurant,
            //      "RestauantOwnerShipAndAdminPolicy");

            //     if (!authResult.Succeeded)
            //         return Unauthorized(new
            //         {
            //             Success = false,
            //             Message = "You are not allowe to view total revent for this restaurant"
            //         });

            //     query = query.Where(o => o.RestaurantId == restaurant.RestaurantId);

            // }

            // var num_orders = query.Count();

            // if (num_orders == 0) return NotFound(new
            // {
            //     Success = false,
            //     Message = "order not found"
            // });

            // return Ok(new
            // {
            //     Success = true,
            //     message = $" number of total orders = {num_orders}"
            // }
            //     );
        }

        [Authorize(Roles = "Admin,RestaurantManager")]
        [HttpGet("total/orders/ped_day/{num_days}")]
        public async Task<IActionResult> total_orders_per_day(int num_days)
        {
            var user_id = await GetUserIdFromToken();

            var result = await _orderservice.ViewOrderNumberPerDayService(num_days, user_id, User);
            return MapServiceResult(result);



        }

        [Authorize(Roles = "Admin,RestaurantManager")]
        [HttpGet("total/revent/{num_days}")]
        public async Task<IActionResult> total_revent(int num_days)
        {
            var user_id = await GetUserIdFromToken();
            var result = await _orderservice.ViewTotalRevientService(num_days, user_id, User);
            return MapServiceResult(result);

        }

        [Authorize(Roles = "Admin,RestaurantManager")]
        [HttpGet("get_most_selling_item")]
        public async Task<IActionResult> get_most_selig_item()
        {
            string user_id = await GetUserIdFromToken();
            var result = await _orderservice.GetMostSelinItemService(user_id, User);
            return MapServiceResult(result);

        }

        [Authorize(Roles = "Admin,RestaurantManager")]
        [HttpGet("view/orders/per/rstaurant")]
        public async Task<IActionResult> order_per_restaurant()
        {
            var user_id = await GetUserIdFromToken();

            var result = await _orderservice.ViewOrderPerRestaurant(user_id, User);
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