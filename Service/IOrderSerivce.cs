using System;
using System.Text.Json;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Flters;
using food_order_system1.Modles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Abstractions;
using static food_order_system1.Controllers.OrderController;

namespace food_order_system1.Service
{
    public interface IOrderSerivce
    {
        Task<ServiceResult<int>> CreateOderService(Cart cart, CreateOrderDTO dto);
        Task<ServiceResult<PaginationResponse<ViewOrderOrderItemDTO>>> ViewAllOrdersService(PaginationParams p, OrderFilter rf, string user_id, UserRolee role);
        Task<ServiceResult<OrderStatuss>> ChangeOrderStatusService(Orders order);
        Task<ServiceResult<ViewOrderStatisticDTO>> GetOrderStatisticService(int? numberDays, string use_id, UserRolee role);
        Task<ServiceResult<List<ViewOrderNumberPerDay>>> ViewOrderNumberPerDayService(int num_days ,int? res_id, string user_id, UserRolee role);
        Task<ServiceResult<List<ViewMostSelingItemDTO>>> GetMostSelinItemService(string user_id, UserRolee role);

        Task<(Cart?, CartAuthorizationDTO?)> GetCartEntityAndAuth(int cart_id);

        Task<(Orders?, RestaurantAuthorizationDTO?)> GetOrderEntityAndAuth(int order_id);

    }

    public class OrderSerivce : IOrderSerivce
    {
        private readonly AppUser _dbContext;
        private readonly ILogger<OrderSerivce> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _redisCache;


        public OrderSerivce(AppUser context, ILogger<OrderSerivce> logger ,IMemoryCache memoryCache , IDistributedCache redisCache)
        {
            _dbContext = context;
            _logger = logger;
            _memoryCache = memoryCache;
            _redisCache = redisCache;

        }

                public async Task<ServiceResult<int>> CreateOderService(Cart cart, CreateOrderDTO dto)
        {
            var restaurant = await _dbContext.restaurants.AnyAsync(r => r.RestaurantId == cart.RestaurantId && !r.IsDeleted && r.IsOpen && r.RestaurantStatus == RestaurantStatuss.Accepted);
            var menu = await _dbContext.menu_category.AnyAsync(m => m.RestaurantId == cart.RestaurantId && !m.IsDeleted);
            var cart_item = await _dbContext.cart_items.Where(i => i.CartId == cart.CartId).ToListAsync();

            if (!restaurant) 
            {_logger.LogWarning("can not create order for this restaurant {RestaurantId}",cart.RestaurantId);
                return new ServiceResult<int>
            {
                Success = false,
                Message = "can not create order for this restaurant",
                StatusCode = 400
            };}

              if (!menu) 
            {_logger.LogWarning("can not found menu for  restaurant {RestaurantId}",cart.RestaurantId);
                return new ServiceResult<int>
            {
                Success = false,
                Message = "menu not found",
                StatusCode = 400
            };}

            if (cart_item.Count <= 0) 
            {_logger.LogWarning("can not create order for this cart because no item in this cart{cart_id}",cart.CartId);
                return new ServiceResult<int>
            {
                Success = false,
                Message = "no item in this cart",
                StatusCode = 404
            };}

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


            var IdAndQuantityList = cart_item.ToDictionary(i => i.ItemId, i => i.Quantity);

            var items = await _dbContext.items
              .Where(i => IdAndQuantityList.Keys.Contains(i.ItemId) && i.IsActive && !i.IsDeleted).ToListAsync();

            if (items.Count != IdAndQuantityList.Keys.Count) 
            {
                _logger.LogWarning("can not create order because cart {cart_id} has no items  ",cart.CartId);
                return new ServiceResult<int>
            {
                Success = false,
                Message = "can not found item for this order",
                StatusCode = 404
            };}


            foreach (var item in items)
            {
                var new_order_item = new OrderItem
                {
                    Quantity = IdAndQuantityList[item.ItemId],
                    ItemId = item.ItemId,
                    PriceAtPurchase = item.ItemPrice // snapshot  
                };
                total_prcie += item.ItemPrice * IdAndQuantityList[item.ItemId];

                new_order.OrderItems.Add(new_order_item);// this add FK order id to order items 

            }
            new_order.TotalPrice = total_prcie;



            var new_order_status = new OrderStatus
            {
                StatusName = OrderStatuss.Pending,
                order = new_order,
                OrderTime = DateTime.UtcNow
            };



            _dbContext.orders.Add(new_order);
            _dbContext.order_statuses.Add(new_order_status);

            await _dbContext.SaveChangesAsync();

            total_prcie = 0;
            _logger.LogInformation("user {user_id} create new order {order_id} in restaurant {restaurant_id}",cart.UserId, new_order.OrderId , cart.RestaurantId);
            return new ServiceResult<int>
            {
                Success = true,
                Message = "new order is created",
                Data = new_order.OrderId,
                StatusCode = 201

            };


        }


