using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Modles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace food_order_system1.Service
{
    public interface IOrderSerivce
    {
        Task<ServiceResult<int>> CreateOderService(int cart_id, CreateOrderDTO dto, ClaimsPrincipal User);
        Task<ServiceResult<List<ViewOrderOrderItemDTO>>> ViewAllOrdersService(string user_id, ClaimsPrincipal User);
        Task<ServiceResult<List<ViewOrderOrderItemDTO>>> ViewOrdersService(int res_id, ClaimsPrincipal User);

        Task<ServiceResult<List<ViewOrderOrderItemDTO>>> ViewPendingOrderService(int res_id, ClaimsPrincipal User);

        Task<ServiceResult<OrderStatuss>> ChangeOrderStatusService(int order_id, ClaimsPrincipal User);

        Task<ServiceResult<ViewOrderStatisticDTO>> GetOrderStatisticService(int numberDays);
        Task<ServiceResult<int>> ViewAllOrderNumbersService(string user_id, ClaimsPrincipal User);
        Task<ServiceResult<object>> ViewOrderNumberPerDayService(int num_days, string user_id, ClaimsPrincipal User);

        Task<ServiceResult<double>> ViewTotalRevientService(int num_days, string user_id, ClaimsPrincipal User);

        Task<ServiceResult<List<ViewMostSelingItemDTO>>> GetMostSelinItemService(string user_id, ClaimsPrincipal User);

        Task<ServiceResult<List<ViewRestaurantOrderDTO>>> ViewOrderPerRestaurant(string user_id, ClaimsPrincipal User);

    }

    public class OrderSerivce : IOrderSerivce
    {
        private readonly AppUser _dbContext;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<ApplicationUser> _userManager;


        public OrderSerivce(AppUser context,
         IAuthorizationService authorizationService,
         UserManager<ApplicationUser> userManager)
        {
            _dbContext = context;
            _authorizationService = authorizationService;
            _userManager = userManager;
        }

        public async Task<ServiceResult<OrderStatuss>> ChangeOrderStatusService(int order_id, ClaimsPrincipal User)
        {
            var order = await _dbContext.orders
           .Include(o => o.Cart)
           .FirstOrDefaultAsync(o => o.OrderId == order_id);
            if (order == null) return new ServiceResult<OrderStatuss>
            {
                Success = false,
                Message = "order not found",
                StatusCode = 404
            };

            var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.RestaurantId == order.Cart.RestaurantId && !r.IsDeleted);
            if (restaurant == null) return new ServiceResult<OrderStatuss>
            {
                Success = false,
                Message = "Restaurant not found",
                StatusCode = 404
            };

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              restaurant,
              "RestauantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<OrderStatuss>
                {
                    Success = false,
                    Message = "You are not authorized to view orders for this restaurant",
                    StatusCode = 403
                };

            if (order.Status == OrderStatuss.Delivered)
                return new ServiceResult<OrderStatuss>
                {
                    Success = false,
                    Message = "order already completed",
                    StatusCode = 400
                };

            switch (order.Status)
            {
                case OrderStatuss.Pending:
                    order.Status = OrderStatuss.Accepted;
                    break;

                case OrderStatuss.Accepted:
                    order.Status = OrderStatuss.Delivered;
                    break;

                default:
                    return new ServiceResult<OrderStatuss>
                    {
                        Success = false,
                        Message = "invalid status transition",
                        StatusCode = 400
                    };
            }

            _dbContext.order_statuses.Add(new OrderStatus
            {
                StatusName = order.Status,
                OrderId = order.OrderId,
                OrderTime = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
            return new ServiceResult<OrderStatuss>
            {
                Success = true,
                Message = $"Order status updated successfully.",
                Data = order.Status,
                StatusCode = 200
            };

        }

        public async Task<ServiceResult<int>> CreateOderService(int cart_id, CreateOrderDTO dto, ClaimsPrincipal User)
        {
            var cart = await _dbContext.carts
             .Include(c => c.User)
            .FirstOrDefaultAsync(i => i.CartId == cart_id);

            if (cart == null) return new ServiceResult<int>
            {
                Success = false,
                Message = "cart not found for this user",
                StatusCode = 404
            };

            if (cart.User == null)
            {
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "cart user is null",
                    StatusCode = 404
                };
            }

            var authResult = await _authorizationService.AuthorizeAsync(
                User,
                cart.User,
               "UserOwnerShipPolicy");

            if (authResult.Succeeded == false)
            {
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "You are not authorized to create an order for this cart.",
                    StatusCode = 403
                };
            }


            var restaurant = await _dbContext.restaurants.AnyAsync(r => r.RestaurantId == cart.RestaurantId && !r.IsDeleted && r.IsOpen && r.RestaurantStatus == RestaurantStatuss.Accepted);
            var menu = await _dbContext.menu_category.AnyAsync(m => m.RestaurantId == cart.RestaurantId && !m.IsDeleted);
            var cart_item = await _dbContext.cart_items.Where(i => i.CartId == cart.CartId).ToListAsync();

            if (!restaurant) return new ServiceResult<int>
            {
                Success = false,
                Message = "can not create order for this restaurant",
                StatusCode = 400
            };

            if (!menu) return new ServiceResult<int>
            {
                Success = false,
                Message = "can not create order for this menu",
                StatusCode = 400
            };

            if (cart_item.Count <= 0) return new ServiceResult<int>
            {
                Success = false,
                Message = "no item in this cart",
                StatusCode = 404
            };

            var new_order = new Orders
            {
                CartId = cart.CartId,
                UserId = cart.UserId,
                Status = OrderStatuss.Pending,
                CreateAt = DateTime.UtcNow,
                RestaurantId = cart.RestaurantId,
                Address = dto.addrees
            };

            decimal total_prcie = 0;


            foreach (var item in cart_item)
            {
                var item_price = await _dbContext.items.FirstOrDefaultAsync(i => i.ItemId == item.ItemId && i.IsActive && !i.IsDeleted);
                if (item_price == null) return new ServiceResult<int>
                {
                    Success = false,
                    Message = "can not found item for this order",
                    StatusCode = 404
                };
                var new_order_item = new OrderItem
                {
                    Quantity = item.Quantity,
                    ItemId = item.ItemId,
                    PriceAtPurchase = item_price.ItemPrice // snapshot  
                };
                total_prcie += (decimal)item_price.ItemPrice * item.Quantity;
                new_order.OrderItems.Add(new_order_item);// this add FK order id to order items 

            }
            new_order.TotalPrice = total_prcie;
            _dbContext.orders.Add(new_order);


            await _dbContext.SaveChangesAsync();

            var new_order_status = new OrderStatus
            {
                StatusName = OrderStatuss.Pending,
                OrderId = new_order.OrderId,
                OrderTime = DateTime.UtcNow
            };

            _dbContext.order_statuses.Add(new_order_status);

            await _dbContext.SaveChangesAsync();
            total_prcie = 0;
            return new ServiceResult<int>
            {
                Success = true,
                Message = "new order is created",
                Data = new_order.OrderId,
                StatusCode = 201

            };


        }

        public async Task<ServiceResult<List<ViewMostSelingItemDTO>>> GetMostSelinItemService(string user_id, ClaimsPrincipal User)
        {

            var query = _dbContext.orderItems.AsQueryable();

            if (User.IsInRole("RestaurantManager"))
            {


                var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.UserId == user_id);
                if (restaurant == null) return new ServiceResult<List<ViewMostSelingItemDTO>>
                {
                    Success = false,
                    Message = "Restaurant not found",
                    StatusCode = 404
                };


                var authResult = await _authorizationService.AuthorizeAsync(
                  User,
                  restaurant,
                 "RestauantOwnerShipAndAdminPolicy");

                if (!authResult.Succeeded)
                    return new ServiceResult<List<ViewMostSelingItemDTO>>
                    {
                        Success = false,
                        Message = "You are not allowe to view most selling items",
                        StatusCode = 403
                    };

                query = query.Where(o => o.Order.RestaurantId == restaurant.RestaurantId);
            }
            var most_selling = await query
            .GroupBy(i => new { i.Order.Restaurant.Restaurant_Name, i.Item.ItemName })
            .Select(o => new ViewMostSelingItemDTO
            {
                item_name = o.Key.ItemName,
                restaurantName = o.Key.Restaurant_Name,
                time_of_orders = o.Sum(x => x.Quantity)
            })
             .OrderByDescending(r => r.time_of_orders)
             .ToListAsync();

            return new ServiceResult<List<ViewMostSelingItemDTO>>
            {
                Success = true,
                Data = most_selling,
                StatusCode = 200
            };


        }

        public async Task<ServiceResult<ViewOrderStatisticDTO>> GetOrderStatisticService(int numberDays)
        {
            var end_date = DateTime.UtcNow.Date;
            var start_date = DateTime.UtcNow.AddDays(-numberDays).Date;

            //    var result = new List<object>();

            var orders = await _dbContext.orders
                    .Where(o => o.CreateAt.Date >= start_date && o.CreateAt.Date <= end_date)
                    .Select(o => new ViewOrderDTO
                    {
                        UserName = o.User.fullName,
                        restaurant = o.Restaurant.Restaurant_Name,
                        OrderPrice = (double)o.TotalPrice,
                        time = o.CreateAt.Date
                    })
                    .ToListAsync();



            ViewOrderStatisticDTO OrderStatistic = new ViewOrderStatisticDTO
            {
                orders = orders,
                from = start_date.ToString(),
                to = end_date.ToString(),
                OrderNumbers = orders.Count == 0 ? 0 : orders.Count
            };

            return new ServiceResult<ViewOrderStatisticDTO>
            {
                Success = true,
                Data = OrderStatistic,
                StatusCode = 200

            };



        }

        public async Task<ServiceResult<int>> ViewAllOrderNumbersService(string user_id, ClaimsPrincipal User)
        {
            var query = _dbContext.orders.AsQueryable();

            if (User.IsInRole("RestaurantManager"))
            {

                var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.UserId == user_id);
                if (restaurant == null) return new ServiceResult<int>
                {
                    Success = false,
                    Message = "Restaurant not found",
                    StatusCode = 404
                };

                //   Console.WriteLine(restaurant.User.UserName);
                var authResult = await _authorizationService.AuthorizeAsync(
                  User,
                  restaurant,
                 "RestauantOwnerShipAndAdminPolicy");

                if (!authResult.Succeeded)
                    return new ServiceResult<int>
                    {
                        Success = false,
                        Message = "You are not allowe to view total revent for this restaurant",
                        StatusCode = 403
                    };

                query = query.Where(o => o.RestaurantId == restaurant.RestaurantId);

            }

            var num_orders = query.Count();

            if (num_orders == 0) return new ServiceResult<int>
            {
                Success = false,
                Message = "order not found",
                StatusCode = 404
            };

            return new ServiceResult<int>
            {
                Success = true,
                Data = num_orders == 0 ? 0 : num_orders,
                StatusCode = 200

            };
        }

        public async Task<ServiceResult<List<ViewOrderOrderItemDTO>>> ViewAllOrdersService(string user_id, ClaimsPrincipal User)
        {

            var query = _dbContext.orders.AsQueryable();

            if (User.IsInRole("Customer"))
            {
                query = query.Where(o => o.UserId == user_id);

            }
            else if (User.IsInRole("RestaurantManager"))
            {

                var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.UserId == user_id);
                if (restaurant == null) return new ServiceResult<List<ViewOrderOrderItemDTO>>
                {
                    Success = false,
                    Message = "restaurant not found",
                    StatusCode = 404
                };

                query = query.Where(o => o.RestaurantId == restaurant.RestaurantId);

            }
            var orders = await query
                .Select(o => new ViewOrderOrderItemDTO
                {
                    user = o.User.fullName,
                    cart = o.CartId,
                    restaurant = o.Cart.Restaurant.Restaurant_Name,
                    statis = o.Status,
                    address = o.Address,
                    time = o.CreateAt,
                    items = o.OrderItems.Select(oi => new ViewItemDTO
                    {
                        item_name = oi.Item.ItemName,
                        quantity = oi.Quantity,
                        price = (double)oi.PriceAtPurchase,
                    }).ToList(),
                    totalPrice = (double)o.TotalPrice

                })
                .ToListAsync();

            return new ServiceResult<List<ViewOrderOrderItemDTO>>
            {
                Success = true,
                Data = orders,
                StatusCode = 200
            };


        }

        public async Task<ServiceResult<object>> ViewOrderNumberPerDayService(int num_days, string user_id, ClaimsPrincipal User)
        {
            var query = _dbContext.orders.AsQueryable();

            if (User.IsInRole("RestaurantManager"))
            {


                var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.UserId == user_id);
                if (restaurant == null) return new ServiceResult<object>
                {
                    Success = false,
                    Message = "Restaurant not found",
                    StatusCode = 404
                };

                //   Console.WriteLine(restaurant.User.UserName);
                var authResult = await _authorizationService.AuthorizeAsync(
                  User,
                  restaurant,
                 "RestauantOwnerShipAndAdminPolicy");

                if (!authResult.Succeeded)
                    return new ServiceResult<object>
                    {
                        Success = false,
                        Message = "You are not allowe to view total revent for this restaurant",
                        StatusCode = 403
                    };

                query = query.Where(o => o.RestaurantId == restaurant.RestaurantId);

            }

            var result = new List<object>();

            for (int i = 0; i <= num_days; i++)
            {



                var orders = await query
                        .Where(o => o.CreateAt.Date == DateTime.UtcNow.AddDays(-i).Date).ToListAsync();

                result.Add(new
                {
                    day = DateTime.UtcNow.AddDays(-i).Date.ToString("dd/MM/yyyy"),
                    order_number = orders.Count
                }

             );
            }
            ;

            return new ServiceResult<object>
            {
                Success = true,
                Data = result,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<List<ViewRestaurantOrderDTO>>> ViewOrderPerRestaurant(string user_id, ClaimsPrincipal User)
        {
            var query = _dbContext.orderItems.AsQueryable();
            if (User.IsInRole("RestaurantManager"))
            {


                var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.UserId == user_id);
                if (restaurant == null) return new ServiceResult<List<ViewRestaurantOrderDTO>>
                {
                    Success = false,
                    Message = "Restaurant not found",
                    StatusCode = 404
                };


                var authResult = await _authorizationService.AuthorizeAsync(
                  User,
                  restaurant,
                 "RestauantOwnerShipAndAdminPolicy");

                if (!authResult.Succeeded)
                    return new ServiceResult<List<ViewRestaurantOrderDTO>>
                    {
                        Success = false,
                        Message = "You are not allowe to view orders for this restaurant",
                        StatusCode = 403
                    };

                query = query.Where(oi => oi.Order.Restaurant.UserId == user_id);
            }

            var orders = await query
                     .GroupBy(oi => new { oi.Order.Restaurant.RestaurantId, oi.Order.Restaurant.Restaurant_Name })
                     .Select(g => new ViewRestaurantOrderDTO
                     {
                         RestaurantName = g.Key.Restaurant_Name,
                         Orders = g.Select(oi => new ViewItemOrderItemDTO
                         {
                             item_name = oi.Item.ItemName,
                             quantity = oi.Quantity,
                             timecreated = oi.Order.CreateAt,
                         }).ToList()
                     }).ToListAsync();

            if (!orders.Any()) return new ServiceResult<List<ViewRestaurantOrderDTO>>
            {
                Success = false,
                Message = "order not found",
                StatusCode = 404
            };

            return new ServiceResult<List<ViewRestaurantOrderDTO>>
            {
                Success = true,
                Data = orders,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<List<ViewOrderOrderItemDTO>>> ViewOrdersService(int res_id, ClaimsPrincipal User)
        {

            var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.RestaurantId == res_id);
            if (restaurant == null) return new ServiceResult<List<ViewOrderOrderItemDTO>>
            {
                Success = false,
                Message = "restaurant not found",
                StatusCode = 404
            };

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              restaurant,
             "RestauantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<List<ViewOrderOrderItemDTO>>
                {
                    Success = false,
                    Message = "You are not authorized to view orders for this restaurant",
                    StatusCode = 403
                };

            var orders = await _dbContext.orders
                .Where(o => o.RestaurantId == res_id)
                .Select(o => new ViewOrderOrderItemDTO
                {
                    user = o.User.fullName,
                    cart = o.CartId,
                    restaurant = o.Cart.Restaurant.Restaurant_Name,
                    statis = o.Status,
                    address = o.Address,
                    time = o.CreateAt,
                    items = o.OrderItems.Select(oi => new ViewItemDTO
                    {
                        item_name = oi.Item.ItemName,
                        quantity = oi.Quantity,
                        price = (double)oi.PriceAtPurchase,
                    }).ToList(),
                    totalPrice = (double)o.TotalPrice

                }).ToListAsync();

            return new ServiceResult<List<ViewOrderOrderItemDTO>>
            {
                Success = true,
                Data = orders,
                StatusCode = 200
            };


        }

        public async Task<ServiceResult<List<ViewOrderOrderItemDTO>>> ViewPendingOrderService(int res_id, ClaimsPrincipal User)
        {
            var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.RestaurantId == res_id);
            if (restaurant == null) return new ServiceResult<List<ViewOrderOrderItemDTO>>
            {
                Success = false,
                Message = "restaurant not found",
                StatusCode = 404
            };

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              restaurant,
             "RestauantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<List<ViewOrderOrderItemDTO>>
                {
                    Success = false,
                    Message = "You are not authorized to view orders for this restaurant",
                    StatusCode = 403
                };

            var orders = await _dbContext.orders
                .Where(o => o.RestaurantId == res_id && o.Status == OrderStatuss.Pending)
                .Select(o => new ViewOrderOrderItemDTO
                {
                    user = o.User.fullName,
                    cart = o.CartId,
                    restaurant = o.Cart.Restaurant.Restaurant_Name,
                    statis = o.Status,
                    address = o.Address,
                    time = o.CreateAt,
                    items = o.OrderItems.Select(oi => new ViewItemDTO
                    {
                        item_name = oi.Item.ItemName,
                        quantity = oi.Quantity,
                        price = (double)oi.PriceAtPurchase,
                    }).ToList(),
                    totalPrice = (double)o.TotalPrice

                })
                .ToListAsync();

            return new ServiceResult<List<ViewOrderOrderItemDTO>>
            {
                Success = true,
                Data = orders,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<double>> ViewTotalRevientService(int num_days, string user_id, ClaimsPrincipal User)
        {

            var query = _dbContext.orders.AsQueryable();
            var start_date = DateTime.UtcNow.AddDays(-num_days);


            if (User.IsInRole("RestaurantManager"))
            {


                var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.UserId == user_id);
                if (restaurant == null) return new ServiceResult<double>
                {
                    Success = false,
                    Message = "Restaurant not found",
                    StatusCode = 404
                };


                var authResult = await _authorizationService.AuthorizeAsync(
                  User,
                  restaurant,
                 "RestauantOwnerShipAndAdminPolicy");

                if (!authResult.Succeeded)
                    return new ServiceResult<double>
                    {
                        Success = false,
                        Message = "You are not allowe to view total revent for this restaurant",
                        StatusCode = 403
                    };

                query = query.Where(o => o.RestaurantId == restaurant.RestaurantId);
            }
            var order_revent = await query.Where(o => o.CreateAt >= start_date)
            .SumAsync(o => o.TotalPrice);

            if (order_revent == 0) return new ServiceResult<double>
            {
                Success = false,
                Message = "order not found",
                StatusCode = 404
            };

            return new ServiceResult<double>
            {
                Success = true,
                Data = (double)order_revent,
                StatusCode = 200
            };

        }
    }
}