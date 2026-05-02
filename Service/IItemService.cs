using System;
using System.Text.Json;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Exceptions;
using food_order_system1.Flters;
using food_order_system1.Modles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using static food_order_system1.Controllers.OrderController;

namespace food_order_system1.Service
{
    public interface IItemService
    {
        Task<ServiceResult<int>> CreateItemService(int MenuCategoryId, CreateItemDTO request_item);
        Task<ServiceResult<int>> UpdateItemService(Item item, UpdateItemDTO dto);
        Task<ServiceResult<bool>> ActivateDeactivateItemService(Item item);
        Task<ServiceResult<bool>> DeleteItemService(Item item);
        Task<ServiceResult<PaginationResponse<GetItemDTO>>> GetAllItemsService(PaginationParams pagination, ItemFilter filter, string user_id, UserRolee role);
       // Task<GetItemDTO?> GetItemCache(int item_id);
        Task<(Item? , RestaurantAuthorizationDTO?)> GetItemEntityAndAuth(int item_id);
      //  Task<GetMenuDTO?> GetMenuCache(int menu_category_id);
        Task<(MenuCategory? , RestaurantAuthorizationDTO?)> GetMenuEntityAndAuth(int menu_category_id);

    }
    public class ItemService : IItemService
    {
        private readonly AppUser _dbContext;
        private readonly ILogger<ItemService> _logger;

        private readonly IMemoryCache _memoryCache;

        private readonly IDistributedCache _redisCache;

        private readonly IcacheService _cacheService;



        public ItemService(AppUser context, ILogger<ItemService> logger, IMemoryCache memoryCache, IDistributedCache redisCache, IcacheService cacheService)
        {
            _dbContext = context;
            _logger = logger;
            _memoryCache = memoryCache;
            _redisCache = redisCache;
            _cacheService = cacheService;

        }

        //  this method create new item and invalidate caches if name don't exit befor and 
        public async Task<ServiceResult<int>> CreateItemService(int MenuCategoryId, CreateItemDTO request_item)
        {

            var item_is_exist = await _dbContext.items.AnyAsync(m => m.ItemName == request_item.ItemName && m.MenuCategoryId == MenuCategoryId);

            if (item_is_exist)
            {
                _logger.LogWarning("Item with name {ItemName} already exists in menu category {MenuCategoryId}", request_item.ItemName, MenuCategoryId);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "Item already exists in this menu category",
                    StatusCode = 400
                };
            }

            if (request_item.ItemPrice <= 0)
            {
                _logger.LogWarning("Invalid item price {ItemPrice} for item {ItemName} in menu category {MenuCategoryId}", request_item.ItemPrice, request_item.ItemName, MenuCategoryId);
               return new ServiceResult<int>
                    {
                      Success = false,
                      Message = "Price must be greater than 0",
                      StatusCode = 400
                    };
            }
            var new_item = new Item
            {
                ItemName = request_item.ItemName,
                ItemPrice = request_item.ItemPrice,
                MenuCategoryId = MenuCategoryId,
            };

            _dbContext.items.Add(new_item);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database update error while creating item {ItemName}", request_item.ItemName);

                     throw ;
            }

            // clear cache 
            await _cacheService.UpdateKeyVersionForMenu(MenuCategoryId);

            // update cache key version for item pagination

            int? res_id = await _dbContext.menu_category
           .Where(m => m.CategoryId == MenuCategoryId)
           .Select(m =>
           m.RestaurantId)
           .FirstOrDefaultAsync();

            await _cacheService.UpdateKeyVersionForItemPagiation(res_id);