        public async Task<ServiceResult<OrderStatuss>> ChangeOrderStatusService(Orders order)
        {

            if (order.Status == OrderStatuss.Delivered)
            {
                _logger.LogWarning("order already completed you can not change status {orderId}", order.OrderId);
                return new ServiceResult<OrderStatuss>
                {
                    Success = false,
                    Message = "order already completed",
                    StatusCode = 400
                };
            }
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
            _logger.LogInformation("order {OrderId} status changed to {Status}", order.OrderId, order.Status);
            return new ServiceResult<OrderStatuss>
            {
                Success = true,
                Message = $"Order status updated successfully.",
                Data = order.Status,
                StatusCode = 200
            };

        }

        public async Task<ServiceResult<List<ViewMostSelingItemDTO>>> GetMostSelinItemService(string user_id, UserRolee role)
        {

            var query = _dbContext.orderItems.Where(oi=> oi.Order.Status == OrderStatuss.Delivered).AsNoTracking().AsQueryable();

           if(role == UserRolee.Customer)
            {
                query = query.Where(o => o.Order.UserId == user_id);
            }
           else if (role == UserRolee.RestaurantManager)
            {
                   var restaurant = await GetRestaurantCache(user_id);
                if (restaurant == null)
                {
                    _logger.LogWarning("restaurant not ofund for user {user_id}",user_id);
                    return new ServiceResult<List<ViewMostSelingItemDTO>>
                    {
                        Success = false,
                        Message = "Restaurant not found",
                        StatusCode = 404
                    };
                }
                query = query.Where(o => o.Order.RestaurantId == restaurant.RestaurantId);
            }
            var most_selling = await query
            .GroupBy(i => new { 
                i.ItemId,
                i.Order.RestaurantId,
                i.Order.Restaurant.Restaurant_Name, 
                i.Item.ItemName
                 })
            .Select(o => new ViewMostSelingItemDTO
            {
                itemName = o.Key.ItemName,
                restaurantName = o.Key.Restaurant_Name,
                timeOfOrders = o.Select(x=> (int?)x.Quantity).Sum() ?? 0
            })
             .OrderByDescending(r => r.timeOfOrders)
             .Take(10)
             .ToListAsync();

            if (!most_selling.Any())
            {
                _logger.LogWarning("no ordered item found ");
                return new ServiceResult<List<ViewMostSelingItemDTO>>
            {
                Success = false,
                Message= "no ordered item found",
                Data = null,
                StatusCode = 404
            };  
            }
          
             _logger.LogInformation("most selling items is showed to user { user_id}",user_id);
            return new ServiceResult<List<ViewMostSelingItemDTO>>
            {
                Success = true,
                Data = most_selling,
                StatusCode = 200
            };


        }

   

