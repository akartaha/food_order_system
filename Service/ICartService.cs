using System;
using System.Text.Json;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Flters;
using food_order_system1.Modles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace food_order_system1.Service
{
    public interface ICartService
    {
        Task<ServiceResult<int>> CreateCartService(string user_id, int restaurantId, CreateCartDTO request_cart);
        Task<ServiceResult<int>> AddItemToCartService(Cart cart, AddItemToCartDTO request_item);
        Task<ServiceResult<int>> UpdateItemCartService(CartItem cartItem, string owner_id, UpdateCartItem dto);
        Task<ServiceResult<string>> DeleteItemCartService(CartItem cartItem, string owner_id);
        //Task<ServiceResult<PaginationResponse<GetItemDTO>>> ViewCartItemsService(int cart_id);
        Task<ServiceResult<PaginationResponse<GetCartItemDTO>>> ViewAllCartItemsService(PaginationParams p, CartFilter filter, string id);
        Task<(Cart? , CartAuthorizationDTO?)> GetCartEntityAndAuth(int cart_id);
     
        Task<(CartItem? , CartAuthorizationDTO?)> GetCartItemEntityAndAuth(int cart_item_id);

    }

    public class CartService : ICartService

    {

        private readonly AppUser _dbContext;

        private readonly IMemoryCache _memoryCache;

        private readonly IDistributedCache _redisCache;

        private readonly ILogger<ItemService> _logger;
        private readonly IcacheService _cacheService;


        public CartService(AppUser context, ILogger<ItemService> logger, IMemoryCache memoryCache, IDistributedCache redisCache, IcacheService cacheService)
        {
            _dbContext = context;
            _memoryCache = memoryCache;
            _redisCache = redisCache;
            _logger = logger;
            _cacheService = cacheService;
        }

 
         // this method create new cart for user and update cache id success
        public async Task<ServiceResult<int>> CreateCartService(string userId, int restaurantId, CreateCartDTO request_cart)
        {
            
            var restaurant = await GetRestaurantCache(restaurantId);


            if (restaurant == null)
            {
                _logger.LogWarning("Restaurant not found with id {RestaurantId}", restaurantId);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "restaurant not found",
                    StatusCode = 404
                };
            }


            var cartExist = await CheckCartExist(userId, restaurant.RestaurantId);

            if (cartExist)
            {
                _logger.LogWarning("User {UserId} already has cart for restaurant {RestaurantId}", userId, restaurantId);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "you already have a cart for this restaurant",
                    StatusCode = 400
                };
            }
            var newCart = new Cart
            {
                CartName = request_cart.CartName,
                UserId = userId,
                RestaurantId = restaurantId
            };

            _dbContext.carts.Add(newCart);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                  _logger.LogError(ex, "Database update error while creating cart {CartName}", request_cart.CartName);

                  throw ;
            }

            // update cache key for cart
            await _cacheService.UpdateKeyVersionForCartPagiation(userId);
           
            _logger.LogInformation("Cart {CartName} created successfully for user {UserId}", request_cart.CartName, userId);
            return new ServiceResult<int>
            {
                Success = true,
                Message = "new cart create sucessfuly ",
                Data = newCart.CartId,
                StatusCode = 201
            };
        }

        // this method add item to a cart and then update the cache
        public async Task<ServiceResult<int>> AddItemToCartService(Cart cart, AddItemToCartDTO request_item)
        {
                var Item = await GetItemCache(request_item.ItemId);

            if (Item== null)
            {
               _logger.LogWarning("Item not found with id {ItemId}", request_item.ItemId);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "requested item not found",
                    StatusCode = 404
                };

            }
            var menu = await _dbContext.menu_category
            .AnyAsync(m => m.CategoryId == Item.MenuId && m.RestaurantId == cart.RestaurantId && !m.IsDeleted);

            if (!menu)
            {
                _logger.LogWarning("Item {ItemId} does not belong to restaurant {RestaurantId}", Item.ItemId, cart.RestaurantId);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "Item does not belong to this restaurant",
                    StatusCode = 404
                };
            }

            var cart_item = await _dbContext.cart_items.FirstOrDefaultAsync(c => c.CartId == cart.CartId && c.ItemId == request_item.ItemId);

            if (cart_item != null)
            {
                cart_item.Quantity += request_item.Quantity;
            }
            else
            {
                var new_cart_item = new CartItem
                {
                    Quantity = request_item.Quantity,
                    CartId = cart.CartId,
                    ItemId = request_item.ItemId
                };
                _dbContext.cart_items.Add(new_cart_item);
            }

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                  _logger.LogError(ex, "Database update error while adding item to cart {CartName}", cart.CartName);

                  throw ;
            }

            // update cache key for cart
            await _cacheService.UpdateKeyVersionForCartPagiation(cart.UserId);



            _logger.LogInformation("Item {ItemName} added to cart {CartName}", Item.ItemName, cart.CartName);
            return new ServiceResult<int>
            {
                Success = true,
                Message = $"{Item.ItemName}  is added to  {cart.CartName}  sucessfully",
                Data = Item.ItemId,
                StatusCode = 200
            };


        }
   
        // this method update item quantity in a cart and then update cache 
        public async Task<ServiceResult<int>> UpdateItemCartService(CartItem cartItem, string owner_id, UpdateCartItem dto)
        {

            if (dto.NewQuantity <= 0)
            {
                 _logger.LogWarning("Invalid quantity {Quantity} provided", dto.NewQuantity);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "quantity should be grater than 0",
                    StatusCode = 400
                };
            }
            cartItem.Quantity = dto.NewQuantity;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                  _logger.LogError(ex, "Database update error while updating item in a cart {ItemId}", cartItem.ItemId);

                  throw ;
            }

            // update cache key for cart
            await _cacheService.UpdateKeyVersionForCartPagiation(owner_id);

            _logger.LogInformation("Cart item {ItemId} quantity updated to {Quantity}", cartItem.ItemId, cartItem.Quantity);
            return new ServiceResult<int>
            {
                Success = true,
                Message = "item quantity is updated ",
                Data = cartItem.Quantity,
                StatusCode = 200
            };

        }



        public async Task<ServiceResult<string>> DeleteItemCartService(CartItem cartItem, string owner_id)
        {
            _dbContext.cart_items.Remove(cartItem);
            try
            {
              await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database update error while deleting cart item {ItemId}", cartItem.ItemId);

                  throw ;
                
            }
           
            _logger.LogInformation("Cart item {ItemId} quantity updated to {Quantity}", cartItem.ItemId, cartItem.Quantity);

            // update cache key for cart
            await _cacheService.UpdateKeyVersionForCartPagiation(owner_id);
            return new ServiceResult<string>
            {
                Success = true,
                Message = $"{cartItem.Item.ItemName} is removed sucessfully",
                StatusCode = 200
            };
        }


        public async Task<ServiceResult<PaginationResponse<GetCartItemDTO>>> ViewAllCartItemsService(PaginationParams pagination, CartFilter filter, string loged_user_id)
        {

            var query = _dbContext.carts.Where(c=> c.UserId== loged_user_id).AsNoTracking().AsQueryable();

            // key version 
            var version_string = $"Carts_Version_User{loged_user_id}";

            var version = await _redisCache.GetStringAsync(version_string) ?? "1";

            bool use_caching = pagination.PageNumber <= 3 &&
                         string.IsNullOrEmpty(filter.cartName) &&
                         !filter.RestaurantId.HasValue &&
                          !filter.cartId.HasValue &&
                         string.IsNullOrEmpty(filter.SortBy);


            string cacheKey = $"Carts_{version}" +
                        $"PageNumber{pagination.PageNumber}_" +
                        $"PageSize{pagination.PageSize}_" +
                        $"UserId{loged_user_id}";

            //try get from cache
            if (use_caching)
            {
                var CachedData = await GetItemFromCacheHelper(cacheKey);

                if (CachedData != null)
                {
                    _logger.LogDebug("Cache hit for carts with key {CacheKey}", cacheKey);
                    // return data with pagination info
                    return new ServiceResult<PaginationResponse<GetCartItemDTO>>
                    {
                        Success = true,
                        Data = new PaginationResponse<GetCartItemDTO>
                        {
                            pageSize = pagination.PageSize,
                            pageNumber = pagination.PageNumber,
                            totalCount = CachedData.totalCount,
                            Data = CachedData.Data
                        },
                        StatusCode = 200
                    };

                }
            }

            // aply fltering 
            // filter by cart id name o restaurant id  if have value
            query = ApplyFilter(query, filter);

            // get total data for show with pagination
            var totalData = await query.CountAsync();

            // prepare with pagination
            var CartItems = await query
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
             .Select(c => new GetCartItemDTO
             {
                 CartName = c.CartName,
                 RestaurantName = c.Restaurant.Restaurant_Name,
                 Username = c.User.UserName ?? "",
                 Items = c.CartItem
                 .Where(ci=> !ci.Item.IsDeleted &&  ci.Item.IsActive)
                 .Select(ci => new GetCartItemItemDTO
                 {
                     ItemName = ci.Item.ItemName,
                     ItemPrice = ci.Item.ItemPrice,
                     Quantity = ci.Quantity
                 }).ToList(),

                cartTotalPrice = (double)c.CartItem
                      .Where(ci => !ci.Item.IsDeleted && ci.Item.IsActive)
                      .Select(ci => ci.Item.ItemPrice * ci.Quantity)
                       .DefaultIfEmpty(0)
                        .Sum()

             }).ToListAsync();


            // 2. Store data form DB in both caches if nim price and max price and stor by  is null
            if (use_caching)
            {
                _memoryCache.Set(
                    cacheKey,
                   new PaginationResponse<GetCartItemDTO>
                   {
                       pageSize = pagination.PageSize,
                       pageNumber = pagination.PageNumber,
                       totalCount = totalData,
                       Data = CartItems
                   },
                  TimeSpan.FromMinutes(3));


                await _redisCache.SetStringAsync(
                 cacheKey,
                 JsonSerializer.Serialize(
                         new PaginationResponse<GetCartItemDTO>
                         {
                             pageSize = pagination.PageSize,
                             pageNumber = pagination.PageNumber,
                             totalCount = totalData,
                             Data = CartItems
                         }
                 ),
                 new DistributedCacheEntryOptions
                 {
                     AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                 }
                 );
               _logger.LogDebug("Cart data cached with key {CacheKey}", cacheKey);
            }


            _logger.LogInformation("Cart items retrieved from database for user {UserId}", loged_user_id);

            return new ServiceResult<PaginationResponse<GetCartItemDTO>>
            {
                Success = true,
                Data = new PaginationResponse<GetCartItemDTO>
                {
                    pageSize = pagination.PageSize,
                    pageNumber = pagination.PageNumber,
                    totalCount = totalData,
                    Data = CartItems
                },
                StatusCode = 200
            };



        }

        public async Task<(Cart? , CartAuthorizationDTO?)> GetCartEntityAndAuth(int cart_id)
        {
            var cart= await _dbContext.carts.Where(c => c.CartId == cart_id)
            .Select(c=> new
            {
                cart=c,
                auth=new CartAuthorizationDTO
            {
                cartId = c.CartId,
                ownerId = c.UserId
            }
            }).FirstOrDefaultAsync();

            if(cart == null)
                 return(null , null);

        return (cart.cart,cart.auth); 
        }

        private async Task<GetRestaurantCacheDTO?> GetRestaurantCache(int restaurant_id)
        {

            string versionString = $"Restaurant_Version_{restaurant_id}";
            var version = await _redisCache.GetStringAsync(versionString) ?? "1";

            string Key = $"Restaurant_{version}_{restaurant_id}";

            if (_memoryCache.TryGetValue(Key, out GetRestaurantCacheDTO? checkRestaurantMemory))
            {
                _logger.LogDebug("restaurant {restaurant_id} retrieved from cache", restaurant_id);
                return checkRestaurantMemory;

            }
            // 1. Try get from cache
            var cachedData = await _redisCache.GetStringAsync(Key);

            if (cachedData != null)
            {
                var checkRestaurantRedis = JsonSerializer.Deserialize<GetRestaurantCacheDTO?>(cachedData);

                _logger.LogDebug("restaurant {restaurant_id} retrieved from Redis cache", restaurant_id);

                // add data from redis to memory cache for faster access next time
                _memoryCache.Set(Key, checkRestaurantRedis, TimeSpan.FromMinutes(5));
                _logger.LogDebug("restaurant {restaurant_id} stored in CACHE", restaurant_id);

                return checkRestaurantRedis;
            }

            var DbRestaurant = await _dbContext.restaurants
             .Where(r => r.RestaurantId == restaurant_id && !r.IsDeleted && r.RestaurantStatus == RestaurantStatuss.Accepted)
             .Select(r => new GetRestaurantCacheDTO
             {
                 restaurantName = r.Restaurant_Name,
                 RestaurantId = r.RestaurantId,
                 ownerId = r.UserId
             }).FirstOrDefaultAsync();

            // // 2. Store in both caches
            _memoryCache.Set(Key, DbRestaurant, TimeSpan.FromMinutes(3));
            _logger.LogDebug("restaurant {restaurant_id} stored in CACHE", restaurant_id);

            await _redisCache.SetStringAsync(
             Key,
             JsonSerializer.Serialize(DbRestaurant),
             new DistributedCacheEntryOptions
             {
                 AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
             }
             );
            _logger.LogDebug("restaurant {restaurant_id} stored in Redis & in memory cache", restaurant_id);

            return DbRestaurant;
        }

        // this method gets item with specific item id  from cache if not exits form DB and then readd to cache
        public async Task<GetItemDTO?> GetItemCache(int item_id)
        {
               string versionString = $"Item_Version_{item_id}";
            var version = await _redisCache.GetStringAsync(versionString) ?? "1";
           string cacheKey = $"Item_{version}_{item_id}";

            if (_memoryCache.TryGetValue(cacheKey, out GetItemDTO? item_memory))
            {
                _logger.LogDebug("Item {ItemId} retrieved from cache", item_id);
                return item_memory;

            }
            // 1. Try get from cache
            var cachedData = await _redisCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                var redis_item = JsonSerializer.Deserialize<GetItemDTO>(cachedData);

                _logger.LogDebug("Item {ItemId} retrieved from Redis cache", item_id);

                if (redis_item != null)
                {
                    // add data from redis to memory cache for faster access next time
                    _memoryCache.Set(cacheKey, redis_item, TimeSpan.FromMinutes(5));
                    _logger.LogDebug("Item {ItemId} stored in CACHE", item_id);

                    return redis_item;
                }
            }

            var itemDb = await _dbContext.items
              .Where(i => i.ItemId == item_id && !i.IsDeleted)
              .Select(i => new GetItemDTO
              {
                  ItemId = i.ItemId,
                  ItemName = i.ItemName,
                  ItemPrice = i.ItemPrice,
                  MenuId = i.MenuCategoryId,
                  RestaurantId = i.MenuCategory.restaurant.RestaurantId,
                  UserId = i.MenuCategory.restaurant.UserId,
                  IsDeleted = i.IsDeleted,
                  IsAvailable = i.IsActive
              })
                .FirstOrDefaultAsync();


            if (itemDb != null)
            {
                //   2. Store in both caches
                _memoryCache.Set(cacheKey, itemDb, TimeSpan.FromMinutes(3));
                _logger.LogDebug("Item {ItemId} stored in CACHE", item_id);

                await _redisCache.SetStringAsync(
                 cacheKey,
                 JsonSerializer.Serialize(itemDb),
                 new DistributedCacheEntryOptions
                 {
                     AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                 }
                 );
                _logger.LogDebug("Item {ItemId} stored in Redis & in memory cache", item_id);

            }
            return itemDb;
        }



        public async Task<(CartItem? , CartAuthorizationDTO?)> GetCartItemEntityAndAuth(int cart_item_id)
        {
            var cartItem= await _dbContext.cart_items
               .Where(ci => ci.CartItemId == cart_item_id)
               .Select(ci=> new
               {
                  cart_item=ci,
                  auth=new CartAuthorizationDTO
            {
                cartId = ci.CartId,
                ownerId = ci.Cart.UserId
            } 
               }).FirstOrDefaultAsync();

            if(cartItem == null)
                  return (null,null);
            
        return(cartItem.cart_item,cartItem.auth);

        }


        // this method gets list of items form cache if not exist return null and used in GetAllItemsService (used to organize and clear code)
        private async Task<PaginationResponse<GetCartItemDTO>?> GetItemFromCacheHelper(string cacheKey)
        {
            if (_memoryCache.TryGetValue(cacheKey, out PaginationResponse<GetCartItemDTO>? item_memory))
            {
                _logger.LogDebug("Item  retrieved from cache with key  {cachekey }", cacheKey);
                return item_memory;
            }
            // 1. Try get from cache
            var cachedData = await _redisCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                var redis_item = JsonSerializer.Deserialize<PaginationResponse<GetCartItemDTO>?>(cachedData);

                _logger.LogDebug("Item retrieved from Redis cache with key {cachekey}", cacheKey);

                if (redis_item != null)
                {
                    // add data from redis to memory cache for faster access next time
                    _memoryCache.Set(cacheKey, redis_item, TimeSpan.FromMinutes(5));
                    _logger.LogDebug("Item  stored in CACHE with key {cacheKey}", cacheKey);

                    return redis_item;
                }
            }
            return null;
        }


        // this method used to apply filtering for GetAllItemsService  method
        private IQueryable<Cart> ApplyFilter(IQueryable<Cart> query, CartFilter filter)
        {
            if (filter.cartId.HasValue)
            {
                query = query.Where(i => i.CartId >= filter.cartId.Value);
                _logger.LogDebug("Applied get by cart id filter: {cartId}", filter.cartId.Value);
            }
            if (filter.RestaurantId.HasValue)
            {
                query = query.Where(i => i.RestaurantId == filter.RestaurantId.Value);
                _logger.LogDebug("Applied get by restaurant id filter: {RestaurantId}", filter.RestaurantId.Value);
            }
            if (!string.IsNullOrEmpty(filter.cartName))
            {
                query = query.Where(i => i.CartName.Contains(filter.cartName));
                _logger.LogDebug("Applied get by cart name  filter: {cartName}", filter.cartName);
            }
            if (!string.IsNullOrEmpty(filter.SortBy))
            {
                query = ApplySorting(query, filter.SortBy, filter.FromLowToHigh);
                _logger.LogDebug("Applied sorting - SortBy: {SortBy}, FromLowToHigh: {FromLowToHigh}", filter.SortBy, filter.FromLowToHigh);
            }
            else
            {
                // default sorting by ID
                query = query.OrderByDescending(i => i.CartId);
                _logger.LogDebug("Applied default sorting by CartId in decending order");
            }
            return query;
        }

        // this method used to apply sorting for GetAllItemsService method 
        private IQueryable<Cart> ApplySorting(IQueryable<Cart> query, string sort_by, bool? from_low_to_high)
        {
            bool ascending = from_low_to_high.HasValue ? from_low_to_high.Value : true; // default to ascending if not specified

            switch (sort_by.ToLower())
            {
                case "restaurantid":
                    query = ascending ? query.OrderBy(i => i.RestaurantId) : query.OrderByDescending(i => i.RestaurantId);
                    _logger.LogDebug("Sorting by restaurant id in {Order} order", ascending ? "ascending" : "descending");
                    break;
                case "cartname":
                    query = ascending ? query.OrderBy(i => i.CartName) : query.OrderByDescending(i => i.CartName);
                    _logger.LogDebug("Sorting by name in {Order} order", ascending ? "ascending" : "descending");
                    break;

                case "cartid":
                default:
                    // Default sorting if sort_by value is unrecognized
                    query = ascending ? query.OrderBy(i => i.CartId) : query.OrderByDescending(i => i.CartId);
                    _logger.LogDebug("Sorting by ID in {Order} order (default)", ascending ? "ascending" : "descending");
                    break;
            }

            return query;
        }

        private async Task<bool> CheckCartExist(string UserId, int Restaurant_id)
        {
            return await _dbContext.carts
                    .AnyAsync(c => c.UserId == UserId && c.RestaurantId == Restaurant_id);


        }
    }
}