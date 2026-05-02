using System;
using System.Text.Json;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Flters;
using food_order_system1.Modles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using static food_order_system1.Controllers.OrderController;


namespace food_order_system1.Service.RestaurantService
{
    public interface IRestauantService
    {
        Task<ServiceResult<PaginationResponse<viewRestaurantAndMenuDTO>>> GetRestauantMenuAndItmesService(PaginationParams p, RestaurantFilter f, string userId, UserRolee role);

        Task<ServiceResult<int>> CreateRestaurantService(CreateRestaurantDTO request_restaurant);

        Task<ServiceResult<int>> UpdateRestaurantService(UpdateRestaurantDTO dto, Restaurant restaurant);

        Task<ServiceResult<bool>> OpenCloseRestaurantService(Restaurant restaurant);

        Task<ServiceResult<bool>> DeleteRestaurantService(Restaurant restaurant);

        Task<ServiceResult<string>> CreateRequestResturantManagerServise(string user_id);

        Task<ServiceResult<int>> AcceptRequestToManagerService(string user_id);

        Task<ServiceResult<int>> AproveRegectRestaurantService(Restaurant res);

        Task<(Restaurant?, RestaurantAuthorizationDTO?)> GetRestaurantEntityAndAuth(int res_id);

        Task<ApplicationUser?> GetUserById(string userId);




    }

    public class RestaurantService : IRestauantService
    {
        private readonly AppUser _dbContext;
          private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RestaurantService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _redisCache;
        private readonly IcacheService _cacheService;
        public RestaurantService(AppUser context,UserManager<ApplicationUser> userManager ,  ILogger<RestaurantService> logger, IMemoryCache memoryCache, IDistributedCache redisCache, IcacheService cacheService)
        {
            _dbContext = context;
             _userManager = userManager;
            _logger = logger;
            _memoryCache = memoryCache;
            _redisCache = redisCache;
            _cacheService = cacheService;
        }





        public async Task<ServiceResult<int>> CreateRestaurantService(CreateRestaurantDTO request_restaurant)
        {
            var nameExists = await _dbContext.restaurants
                .AnyAsync(r => r.Restaurant_Name == request_restaurant.Restaurant_Name);

            if (nameExists)
            {
                _logger.LogWarning(
    "Restaurant name already exists: {RestaurantName}",
    request_restaurant.Restaurant_Name);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "name already exist",
                    StatusCode = 400
                };
            }

            var userHasRestaurant = await _dbContext.restaurants
                .AnyAsync(r => r.UserId == request_restaurant.ManagerId);

            if (userHasRestaurant)
            {
              _logger.LogWarning(
    "User {ManagerId} already owns a restaurant. Restaurant creation blocked.",
    request_restaurant.ManagerId);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "manager already has restaurant",
                    StatusCode = 400
                };
            }