        public async Task<ServiceResult<ViewOrderStatisticDTO>> GetOrderStatisticService(int? numberDays, string user_id, UserRolee role)
        {
              var query = _dbContext.orders
              .Where(o => o.Status == OrderStatuss.Delivered)
             .AsNoTracking()
             .AsQueryable();

            if(numberDays.HasValue)
            {
            var start_date = DateTime.UtcNow.AddDays(-numberDays.Value).Date;
             var end_date = DateTime.UtcNow.AddDays(1).Date;

             query=query.Where(o => o.CreateAt >= start_date && o.CreateAt  < end_date);
            }



            if (role == UserRolee.Customer)
            {
               query = query.Where(o => o.UserId == user_id);  
            }
          
           else if (role == UserRolee.RestaurantManager)
            {
                var restaurant=await GetRestaurantCache(user_id);
                if(restaurant == null)
                {  _logger.LogWarning("restaurant not found for user {user_id}",user_id);
                    return new ServiceResult<ViewOrderStatisticDTO>
                    {
                        Success=false,
                        Message="restaurant not found",
                        StatusCode=404
                        
                    };
                }
                query = query.Where(o => o.Restaurant.UserId == user_id);
            }

            var totalRevent = await query
            .Select(o => (decimal?) o.TotalPrice)
            .SumAsync() ?? 0;
            var orderNumber=await query.CountAsync();



            ViewOrderStatisticDTO OrderStatistic = new ViewOrderStatisticDTO
            {
                from = numberDays.HasValue ?  DateTime.UtcNow.AddDays(-numberDays.Value).Date.ToString("yyyy-MM-dd") : "",
                to = numberDays.HasValue ?  DateTime.UtcNow.AddDays(1).Date.ToString("yyyy-MM-dd"):"",
                OrderNumbers = orderNumber ,
                TotalRevent=(double) totalRevent
            };
   
            if(orderNumber == 0)
            {
                _logger.LogWarning("order ont found to show for user {user_id}",user_id);
                  return new ServiceResult<ViewOrderStatisticDTO>
                 {
                   Success = false,
                   Message="no order found",
                   Data = null,
                   StatusCode = 404
                  };    
            }
            
            _logger.LogInformation(
    "Order statistics (total revenue and order count) shown for user {UserId}",
    user_id);

            return new ServiceResult<ViewOrderStatisticDTO>
            {
                Success = true,
                Data = OrderStatistic,
                StatusCode = 200

            };

        }


        public async Task<ServiceResult<PaginationResponse<ViewOrderOrderItemDTO>>> ViewAllOrdersService(PaginationParams pagination, OrderFilter filter, string user_id, UserRolee role)
        {

            var query = _dbContext.orders
            .Where(o => o.Status == filter.Status)
            .AsNoTracking()
            .AsQueryable();

            if (role == UserRolee.Customer)
            {
                query = query.Where(o => o.UserId == user_id);

            }
            else if (role == UserRolee.RestaurantManager)
            {

                var restaurant = await GetRestaurantCache(user_id);
                if (restaurant == null)
                {
                    _logger.LogInformation("Restaurant not found for user  {user_id}", user_id);
                    return new ServiceResult<PaginationResponse<ViewOrderOrderItemDTO>>
                    {
                        Success = false,
                        Message = "Restaurant not found",
                        StatusCode = 404
                    };
                }
                query = query.Where(o => o.RestaurantId == restaurant.RestaurantId);

            }

            // filter by price if min or max price has value
            query = ApplyFilter(query, filter, role);

            // get total count of data after filtering and before pagination
            var totaldata = await query.CountAsync();
            _logger.LogInformation("Total orders count after filtering: {TotalCount}", totaldata);



            var orders = await query
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
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

_logger.LogInformation(
    "orders retrieved from database with pagination - PageNumber: {PageNumber}, PageSize: {PageSize}, TotalCount: {TotalCount}",
    pagination.PageNumber,
    pagination.PageSize,
    totaldata
);            // return data with pagination info
            return new ServiceResult<PaginationResponse<ViewOrderOrderItemDTO>>
            {
                Success = true,
                Data = new PaginationResponse<ViewOrderOrderItemDTO>
                {
                    pageSize = pagination.PageSize,
                    pageNumber = pagination.PageNumber,
                    totalCount = totaldata,
                    Data = orders
                },
                StatusCode = 200
            };


        }