            _logger.LogInformation("Item created with ID {ItemId} in menu category {MenuCategoryId} by user ", new_item.ItemId, MenuCategoryId);
            return new ServiceResult<int>
            {
                Success = true,
                Message = "new item created",
                Data = new_item.ItemId,
                StatusCode = 201
            };
        }

        // this method change active/deactive item and invalidated caches
        public async Task<ServiceResult<bool>> ActivateDeactivateItemService(Item item)
        {

            item.IsActive = !item.IsActive;

         try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Database update error while activating/deactivating item {ItemName}", item.ItemName);
                     throw ;
            }
            // remover menu in chache
            await _cacheService.UpdateKeyVersionForMenu(item.MenuCategoryId);

            // remve Item in cache
            await _cacheService.UpdateKeyVersionForItem(item.ItemId);


            // upfdate  cache key version for item pages
            int? res_id = await _dbContext.items
          .Where(i => i.ItemId == item.ItemId)
          .Select(i =>
          i.MenuCategory.RestaurantId)
          .FirstOrDefaultAsync();

            await _cacheService.UpdateKeyVersionForItemPagiation(res_id);

            var getUserForCart = await _dbContext.cart_items.Where(c => c.ItemId == item.ItemId)
            .Select(i => i.Cart.UserId)
            .FirstOrDefaultAsync();
            if (getUserForCart != null)
            {
                // update cache key for cart
                await _cacheService.UpdateKeyVersionForCartPagiation(getUserForCart);
            }




            return new ServiceResult<bool>
            {
                Success = true,
                Message = $"Item is now {(item.IsActive ? "active" : "inactive")}",
                Data = true,
                StatusCode = 200
            };
        }

        // this method delete item and invalidate caches
        public async Task<ServiceResult<bool>> DeleteItemService(Item item)
        {

            item.IsDeleted = true;
            item.IsActive = false;

            // delete item form cart
            var cartItems = await _dbContext.cart_items.Where(i => i.ItemId == item.ItemId).ToListAsync();
            _dbContext.cart_items.RemoveRange(cartItems);
            if (cartItems.Any())
              try
            {
              await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Database update error while activating/deactivating item {ItemName}", item.ItemName);
                     throw ;
            }
            // remover menu in chache
            await _cacheService.UpdateKeyVersionForMenu(item.MenuCategoryId);

            // remve Item in cache
            await _cacheService.UpdateKeyVersionForItem(item.ItemId);

            // update chache key version for item pages
            int? res_id = await _dbContext.items
            .Where(i => i.ItemId == item.ItemId)
            .Select(i =>
            i.MenuCategory.RestaurantId)
            .FirstOrDefaultAsync();

            await _cacheService.UpdateKeyVersionForItemPagiation(res_id);

            var getUserForCart = await _dbContext.cart_items.Where(c => c.ItemId == item.ItemId)
          .Select(i => i.Cart.UserId)
          .FirstOrDefaultAsync();
            if (getUserForCart != null)
            {
                // update cache key for cart
                await _cacheService.UpdateKeyVersionForCartPagiation(getUserForCart);
            }

            return new ServiceResult<bool>
            {
                Success = true,
                Message = "item deleted sucessfully",
                Data = true,
                StatusCode = 200
            };
        }


        // this ethod upfate item name and price if name don't exit and price not equal zero and then invalidate caches
        public async Task<ServiceResult<int>> UpdateItemService(Item item, UpdateItemDTO dto)
        {

            if (string.IsNullOrWhiteSpace(dto.ItemName) && dto.ItemPrice <= 0)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "nothing to update",
                    StatusCode = 400
                };

            if (!string.IsNullOrWhiteSpace(dto.ItemName))
            {
                var nameExists = await _dbContext.items
                    .AnyAsync(i => i.ItemName == dto.ItemName
                                && i.MenuCategoryId == item.MenuCategoryId
                                && i.ItemId != item.ItemId);


                if (nameExists)
                    return new ServiceResult<int>
                    {
                        Success = false,
                        Message = "item name already exist",
                        StatusCode = 400
                    };

                item.ItemName = dto.ItemName;
            }

            if (dto.ItemPrice > 0)
            {
                item.ItemPrice = dto.ItemPrice;
            }
            else
            {
                  return new ServiceResult<int>
                    {
                        Success = false,
                        Message = "pice must be grater than 0",
                        StatusCode = 400
                    };
            }


                try
            {
              await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Database update error while updating item {ItemName}", item.ItemName);
                     throw ;
            }
            // remover menu in chache
            await _cacheService.UpdateKeyVersionForMenu(item.MenuCategoryId);
            // remve Item in cache
            await _cacheService.UpdateKeyVersionForItem(item.ItemId);

            // cleare chache key version for item pages 
            int? res_id = await _dbContext.items
            .Where(i => i.ItemId == item.ItemId)
            .Select(i =>
            i.MenuCategory.RestaurantId)
            .FirstOrDefaultAsync();

            await _cacheService.UpdateKeyVersionForItemPagiation(res_id);

            var getUserForCart = await _dbContext.cart_items.Where(c => c.ItemId == item.ItemId)
        .Select(i => i.Cart.UserId)
        .FirstOrDefaultAsync();
            if (getUserForCart != null)
            {
                // update cache key for cart
                await _cacheService.UpdateKeyVersionForCartPagiation(getUserForCart);
            }


            return new ServiceResult<int>
            {
                Success = true,
                Message = "Item updated successfully",
                Data = item.ItemId,
                StatusCode = 200
            };
        }


        // this method gets list of items for one restaurant or for all restaurants with filtering , sorting and pagination 
        // then set item pages (1-2-3) to a cache 
        public async Task<ServiceResult<PaginationResponse<GetItemDTO>>> GetAllItemsService(PaginationParams pagination, ItemFilter filter, string user_id, UserRolee role)
        {


            string user_role;

            if (role == UserRolee.RestaurantManager)
            {
                var restaurant_id = await _dbContext.restaurants.Where(r => r.UserId == user_id).Select(r => r.RestaurantId).FirstOrDefaultAsync();
                filter.RestaurantId = restaurant_id;
                user_role = "RestaurantManager";
            }
            else
            {
                user_role = "Admin&Customer";
            }
            var version_string = filter.RestaurantId.HasValue ? $"Items_Version_Restaurant_{filter.RestaurantId}" : "Items_Version_Global";

            var version = await _redisCache.GetStringAsync(version_string) ?? "1";




            bool use_caching = pagination.PageNumber <= 3 &&
                            !filter.MinPrice.HasValue &&
                            !filter.MaxPrice.HasValue &&
                            string.IsNullOrEmpty(filter.SortBy);


            string cacheKey = $"Items_{version}" +
                        $"PageNumber{pagination.PageNumber}_" +
                        $"PageSize{pagination.PageSize}_" +
                        $"RestaurantId{filter.RestaurantId}_" +
                        $"role{user_role}";

            //try get from cache
            if (use_caching)
            {
                var CachedData = await GetItemFromCacheHelper(cacheKey);

                if (CachedData != null)
                {
                    _logger.LogInformation("Items retrieved from cache with key {cachekey}", cacheKey);
                    // return data with pagination info
                    return new ServiceResult<PaginationResponse<GetItemDTO>>
                    {
                        Success = true,
                        Data = new PaginationResponse<GetItemDTO>
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

            // get from db
            var query = _dbContext.items
                .Where(i => !i.IsDeleted && i.IsActive)
                .AsNoTracking()
                .AsQueryable();

            // if the user is restaurant manager he will see only his restaurant items but if he is admin or customer he will see all items

            if (role == UserRolee.RestaurantManager)
            {
                var restaurant = await GetRestaurant(user_id);
                if(restaurant == null)
                {
                  _logger.LogInformation("restaurant not ofund for user {user_id}",user_id);
                  return new ServiceResult<PaginationResponse<GetItemDTO>>
                  {
                      Success = false,
                      Data =null,
                      Message="restaurant not ofund for user",
                      StatusCode = 404
                  };
                }
                query = query.Where(i => i.MenuCategory.RestaurantId == restaurant.RestaurantId);
            }


            // filter by price if min or max price has value
            query = ApplyFilter(query, filter, role);

            // get total count of data after filtering and before pagination
            var totaldata = await query.CountAsync();
            _logger.LogInformation("Total items count after filtering: {TotalCount}", totaldata);

            // apply pagination
            var items = await query
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .Select(i => new GetItemDTO
                    {
                        ItemId = i.ItemId,
                        ItemName = i.ItemName,
                        ItemPrice = i.ItemPrice,
                        IsDeleted = i.IsDeleted,
                        IsAvailable = i.IsActive
                    })
                    .ToListAsync();


            // 2. Store data form DB in both caches if nim price and max price and stor by  is null
            if (use_caching)
            {
                _memoryCache.Set(
                    cacheKey,
                   new PaginationResponse<GetItemDTO>
                   {
                       pageSize = pagination.PageSize,
                       pageNumber = pagination.PageNumber,
                       totalCount = totaldata,
                       Data = items
                   },
                  TimeSpan.FromMinutes(3));


                await _redisCache.SetStringAsync(
                 cacheKey,
                 JsonSerializer.Serialize(
                         new PaginationResponse<GetItemDTO>
                         {
                             pageSize = pagination.PageSize,
                             pageNumber = pagination.PageNumber,
                             totalCount = totaldata,
                             Data = items
                         }
                 ),
                 new DistributedCacheEntryOptions
                 {
                     AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                 }
                 );
                _logger.LogInformation("items stored in Redis & in memory cache with key {cachekey}", cacheKey);
            }




            _logger.LogInformation("Items retrieved from database with pagination - PageNumber: {PageNumber}, PageSize: {PageSize}, TotalCount: {TotalCount}", pagination.PageNumber, pagination.PageSize, totaldata);
            // return data with pagination info
            return new ServiceResult<PaginationResponse<GetItemDTO>>
            {
                Success = true,
                Data = new PaginationResponse<GetItemDTO>
                {
                    pageSize = pagination.PageSize,
                    pageNumber = pagination.PageNumber,
                    totalCount = totaldata,
                    Data = items
                },
                StatusCode = 200
            };

            

        }


        // this method return item and authorization object form DB with specific item id 
        public async Task<(Item? , RestaurantAuthorizationDTO?)> GetItemEntityAndAuth(int item_id)
        {
            var item=await _dbContext.items
               .Where(i => i.ItemId == item_id && !i.IsDeleted && i.IsActive )
               .Select(i=>new
               {
                item=i,
                auth=new RestaurantAuthorizationDTO
                {
                    RestaurantId= i.MenuCategory.restaurant.RestaurantId,
                ownerId = i.MenuCategory.restaurant != null ? i.MenuCategory.restaurant.UserId : string.Empty
                }}).FirstOrDefaultAsync();

                if(item == null)
                    return (null,null);
             
             return(item.item,item.auth);
                   
                   
               }
           



      

        // this method gets menu and authorization object with specific id from DB
        public async Task<(MenuCategory? ,RestaurantAuthorizationDTO?)> GetMenuEntityAndAuth(int menu_category_id)
        {
            var menu= await _dbContext.menu_category
               .Where(m => m.CategoryId == menu_category_id)
               .Select(m=> new
               {
                   menu=m,
                   auth=new RestaurantAuthorizationDTO
               {
                RestaurantId = m.restaurant.RestaurantId,
                ownerId = m.restaurant != null ? m.restaurant.UserId : string.Empty
               }}) .FirstOrDefaultAsync();

            if(menu == null)
            {
                return (null, null);
            }
        return (menu.menu, menu.auth);


        }

        // this method gets list of items form cache if not exist return null and used in GetAllItemsService (used to organize and clear code)
        private async Task<PaginationResponse<GetItemDTO>?> GetItemFromCacheHelper(string cacheKey)
        {
            if (_memoryCache.TryGetValue(cacheKey, out PaginationResponse<GetItemDTO>? item_memory))
            {
                _logger.LogDebug("Item  retrieved from cache with key  {cachekey}", cacheKey);
                return item_memory;
            }
            // 1. Try get from cache
            var cachedData = await _redisCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                var redis_item = JsonSerializer.Deserialize<PaginationResponse<GetItemDTO>?>(cachedData);

                _logger.LogDebug("Item retrieved from Redis cache with key {cachekey}", cacheKey);

                if (redis_item != null)
                {
                    // add data from redis to memory cache for faster access next time
                    _memoryCache.Set(cacheKey, redis_item, TimeSpan.FromMinutes(5));
                    _logger.LogDebug("Items stored in Redis & Memory Cache with key {cacheKey}", cacheKey);

                    return redis_item;
                }
            }
            return null;
        }

        // this method used to apply filtering for GetAllItemsService  method
        private IQueryable<Item> ApplyFilter(IQueryable<Item> query, ItemFilter filter, UserRolee role)
        {

            if (filter.RestaurantId.HasValue && role != UserRolee.RestaurantManager)
            {
                query = query.Where(i => i.MenuCategory.RestaurantId == filter.RestaurantId.Value);
                _logger.LogDebug("Applied  restaurant id filter: {RestaurantId}", filter.RestaurantId.Value);
            }
            if (filter.MinPrice.HasValue)
            {
                query = query.Where(i => i.ItemPrice >= filter.MinPrice.Value);
                _logger.LogDebug("Applied minimum price filter: {MinPrice}", filter.MinPrice.Value);
            }
            if (filter.MaxPrice.HasValue)
            {
                query = query.Where(i => i.ItemPrice <= filter.MaxPrice.Value);
                _logger.LogDebug("Applied maximum price filter: {MaxPrice}", filter.MaxPrice.Value);
            }
            if (!string.IsNullOrEmpty(filter.SortBy))
            {
                query = ApplySorting(query, filter.SortBy, filter.FromLowToHigh);
                _logger.LogDebug("Applied sorting - SortBy: {SortBy}, FromLowToHigh: {FromLowToHigh}", filter.SortBy, filter.FromLowToHigh);
            }
            else
            {
                // default sorting by ID
                query = query.OrderByDescending(i => i.ItemId);
                _logger.LogDebug("Applied default sorting by ItemId in decesending order");
            }
            return query;
        }

        // this method used to apply sorting for GetAllItemsService method 
        private IQueryable<Item> ApplySorting(IQueryable<Item> query, string sort_by, bool? from_low_to_high)
        {
            bool ascending = from_low_to_high.HasValue ? from_low_to_high.Value : true; // default to ascending if not specified

            switch (sort_by.ToLower())
            {
                case "price":
                    query = ascending ? query.OrderBy(i => i.ItemPrice) : query.OrderByDescending(i => i.ItemPrice);
                    _logger.LogDebug("Sorting by price in {Order} order", ascending ? "ascending" : "descending");
                    break;
                case "name":
                    query = ascending ? query.OrderBy(i => i.ItemName) : query.OrderByDescending(i => i.ItemName);
                    _logger.LogDebug("Sorting by name in {Order} order", ascending ? "ascending" : "descending");
                    break;

                case "id":
                default:
                    // Default sorting if sort_by value is unrecognized
                    query = ascending ? query.OrderBy(i => i.ItemId) : query.OrderByDescending(i => i.ItemId);
                    _logger.LogDebug("Sorting by ID in {Order} order (default)", ascending ? "ascending" : "descending");
                    break;
            }

            return query;
        }
               private async Task<Restaurant?> GetRestaurant(string user_id)
        {
            var restaurant = await _dbContext.restaurants
             .FirstOrDefaultAsync(r => r.UserId == user_id&& !r.IsDeleted &&  r.RestaurantStatus == RestaurantStatuss.Accepted);

            return restaurant;
        }
    }
}