            var new_res = new Restaurant
            {
                Restaurant_Name = request_restaurant.Restaurant_Name,
                Description = request_restaurant.Description,
                IsOpen = false,
                RestaurantStatus = RestaurantStatuss.Pending,
                Address = request_restaurant.Address,
                UserId = request_restaurant.ManagerId,

            };
            _dbContext.restaurants.Add(new_res);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
              _logger.LogError(ex,
    "Database error occurred while creating restaurant");
                throw;
            }


            _logger.LogInformation("new Restaurant is created with userId {ManagerId}  restauant name {RestaurantName}", request_restaurant.ManagerId, request_restaurant.Restaurant_Name);
            return new ServiceResult<int>
            {
                Success = true,
                Message = "new restaurant creaed",
                Data = new_res.RestaurantId,
                StatusCode = 201
            };


        }

        public async Task<ServiceResult<int>> UpdateRestaurantService(UpdateRestaurantDTO dto, Restaurant restaurant)
        {

            if (!string.IsNullOrWhiteSpace(dto.Restaurant_Name) &&
                dto.Restaurant_Name != restaurant.Restaurant_Name)
            {
                var nameExists = await _dbContext.restaurants
                    .AnyAsync(r => r.Restaurant_Name == dto.Restaurant_Name
                                && r.RestaurantId != restaurant.RestaurantId);

                if (nameExists)
                {
                    _logger.LogWarning("restaurant name already exist with name {RestaurantName}", dto.Restaurant_Name);
                    return new ServiceResult<int>
                    {
                        Success = false,
                        Message = "Restaurant name already exists",
                        StatusCode = 400
                    };
                }
                restaurant.Restaurant_Name = dto.Restaurant_Name;
            }

            if (!string.IsNullOrWhiteSpace(dto.Description))
            {
                restaurant.Description = dto.Description;
            }

            if (!string.IsNullOrWhiteSpace(dto.Address))
            {
                restaurant.Address = dto.Address;
            }

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
    "Database error occurred while updating restaurant {RestaurantId}",
    restaurant.RestaurantId);
                throw;

            }

            await _cacheService.UpdateKeyVersionForRestaurantPagination(restaurant.RestaurantId);
            await _cacheService.UpdateKeyVersionForRestaurant(restaurant.UserId);
            await _cacheService.UpdateKeyVersionForItemPagiation(restaurant.RestaurantId);
            await _cacheService.UpdateKeyVersionForMenuPagination(restaurant.RestaurantId);


_logger.LogInformation(
    "Updating restaurant {RestaurantId}",
    restaurant.RestaurantId);
                return new ServiceResult<int>
            {
                Success = true,
                Message = "Restaurant update sucessfully",
                Data = restaurant.RestaurantId,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<bool>> OpenCloseRestaurantService(Restaurant restaurant)
        {
                    _logger.LogInformation(
    "Toggling restaurant open/close state for {RestaurantId}. Current state: {IsOpen}",
    restaurant.RestaurantId,
    restaurant.IsOpen);

            restaurant.IsOpen = !restaurant.IsOpen;
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "database error accure while opening/closing restaurant {restaurantId}", restaurant.RestaurantId);
                throw;
            }

            await _cacheService.UpdateKeyVersionForRestaurantPagination(restaurant.RestaurantId);
            await _cacheService.UpdateKeyVersionForRestaurant(restaurant.UserId);
            await _cacheService.UpdateKeyVersionForItemPagiation(restaurant.RestaurantId);
            await _cacheService.UpdateKeyVersionForMenuPagination(restaurant.RestaurantId);

    

    _logger.LogInformation(
    "Restaurant {RestaurantId} status changed successfully. New state: {IsOpen}",
    restaurant.RestaurantId,
    restaurant.IsOpen);
            return new ServiceResult<bool>
            {
                Success = true,
                Message = $"Restaurant is now {(restaurant.IsOpen ? "open" : "closed")}",
                Data = restaurant.IsOpen,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<bool>> DeleteRestaurantService(Restaurant restaurant)
        {
              if (restaurant.IsDeleted)
            {
_logger.LogWarning(
    "Restaurant already deleted: {RestaurantId}",
    restaurant.RestaurantId);
                 return new ServiceResult<bool>
              {
                Success = true,
                Message = "Restaurant already deleted",
                StatusCode = 200
              };   
            }
            _logger.LogInformation(
    "Starting deletion process for restaurant {RestaurantId}",
    restaurant.RestaurantId);
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                restaurant.IsDeleted = true;
                restaurant.IsOpen = false;

                await _dbContext.menu_category
                  .Where(m => m.RestaurantId == restaurant.RestaurantId)
                  .ExecuteUpdateAsync(m => m.SetProperty(c => c.IsDeleted, true));


                await _dbContext.items
                 .Where(i => i.MenuCategory.RestaurantId == restaurant.RestaurantId)
                 .ExecuteUpdateAsync(i =>
                  i.SetProperty(c => c.IsDeleted, true)
                  .SetProperty(c => c.IsActive, false)
                  );

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "database error accurred while deleting restaurant {restaurantId}", restaurant.RestaurantId);
                throw;
            }

            await _cacheService.UpdateKeyVersionForRestaurantPagination(restaurant.RestaurantId);
            await _cacheService.UpdateKeyVersionForRestaurant(restaurant.UserId);
            await _cacheService.UpdateKeyVersionForItemPagiation(restaurant.RestaurantId);
            await _cacheService.UpdateKeyVersionForMenuPagination(restaurant.RestaurantId);