        // this method used to apply filtering for GetAllItemsService  method
        private IQueryable<Orders> ApplyFilter(IQueryable<Orders> query, OrderFilter filter, UserRolee role)
        {
            if (filter.orderId.HasValue)
            {
                query = query.Where(o => o.OrderId == filter.orderId.Value);
                _logger.LogDebug("Applied  order id filter: {orderId}", filter.orderId.Value);
            }
            if (filter.RestaurantId.HasValue && role == UserRolee.Admin)
            {
                query = query.Where(o => o.RestaurantId == filter.RestaurantId.Value);
                _logger.LogDebug("Applied  restaurant id filter: {RestaurantId}", filter.RestaurantId.Value);
            }
            if (!string.IsNullOrWhiteSpace(filter.restaurantName))
            {
                query = query.Where(o => o.Restaurant.Restaurant_Name.Contains(filter.restaurantName));
                _logger.LogDebug("Applied restaurant name filter: {restaurantName}", filter.restaurantName);
            }
            if (!string.IsNullOrWhiteSpace(filter.userName))
            {
                query = query.Where(O => O.User.fullName.Contains(filter.userName));
                _logger.LogDebug("Applied user name filter: {userName}", filter.userName);
            }
            if (!string.IsNullOrEmpty(filter.SortBy))
            {
                query = ApplySorting(query, filter.SortBy, filter.FromLowToHigh);
                _logger.LogDebug("Applied sorting - SortBy: {SortBy}, FromLowToHigh: {FromLowToHigh}", filter.SortBy, filter.FromLowToHigh);
            }
            else
            {
                // default sorting by ID
                query = query.OrderByDescending(o => o.OrderId);
                _logger.LogDebug("Applied default sorting by orderId in decesending order");
            }
            return query;
        }

        // this method used to apply sorting for GetAllItemsService method 
        private IQueryable<Orders> ApplySorting(IQueryable<Orders> query, string sort_by, bool? from_low_to_high)
        {
            bool ascending = from_low_to_high.HasValue ? from_low_to_high.Value : true; // default to ascending if not specified

            switch (sort_by.ToLower())
            {
                case "restaurant id":
                    query = ascending ? query.OrderBy(i => i.RestaurantId) : query.OrderByDescending(i => i.RestaurantId);
                    _logger.LogDebug("Sorting by restaurant id in {Order} order", ascending ? "ascending" : "descending");
                    break;
                case "restaurant name":
                    query = ascending ? query.OrderBy(i => i.Restaurant.Restaurant_Name) : query.OrderByDescending(i => i.Restaurant.Restaurant_Name);
                    _logger.LogDebug("Sorting by restaurant name in {Order} order", ascending ? "ascending" : "descending");
                    break;

                case "user name":
                    query = ascending ? query.OrderBy(i => i.User.UserName) : query.OrderByDescending(i => i.User.UserName);
                    _logger.LogDebug("Sorting by user name in {Order} order", ascending ? "ascending" : "descending");
                    break;

                case "order id":
                default:
                    // Default sorting if sort_by value is unrecognized
                    query = ascending ? query.OrderBy(i => i.OrderId) : query.OrderByDescending(i => i.OrderId);
                    _logger.LogDebug("Sorting by ID in {Order} order (default)", ascending ? "ascending" : "descending");
                    break;
            }

            return query;
        }

        public async Task<ServiceResult<List<ViewOrderNumberPerDay>>> ViewOrderNumberPerDayService(int num_days ,  int? restaurant_id, string user_id, UserRolee role)
        {

              var start_date = DateTime.UtcNow.AddDays(-num_days).Date;
             var end_date = DateTime.UtcNow.Date;

    
            var query = _dbContext.orders
            .Where(o=> o.Status == OrderStatuss.Delivered && o.CreateAt >= start_date && o.CreateAt  < end_date).AsNoTracking().AsQueryable();


            if (role == UserRolee.RestaurantManager)
            {
               var restaurant=await GetRestaurantCache(user_id);
                 if (restaurant == null)
                {
                    _logger.LogDebug("Restaurant not found for user {user_id}", user_id);
                    return new ServiceResult<List<ViewOrderNumberPerDay>>
                    {
                        Success = false,
                        Message = "Restaurant not found",
                        StatusCode = 404
                    };
                }
                query = query.Where(o => o.RestaurantId == restaurant.RestaurantId);
            }
           else if(role  == UserRolee.Admin && restaurant_id.HasValue)
            {
               query = query.Where(o => o.RestaurantId == restaurant_id);  
            }

            var result = new List<ViewOrderNumberPerDay>();

             var orders = await query
                        .GroupBy(o => new
                        {
                            o.CreateAt.Year,
                            o.CreateAt.Month,
                            o.CreateAt.Day 
                        })
                        .Select(g=> new
                        {
                            date=new DateTime(g.Key.Year , g.Key.Month , g.Key.Day),
                            orderNumber=g.Count()
                        }).ToListAsync();
           
            var orderDict=orders.ToDictionary(x=>x.date , x=>x.orderNumber);    
                     
            for (int i = 0; i < num_days; i++)
            {
               var  day = DateTime.UtcNow.AddDays(-i).Date;
              orderDict.TryGetValue(day,out var count);
                result.Add(new ViewOrderNumberPerDay
                {
                    Day = day.ToString("dd/MM/yyyy"),
                    orderNumber = count
                }

             );
            };
            _logger.LogInformation("oder number per day showed");
            return new ServiceResult<List<ViewOrderNumberPerDay>>
            {
                Success = true,
                Data = result.OrderBy(r => ((dynamic)r).Day).ToList(),
                StatusCode = 200
            };
        }


