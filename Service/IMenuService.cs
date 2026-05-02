using System;
using System.Text.Json;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Flters;
using food_order_system1.Modles;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using static food_order_system1.Controllers.OrderController;

namespace food_order_system1.Service
{
    public interface IMenuService
    {
        Task<ServiceResult<int>> CreaetMenuService( CreateMenuDTO request_menu , string userId);
        Task<ServiceResult<int>> UpdateMenuService(MenuCategory menu, UpdateMenuCategoryDTO dto);
        Task<ServiceResult<bool>> DeleteMenuService(MenuCategory menu);
        Task<ServiceResult<PaginationResponse<GetMenuDTO>>> GetAllMenusService(PaginationParams pagination, MenuFilter filter, string user_id, UserRolee role);
      //   Task<GetRestaurantCacheDTO?> GetRestaurantCache(string userid);
        Task<(MenuCategory?, RestaurantAuthorizationDTO?)> GetMenuEntityAndAuth(int menu_category_id);




    }

    public class MenuService : IMenuService
    {
        private readonly AppUser _dbContext;

        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _redisCache;
        private readonly ILogger<MenuService> _logger;

        private readonly IcacheService _cacheService;

        public MenuService(AppUser context,ILogger<MenuService> logger, IMemoryCache memoryCache, IDistributedCache redisCache, IcacheService CacheService)
        {
            _dbContext = context;
            _memoryCache = memoryCache;
            _redisCache = redisCache;
            _logger = logger;
            _cacheService = CacheService;
        }

        public async Task<ServiceResult<int>> CreaetMenuService(CreateMenuDTO request_menu ,string userId)
        { 
               var restaurant = await GetRestaurantCache(userId);
            if (restaurant == null)
            {
_logger.LogWarning("Restaurant not found for user {UserId}", userId);                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "restaurant not found for user",
                    StatusCode = 404,
                };
            }
            var name_exist = await _dbContext.menu_category
               .AnyAsync(m => m.CategoryName == request_menu.CategoryName && m.RestaurantId == restaurant.RestaurantId);

            if (name_exist)
            {
_logger.LogWarning("Menu category {CategoryName} already exists for this restaurant", request_menu.CategoryName);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "Category name already exists for this restaurant",
                    StatusCode = 400
                };
            }
            var new_menu = new MenuCategory
            {
                CategoryName = request_menu.CategoryName,
                RestaurantId = restaurant.RestaurantId,
            };
            _dbContext.menu_category.Add(new_menu);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Database update error while creating menu {MenuName}", new_menu.CategoryName);
                throw;
            }

            // update version for menu pagination
            await _cacheService.UpdateKeyVersionForMenuPagination(restaurant.RestaurantId);


_logger.LogInformation("New menu created {MenuName}", new_menu.CategoryName);
            return new ServiceResult<int>
            {
                Success = true,
                Message = $"{request_menu.CategoryName}    is create ",
                Data = new_menu.CategoryId,
                StatusCode = 201
            };

        }

        public async Task<ServiceResult<bool>> DeleteMenuService(MenuCategory menu)
        {
        if (menu.IsDeleted)
            {
             return new ServiceResult<bool>
              {
                Success = true,
                Message = "Menu already deleted",
                StatusCode = 200,
                Data=false
              };   
            }

          
                menu.IsDeleted = !menu.IsDeleted;
          

            var items = await _dbContext.items
            .Where(i => i.MenuCategoryId == menu.CategoryId)
            .ExecuteUpdateAsync(i =>
            i.SetProperty(c => c.IsDeleted, true)
            .SetProperty(c => c.IsActive, false));

            // update key cache for menu 
            await _cacheService.UpdateKeyVersionForMenu(menu.CategoryId);
            // update key version for menu paginaton
            await _cacheService.UpdateKeyVersionForMenuPagination(menu.RestaurantId);


            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Database update error while deleting menu {MenuName}", menu.CategoryName);
                throw;
            }