_logger.LogInformation(
    "Restaurant deleted successfully with id {RestaurantId}",
    restaurant.RestaurantId);

            return new ServiceResult<bool>
            {
                Success = true,
                Message = "Restaurant deleted successfully",
                StatusCode = 200
            };
        }



        public async Task<ServiceResult<PaginationResponse<viewRestaurantAndMenuDTO>>> GetRestauantMenuAndItmesService(PaginationParams pagination, RestaurantFilter filter, string userId, UserRolee role)
        {
            var query = _dbContext.restaurants
            .AsNoTracking();

            if (role == UserRolee.Customer)
            {
                filter.Status = RestaurantStatuss.Accepted;
                query = query.Where(r => !r.IsDeleted && r.RestaurantStatus == filter.Status);
            }
            else if (role == UserRolee.RestaurantManager)
            {
                query = query.Where(r => r.UserId == userId && !r.IsDeleted && r.RestaurantStatus == RestaurantStatuss.Accepted);
                var res_id = await query.Select(r => r.RestaurantId).FirstOrDefaultAsync();
                filter.restaurantId = res_id;
                filter.Status = RestaurantStatuss.Accepted;

            }

            bool use_caching = pagination.PageNumber <= 3 &&
                            pagination.PageSize == 10 &&
                           string.IsNullOrEmpty(filter.restaurantName) &&
                           string.IsNullOrEmpty(filter.SortBy) &&
                           filter.restaurantId.HasValue;



            var version_string = $"Restaurant_Version_{filter.restaurantId}";
            var version = await _redisCache.GetStringAsync(version_string) ?? "1";

            string cacheKey = $"Restaurants_{version}_" +
                $"PageNumber{pagination.PageNumber}_" +
                $"PageSize{pagination.PageSize}_" +
                $"RestaurantId{filter.restaurantId}_" +
                $"Status{filter.Status}";

            if (use_caching)
            {
                var CachedData = await GetItemFromCacheHelper(cacheKey);

                if (CachedData != null)
                {
                    _logger.LogDebug("restaurants retrieved from cache with key {cachekey}", cacheKey);
                    // return data with pagination info
                    return new ServiceResult<PaginationResponse<viewRestaurantAndMenuDTO>>
                    {
                        Success = true,
                        Data = new PaginationResponse<viewRestaurantAndMenuDTO>
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

            query = ApplyFilter(query, filter, role);

            var totalData = await query.CountAsync();

            var restaurants = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(r => new viewRestaurantAndMenuDTO
            {
                restaurantName = r.Restaurant_Name,
                address = r.Address,
                Discription = r.Description,
                Open = r.IsOpen ? "Open Now" : "Clouse Now",
                IsDeleted = r.IsDeleted,
                restaurantStatus = r.RestaurantStatus,
                Menus = r.Category.Where(m => !m.IsDeleted).Select(r => new ViewRestaurantMenuMenuDTO
                {
                    MenuName = r.CategoryName,
                    Items = r.menu_category_items.Where(r => r.IsActive && !r.IsDeleted)
                      .Select(r => new ViewRestaurantMenuItemsDTO
                      {
                          ItemName = r.ItemName,
                          ItemPrice = (double)r.ItemPrice,

                      }).ToList(),

                }).ToList()

            }).ToListAsync();



            if (use_caching)
            {
                _memoryCache.Set(
                  cacheKey,
                  new PaginationResponse<viewRestaurantAndMenuDTO>
                  {
                      pageSize = pagination.PageSize,
                      pageNumber = pagination.PageNumber,
                      totalCount = totalData,
                      Data = restaurants
                  },
                  TimeSpan.FromMinutes(3));


                await _redisCache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(
                    new PaginationResponse<viewRestaurantAndMenuDTO>
                    {
                        pageSize = pagination.PageSize,
                        pageNumber = pagination.PageNumber,
                        totalCount = totalData,
                        Data = restaurants
                    }),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            }
            );


            }

            if (restaurants.Count > 0)
            {
                _logger.LogInformation(
    "Fetched restaurants from database. Page: {PageNumber}, Size: {PageSize}, Total: {Total}",
    pagination.PageNumber,
    pagination.PageSize,
    totalData);
                return new ServiceResult<PaginationResponse<viewRestaurantAndMenuDTO>>
                {
                    Success = true,
                    Data = new PaginationResponse<viewRestaurantAndMenuDTO>
                    {
                        pageSize = pagination.PageSize,
                        pageNumber = pagination.PageNumber,
                        totalCount = totalData,
                        Data = restaurants
                    },
                    StatusCode = 200
                };
            }
            else
                return new ServiceResult<PaginationResponse<viewRestaurantAndMenuDTO>>
                {
                    Success = false,
                    Message = $"No Restaurant Found ",
                    StatusCode = 200
                };
        }


        // this method used to apply filtering for GetAllItemsService  method
        private IQueryable<Restaurant> ApplyFilter(IQueryable<Restaurant> query, RestaurantFilter filter, UserRolee role)
        {

            query = query.Where(r => r.RestaurantStatus == filter.Status);

            if (filter.restaurantId.HasValue)
            {
                query = query.Where(r => r.RestaurantId == filter.restaurantId.Value);
                _logger.LogDebug("Applied restaurant id filter: {restaurantId}", filter.restaurantId.Value);
            }
            if (!string.IsNullOrWhiteSpace(filter.restaurantName))
            {
                query = query.Where(r => r.Restaurant_Name.Contains(filter.restaurantName));
                _logger.LogDebug("Applied restaurant name filter: {restaurantName}", filter.restaurantName);
            }
            if (!string.IsNullOrWhiteSpace(filter.Adress))
            {
                query = query.Where(r => r.Address.Contains(filter.Adress));
                _logger.LogDebug("Applied adress filter: {Adress}", filter.Adress);
            }
            if (!string.IsNullOrEmpty(filter.SortBy))
            {
                query = ApplySorting(query, filter.SortBy, filter.FromLowToHigh);
                _logger.LogDebug("Applied sorting - SortBy: {SortBy}, FromLowToHigh: {FromLowToHigh}", filter.SortBy, filter.FromLowToHigh);
            }
            else
            {
                // default sorting by ID
                query = query.OrderByDescending(r => r.RestaurantId);
                _logger.LogDebug("Applied default sorting by restauantId in decending order");
            }
            return query;
        }

        // this method used to apply sorting for GetAllItemsService method 
        private IQueryable<Restaurant> ApplySorting(IQueryable<Restaurant> query, string sort_by, bool? from_low_to_high)
        {
            bool ascending = from_low_to_high.HasValue ? from_low_to_high.Value : true; // default to ascending if not specified

            switch (sort_by.ToLower())
            {
                case "restaurant id":
                    query = ascending ? query.OrderBy(r => r.RestaurantId) : query.OrderByDescending(r => r.RestaurantId);
                    _logger.LogDebug("Sorting by restaurant id in {Order} order", ascending ? "ascending" : "descending");
                    break;
                case "restaurant name":
                    query = ascending ? query.OrderBy(r => r.Restaurant_Name) : query.OrderByDescending(r => r.Restaurant_Name);
                    _logger.LogDebug("Sorting by restaurant name in {Order} order", ascending ? "ascending" : "descending");
                    break;

                case "adress":
                    query = ascending ? query.OrderBy(r => r.Address) : query.OrderByDescending(r => r.Address);
                    _logger.LogDebug("Sorting by adress in {Order} order", ascending ? "ascending" : "descending");
                    break;

                default:
                    // Default sorting if sort_by value is unrecognized
                    query = ascending ? query.OrderBy(r => r.RestaurantId) : query.OrderByDescending(r => r.RestaurantId);
                    _logger.LogDebug("Sorting by ID in {Order} order (default)", ascending ? "ascending" : "descending");
                    break;
            }

            return query;
        }

        // this method gets list of items form cache if not exist return null and used in GetAllItemsService (used to organize and clear code)
        private async Task<PaginationResponse<viewRestaurantAndMenuDTO>?> GetItemFromCacheHelper(string cacheKey)
        {
            if (_memoryCache.TryGetValue(cacheKey, out PaginationResponse<viewRestaurantAndMenuDTO>? menu_memory))
            {
                _logger.LogDebug("restaurant and items  retrieved from cache with key  {cachekey }", cacheKey);
                return menu_memory;
            }
            // 1. Try get from cache
            var cachedData = await _redisCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                var redis_menu = JsonSerializer.Deserialize<PaginationResponse<viewRestaurantAndMenuDTO>?>(cachedData);

                _logger.LogDebug("restaurant and items retrieved from Redis cache with key {cachekey}", cacheKey);

                if (redis_menu != null)
                {
                    // add data from redis to memory cache for faster access next time
                    _memoryCache.Set(cacheKey, redis_menu, TimeSpan.FromMinutes(5));
                    _logger.LogDebug("restaurant  stored in CACHE with key {cacheKey}", cacheKey);

                    return redis_menu;
                }
            }
            return null;
        }




        public async Task<ServiceResult<string>> CreateRequestResturantManagerServise(string user_id)
        {
            var existOrderNow = await _dbContext.orders
            .AsNoTracking()
            .AnyAsync(o => o.UserId == user_id && o.Status != OrderStatuss.Delivered);
            if (existOrderNow)
            {
_logger.LogWarning(
    "User {UserId} attempted to become restaurant manager but has active orders",
    user_id);
                return
                    new ServiceResult<string>
                    {
                        Success = false,
                        Message = "You already have an order in processing wait until your order is completed",
                        StatusCode = 400
                    };
            }

            var existingRequest = await _dbContext.request_manager
               .AsNoTracking()
               .AnyAsync(r => r.UserId == user_id);

            if (existingRequest)
            {
               _logger.LogInformation(
    "User {UserId} already submitted restaurant manager request",
    user_id);
                return
                    new ServiceResult<string>
                    {
                        Success = false,
                        Message = "You already send a request to become a restaurant manager.",
                        StatusCode = 400
                    };
            }

            var new_request = new RestauranManagerRequest
            {
                UserId = user_id,
                Status = "Pending",
                RequestedAt = DateTime.UtcNow

            };

            _dbContext.request_manager.Add(new_request);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "database error acured while requestion to restaurasnt manager for uer {userId}", user_id);
                throw;
            }

         _logger.LogInformation(
    "New restaurant manager request created for user {UserId}",
    user_id);
            return
                new ServiceResult<string>
                {
                    Success = true,
                    Message = "Your request to become a restaurant manager has been submitted successfully , wait untill approved by admin",
                    Data = new_request.UserId,
                    StatusCode = 201
                };

        }

        public async Task<ServiceResult<int>> AcceptRequestToManagerService(string userId)
        {

             var user=await _userManager.FindByIdAsync(userId);
       
          if(user == null)
            {
                _logger.LogWarning("accept request to manager requeste called for non existing user {userId}",userId);
             return new ServiceResult<int>
              {
                Success = true,
                Message = "user not ofund",
                StatusCode = 404
              }; 
            }


            var request = await _dbContext.request_manager.FirstOrDefaultAsync(r => r.UserId == user.Id && r.Status == "Pending");
            if (request == null)
            {
                _logger.LogWarning("user {userId} don't have a pending request to become a restaurant manager.",userId);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "No pending request found for this user.",
                    StatusCode = 404
                };

            }
               
            var roles = await _userManager.GetRolesAsync(user);

           using var transaction=await _dbContext.Database.BeginTransactionAsync();
           try {

            // Add the user to the RestaurantManager role
            if (roles.Contains(UserRolee.Customer.ToString()))
            {
                await _userManager.RemoveFromRoleAsync(user, UserRolee.Customer.ToString());
            }

            var result = await _userManager.AddToRoleAsync(user, UserRolee.RestaurantManager.ToString());
            if (!result.Succeeded)
            {
               await transaction.RollbackAsync();
                _logger.LogWarning("can not assign restaurant manager role for user {Id}", user.Id);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "can not assign restaurant manager role for user",
                    StatusCode = 500
                };
            }

            // change the request status in the database
            request.Status = "Accepted";
            request.AcceptedAt = DateTime.UtcNow;

          
            _dbContext.request_manager.Update(request);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();
            
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
_logger.LogError(ex,
    "Database error occurred while promoting user {UserId} to restaurant manager",
    user.Id);
                throw;
            }

            return
                new ServiceResult<int>
                {
                    Success = true,
                    Message = "User has been promoted to Restaurant Manager.",
                    Data = user.userId,
                    StatusCode = 200

                };


        }

        public async Task<ServiceResult<int>> AproveRegectRestaurantService(Restaurant restaurant)
        {

            switch (restaurant.RestaurantStatus)
            {
                case RestaurantStatuss.Pending:
                    restaurant.RestaurantStatus = RestaurantStatuss.Accepted;
                    break;

                case RestaurantStatuss.Accepted:
                    restaurant.RestaurantStatus = RestaurantStatuss.Regected;
                    break;

                case RestaurantStatuss.Regected:
                    restaurant.RestaurantStatus = RestaurantStatuss.Accepted;
                    break;

                default:
                  _logger.LogWarning(
    "Invalid restaurant status transition: {Status}",
    restaurant.RestaurantStatus); 

                    return new ServiceResult<int>
                    {
                        Success = false,
                        Message = "invalid status transmision",
                        StatusCode = 400
                    };
            }

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "database error acured while Approvinf a restaurasnt {restaurantId}", restaurant.RestaurantId);
                throw;
            }

         _logger.LogInformation(
    "Restaurant {RestaurantId} status changed to {Status}",
    restaurant.RestaurantId,
    restaurant.RestaurantStatus);

            return new ServiceResult<int>
            {
                Success = true,
                Message = $"{restaurant.Restaurant_Name} is {restaurant.RestaurantStatus.ToString()}  successfully",
                Data = restaurant.RestaurantId,
                StatusCode = 200
            };

        }

        public async Task<Cart?> GetCart(int cart_id)
        {
            return await _dbContext.carts
           .FirstOrDefaultAsync(i => i.CartId == cart_id);
        }

        public async Task<Orders?> GetOrders(int order_id)
        {
            return await _dbContext.orders
              .Include(o => o.Cart)
              .FirstOrDefaultAsync(o => o.OrderId == order_id);
        }

        public async Task<(Restaurant?, RestaurantAuthorizationDTO?)> GetRestaurantEntityAndAuth(int res_id)
        {
            var res = await _dbContext.restaurants
            .Where(r => r.RestaurantId == res_id && !r.IsDeleted)
            .Select(r => new
            {
                restaurant = r,
                Auth = new RestaurantAuthorizationDTO
                {
                    RestaurantId = r.RestaurantId,
                    ownerId = r.UserId
                }
            }).FirstOrDefaultAsync();

            if (res == null)
                return (null, null);

            return (res.restaurant, res.Auth);

        }

        public async Task<ApplicationUser?> GetUserById(string userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            return user;
        }
    }
}