        public async Task<(Cart?, CartAuthorizationDTO?)> GetCartEntityAndAuth(int cart_id)
        {
            var cart = await _dbContext.carts.Where(c => c.CartId == cart_id)
            .Select(c => new
            {
                cart = c,
                auth = new CartAuthorizationDTO
                {
                    cartId = c.CartId,
                    ownerId = c.UserId
                }
            }).FirstOrDefaultAsync();

            if (cart == null)
                return (null, null);

            return (cart.cart, cart.auth);
        }

    
             public async Task<(Orders?, RestaurantAuthorizationDTO?)> GetOrderEntityAndAuth(int order_id)
        {
            var order = await _dbContext.orders
              .Where(o => o.OrderId == order_id)
              .Select(o => new
              {
                  orders = o,
                  Auth = new RestaurantAuthorizationDTO
                  {
                      RestaurantId = o.RestaurantId,
                      ownerId = o.Restaurant.UserId
                  }
              }).FirstOrDefaultAsync();

            if (order == null) return (null, null);

            return (order.orders, order.Auth);
        }

        public async Task<Cart?> GetCart(int cart_id)
        {
            return await _dbContext.carts
           .FirstOrDefaultAsync(i => i.CartId == cart_id);
        }
       public async Task<GetRestaurantCacheDTO?> GetRestaurantCache(string userId)
        {

            string versionString = $"User_Restaurant_Version_{userId}";
            var version = await _redisCache.GetStringAsync(versionString) ?? "1";

            string Key = $"User_Restaurant_{version}_{userId}";

            if (_memoryCache.TryGetValue(Key, out GetRestaurantCacheDTO? checkRestaurantMemory))
            {
                _logger.LogDebug("restaurant for user {UserId} retrieved from cache", userId);
                return checkRestaurantMemory;

            }
            // 1. Try get from cache
            var cachedData = await _redisCache.GetStringAsync(Key);

            if (cachedData != null)
            {
                var checkRestaurantRedis = JsonSerializer.Deserialize<GetRestaurantCacheDTO?>(cachedData);

                _logger.LogDebug("restaurant for user {UserId} retrieved from Redis cache", userId);

                // add data from redis to memory cache for faster access next time
                _memoryCache.Set(Key, checkRestaurantRedis, TimeSpan.FromMinutes(5));
                _logger.LogDebug("for user {UserId} stored in CACHE", userId);

                return checkRestaurantRedis;
            }

            var DbRestaurant = await _dbContext.restaurants
             .Where(r => r.UserId == userId && !r.IsDeleted && r.RestaurantStatus == RestaurantStatuss.Accepted && r.IsOpen)
             .Select(r => new GetRestaurantCacheDTO
             {
                 restaurantName = r.Restaurant_Name,
                 RestaurantId = r.RestaurantId,
                 ownerId = r.UserId
             }).FirstOrDefaultAsync();

            // // 2. Store in both caches
            _memoryCache.Set(Key, DbRestaurant, TimeSpan.FromMinutes(3));
            _logger.LogDebug("restaurant for user {UserId} stored in CACHE", userId);

            await _redisCache.SetStringAsync(
             Key,
             JsonSerializer.Serialize(DbRestaurant),
             new DistributedCacheEntryOptions
             {
                 AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
             }
             );
            _logger.LogDebug("restaurant {restaurant_id} stored in Redis & in memory cache", DbRestaurant?.RestaurantId);

            return DbRestaurant;
        }
    }
}