_logger.LogInformation("Menu category {CategoryName} deleted successfully", menu.CategoryName);
            return new ServiceResult<bool>
            {
                Success = true,
                Message = "Menu category deleted sucess fully",
                Data = true,
                StatusCode = 200
            };
        }



        public async Task<ServiceResult<int>> UpdateMenuService(MenuCategory menu, UpdateMenuCategoryDTO dto)
        {

            if (!string.IsNullOrWhiteSpace(dto.CategoryName))
            {
                var nameExists = await _dbContext.menu_category
                    .AnyAsync(r => r.CategoryName == dto.CategoryName
                                && r.RestaurantId == menu.RestaurantId
                                && r.CategoryId != menu.CategoryId);


                if (nameExists || dto.CategoryName == menu.CategoryName)
                {
_logger.LogWarning("Menu category name {CategoryName} already exists", dto.CategoryName);
                    return new ServiceResult<int>
                    {
                        Success = false,
                        Message = "menu category name already exist",
                        StatusCode = 400
                    };
                }
                menu.CategoryName = dto.CategoryName;
            }
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Database update failed while updating menu {CategoryName}", dto.CategoryName);
                throw;
            }
            // update key cache  for menu
            await _cacheService.UpdateKeyVersionForMenu(menu.CategoryId);
            // update key version for menu paginaton
            await _cacheService.UpdateKeyVersionForMenuPagination(menu.RestaurantId);
            _logger.LogInformation("Menu category updated successfully {CategoryName}", dto.CategoryName);
            return new ServiceResult<int>
            {
                Success = true,
                Message = "Menu category updated successfully",
                Data = menu.CategoryId,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<PaginationResponse<GetMenuDTO>>> GetAllMenusService(PaginationParams pagination, MenuFilter filter, string user_id, UserRolee role)
        {

            if (role == UserRolee.RestaurantManager)
            {
                var restaurant = await GetRestaurantCache(user_id);
                if (restaurant == null)
                { 
_logger.LogWarning("Restaurant not found for user {UserId}", user_id);
                    return new ServiceResult<PaginationResponse<GetMenuDTO>>
                    {
                        Success = false,
                        Message = "Restaurant not found",
                        StatusCode = 404
                    };
                }

                filter.RestaurantId = restaurant.RestaurantId;
            }

            bool use_caching = pagination.PageNumber <= 3 &&
                            pagination.PageSize == 10 &&
                           !filter.MenuId.HasValue &&
                           string.IsNullOrEmpty(filter.MenuName) &&
                           string.IsNullOrEmpty(filter.SortBy) &&
                           filter.RestaurantId.HasValue;

            var version_string = $"Menus_Version_Restaurant_{filter.RestaurantId}";
            var version = await _redisCache.GetStringAsync(version_string) ?? "1";

            string cacheKey = $"Menus_{version}" +
                $"PageNumber{pagination.PageNumber}_" +
                $"PageSize{pagination.PageSize}_" +
                $"RestaurantId{filter.RestaurantId}_";
            if (use_caching)
            {
                var CachedData = await GetItemFromCacheHelper(cacheKey);

                if (CachedData != null)
                {
_logger.LogDebug("Menus retrieved from cache with key {CacheKey}", cacheKey);
                    // return data with pagination info
                    return new ServiceResult<PaginationResponse<GetMenuDTO>>
                    {
                        Success = true,
                        Data = new PaginationResponse<GetMenuDTO>
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

            var query = _dbContext.menu_category
                   .Where(m => !m.IsDeleted)
                   .AsNoTracking()
                   .AsQueryable();

            // filter by price if min or max price has value
            query = ApplyFilter(query, filter, role);

            // get total count of data after filtering and before pagination
            var totaldata = await query.CountAsync();
            _logger.LogInformation("Total Menus count after filtering: {TotalCount}", totaldata);

            // apply pagination
            var menus = await query
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .Select(m => new GetMenuDTO
                    {
                        CategoryName = m.CategoryName,
                        MenuCategoryId = m.CategoryId,
                        IsActive = !m.IsDeleted,
                        RestaurantId = m.RestaurantId,
                        UserId = m.restaurant.UserId,
                        Items = m.menu_category_items
                        .Where(i => !i.IsDeleted && i.IsActive)
                        .Select(i => new GetItemDTO
                        {
                            ItemId = i.ItemId,
                            ItemName = i.ItemName,
                            ItemPrice = i.ItemPrice,
                            IsDeleted = i.IsDeleted,
                            IsAvailable = i.IsActive
                        }).ToList()
                    })
                    .ToListAsync();


            // 2. Store data form DB in both caches if nim price and max price and stor by  is null
            if (use_caching)
            {
                _memoryCache.Set(
                   cacheKey,
                   new PaginationResponse<GetMenuDTO>
                   {
                       pageSize = pagination.PageSize,
                       pageNumber = pagination.PageNumber,
                       totalCount = totaldata,
                       Data = menus
                   },
                   TimeSpan.FromMinutes(3));


                await _redisCache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(
                    new PaginationResponse<GetMenuDTO>
                    {
                        pageSize = pagination.PageSize,
                        pageNumber = pagination.PageNumber,
                        totalCount = totaldata,
                        Data = menus
                    }
            ),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            }
            );
               _logger.LogDebug("Menus stored in Redis & Memory cache with key {CacheKey}", cacheKey);
            }
            _logger.LogDebug("menus retrieved from database with pagination - PageNumber: {PageNumber}, PageSize: {PageSize}, TotalCount: {TotalCount}", pagination.PageNumber, pagination.PageSize, totaldata);
            // return data with pagination info
            return new ServiceResult<PaginationResponse<GetMenuDTO>>
            {
                Success = true,
                Data = new PaginationResponse<GetMenuDTO>
                {
                    pageSize = pagination.PageSize,
                    pageNumber = pagination.PageNumber,
                    totalCount = totaldata,
                    Data = menus
                },
                StatusCode = 200
            };

        }

        // this method gets list of items form cache if not exist return null and used in GetAllItemsService (used to organize and clear code)
        private async Task<PaginationResponse<GetMenuDTO>?> GetItemFromCacheHelper(string cacheKey)
        {
            if (_memoryCache.TryGetValue(cacheKey, out PaginationResponse<GetMenuDTO>? menu_memory))
            {
_logger.LogDebug("Menu retrieved from cache with key {CacheKey}", cacheKey);
                return menu_memory;
            }
            // 1. Try get from cache
            var cachedData = await _redisCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                var redis_menu = JsonSerializer.Deserialize<PaginationResponse<GetMenuDTO>?>(cachedData);

                _logger.LogDebug("Menu retrieved from Redis cache with key {CacheKey}", cacheKey);

                if (redis_menu != null)
                {
                    // add data from redis to memory cache for faster access next time
                    _memoryCache.Set(cacheKey, redis_menu, TimeSpan.FromMinutes(5));
                    _logger.LogDebug("menu  stored in CACHE with key {cacheKey}", cacheKey);

                    return redis_menu;
                }
            }
            return null;
        }




        // this method gets menu and authorization object with specific id from DB
        public async Task<(MenuCategory?, RestaurantAuthorizationDTO?)> GetMenuEntityAndAuth(int menu_category_id)
        {
            var menu = await _dbContext.menu_category
               .Where(m => m.CategoryId == menu_category_id)
               .Select(m => new
               {
                   menu = m,
                   auth = new RestaurantAuthorizationDTO
                   {
                       RestaurantId = m.restaurant.RestaurantId,
                       ownerId = m.restaurant != null ? m.restaurant.UserId : string.Empty
                   }
               }).FirstOrDefaultAsync();

            if (menu == null)
            {
                return (null, null);
            }
            return (menu.menu, menu.auth);


        }


        // this method used to apply filtering for GetAllItemsService  method
        private IQueryable<MenuCategory> ApplyFilter(IQueryable<MenuCategory> query, MenuFilter filter, UserRolee role)
        {

            if (filter.RestaurantId.HasValue)
            {
                query = query.Where(m => m.RestaurantId == filter.RestaurantId.Value);
                _logger.LogDebug("Applied  restaurant id filter: {RestaurantId}", filter.RestaurantId.Value);
            }
            if (!string.IsNullOrWhiteSpace(filter.MenuName))
            {
                query = query.Where(m => m.CategoryName.Contains(filter.MenuName));
                _logger.LogDebug("Applied menu name filter: {MenuName}", filter.MenuName);
            }
            if (filter.MenuId.HasValue)
            {
                query = query.Where(i => i.CategoryId == filter.MenuId.Value);
                _logger.LogDebug("Applied menu id  filter: {MenuId}", filter.MenuId.Value);
            }
            if (!string.IsNullOrEmpty(filter.SortBy))
            {
                query = ApplySorting(query, filter.SortBy, filter.FromLowToHigh);
                _logger.LogDebug("Applied sorting - SortBy: {SortBy}, FromLowToHigh: {FromLowToHigh}", filter.SortBy, filter.FromLowToHigh);
            }
            else
            {
                // default sorting by ID
                query = query.OrderByDescending(m => m.CategoryId);
                _logger.LogDebug("Applied default sorting by MenuId in decesending order");
            }
            return query;
        }

        // this method used to apply sorting for GetAllItemsService method 
        private IQueryable<MenuCategory> ApplySorting(IQueryable<MenuCategory> query, string sort_by, bool? from_low_to_high)
        {
            bool ascending = from_low_to_high.HasValue ? from_low_to_high.Value : true; // default to ascending if not specified

            switch (sort_by.ToLower())
            {
                case "restaurant id":
                    query = ascending ? query.OrderBy(i => i.RestaurantId) : query.OrderByDescending(i => i.RestaurantId);
                    _logger.LogDebug("Sorting by restaurant id in {Order} order", ascending ? "ascending" : "descending");
                    break;
                case "name":
                    query = ascending ? query.OrderBy(i => i.CategoryName) : query.OrderByDescending(i => i.CategoryName);
                    _logger.LogDebug("Sorting by name in {Order} order", ascending ? "ascending" : "descending");
                    break;

                case "menu id":
                default:
                    // Default sorting if sort_by value is unrecognized
                    query = ascending ? query.OrderBy(i => i.CategoryId) : query.OrderByDescending(i => i.CategoryId);
                    _logger.LogDebug("Sorting by ID in {Order} order (default)", ascending ? "ascending" : "descending");
                    break;
            }

            return query;
        }

private async Task<GetRestaurantCacheDTO?> GetRestaurantCache(string userId)
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
                _logger.LogDebug("Restaurant for user {UserId} stored in CACHE", userId);

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