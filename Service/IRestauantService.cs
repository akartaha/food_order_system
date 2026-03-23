using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Modles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.ObjectPool;
using Org.BouncyCastle.Tls;

namespace food_order_system1.Service.RestaurantService
{
    public interface IRestauantService
    {
        Task<ServiceResult<List<viewRestauantDTO>>> GetAllRestaurantsService(string user_id, string role);

        Task<ServiceResult<List<viewRestauantDTO>>> GetRestauantByNameService(string res_name);

        Task<ServiceResult<List<viewRestauantAndMenuDTO>>> GetRestauantMenuAndItmesService(string res_name);

        Task<ServiceResult<int>> CreateRestaurantService(string user_id, CreateRestaurantDTO request_restaurant);

        Task<ServiceResult<int>> UpdateRestaurantService(int res_id, UpdateRestaurantDTO dto, ClaimsPrincipal User);

        Task<ServiceResult<string>> OpenCloseRestaurantService(int res_id, ClaimsPrincipal User);

        Task<ServiceResult<bool>> DeleteRestaurantService(int res_is, ClaimsPrincipal User);

        Task<ServiceResult<string>> CreateResturantManagerServise(string user_id);

        Task<ServiceResult<int>> AcceptRequestToManagerService(string user_id);

        Task<ServiceResult<int>> AproveRegectRestaurantService(int res_id);


    }

    public class RestaurantService : IRestauantService
    {
        private readonly AppUser _dbContext;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<ApplicationUser> _userManager;


        public RestaurantService(AppUser context,
         IAuthorizationService authorizationService,
         UserManager<ApplicationUser> userManager)
        {
            _dbContext = context;
            _authorizationService = authorizationService;
            _userManager = userManager;
        }
        public async Task<ServiceResult<List<viewRestauantDTO>>> GetAllRestaurantsService(String user_id, string role)
        {
            var query = _dbContext.restaurants.AsNoTracking().AsQueryable();
            if (role == "Customer")
            {

                query = query.Where(r => !r.IsDeleted);


            }
            else if (role == "RestaurantManager")
            {

                query = query.Where(r => !r.IsDeleted && r.UserId == user_id);

            }

            var restaurants = await query
                .Select(r => new viewRestauantDTO
                {
                    Name = r.Restaurant_Name,
                    Discription = r.Description,
                    address = r.Address,
                    Open = r.IsOpen ? "Open now" : "Clouse now "
                }).ToListAsync();

            if (restaurants.Any())
            {
                return new ServiceResult<List<viewRestauantDTO>>
                {
                    Success = true,
                    Data = restaurants,
                    StatusCode = 200

                };
            }
            else
            {
                return new ServiceResult<List<viewRestauantDTO>>
                {
                    Success = false,
                    Message = "restaurant not found",
                    StatusCode = 404

                };

            }
        }





        public async Task<ServiceResult<int>> CreateRestaurantService(string user_id, CreateRestaurantDTO request_restaurant)
        {
            var nameExists = await _dbContext.restaurants
                .AnyAsync(r => r.Restaurant_Name == request_restaurant.Restaurant_Name);

            if (nameExists)
            {
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "name already exist",
                    StatusCode = 400
                };
            }

            var userHasRestaurant = await _dbContext.restaurants
                .AnyAsync(r => r.UserId == user_id);

            if (userHasRestaurant)
            {
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "user already has restaurant",
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
                UserId = user_id,

            };
            _dbContext.restaurants.Add(new_res);
            await _dbContext.SaveChangesAsync();

            return new ServiceResult<int>
            {
                Success = true,
                Message = "new restaurant creaed",
                Data = new_res.RestaurantId,
                StatusCode = 201
            };


        }

        public async Task<ServiceResult<int>> UpdateRestaurantService(int res_id, UpdateRestaurantDTO dto, ClaimsPrincipal User)
        {
            var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.RestaurantId == res_id);
            if (restaurant == null) return new ServiceResult<int>
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
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "You are not authorized to update this restaurant",
                    StatusCode = 403
                };


            if (!string.IsNullOrWhiteSpace(dto.Restaurant_Name) &&
                dto.Restaurant_Name != restaurant.Restaurant_Name)
            {
                var nameExists = await _dbContext.restaurants
                    .AnyAsync(r => r.Restaurant_Name == dto.Restaurant_Name
                                && r.RestaurantId != res_id);

                if (nameExists)
                    return new ServiceResult<int>
                    {
                        Success = false,
                        Message = "Restaurant name already exists",
                        StatusCode = 400
                    };

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

            await _dbContext.SaveChangesAsync();

            return new ServiceResult<int>
            {
                Success = true,
                Message = "Restaurant update sucessfully",
                Data = restaurant.RestaurantId,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<string>> OpenCloseRestaurantService(int res_id, ClaimsPrincipal User)
        {
            var restaurant = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.RestaurantId == res_id && !r.IsDeleted);
            if (restaurant == null) return new ServiceResult<string>
            {
                Success = false,
                Message = "Restaurant not found",
                StatusCode = 404,
            };

            var authResult = await _authorizationService.AuthorizeAsync(
                  User,
                  restaurant,
                 "RestauantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "You are not authorized to open/close this restaurant",
                    StatusCode = 403
                };

            restaurant.IsOpen = !restaurant.IsOpen;
            await _dbContext.SaveChangesAsync();

            return new ServiceResult<string>
            {
                Success = true,
                Message = $"Restaurant is now {(restaurant.IsOpen ? "open" : "closed")}",
                Data = $"Restaurant is now {(restaurant.IsOpen ? "open" : "closed")}",
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<bool>> DeleteRestaurantService(int res_id, ClaimsPrincipal User)
        {
            var restaurant = await _dbContext.restaurants
            .FirstOrDefaultAsync(r => r.RestaurantId == res_id);

            if (restaurant == null) return new ServiceResult<bool>
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
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "You are not authorized to delete this restaurant",
                    StatusCode = 403
                };

            restaurant.IsDeleted = true;
            restaurant.IsOpen = false;

            var menus = await _dbContext.menu_category
               .Where(m => m.RestaurantId == res_id)
               .ExecuteUpdateAsync(m => m.SetProperty(c => c.IsDeleted, true));

            var categoryIds = await _dbContext.menu_category
               .Where(m => m.RestaurantId == res_id)
               .Select(m => m.CategoryId)
               .ToListAsync();

            var items = await _dbContext.items
               .Where(i => categoryIds.Contains(i.MenuCategoryId))
               .ExecuteUpdateAsync(i =>
                i.SetProperty(c => c.IsDeleted, true)
                .SetProperty(c => c.IsActive, false)

                );


            await _dbContext.SaveChangesAsync();

            return new ServiceResult<bool>
            {
                Success = true,
                Message = "Restaurant deleted successfully",
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<List<viewRestauantDTO>>> GetRestauantByNameService(string res_name)
        {
            var restaurants = await _dbContext.restaurants.AsNoTracking().Where(r => r.Restaurant_Name.Contains(res_name) && !r.IsDeleted)
            .Select(r => new viewRestauantDTO
            {
                Name = r.Restaurant_Name,
                Discription = r.Description,
                address = r.Address,
                Open = r.IsOpen ? "Open now" : "Clouse now "
            }).ToListAsync();

            if (restaurants.Count > 0)
                return new ServiceResult<List<viewRestauantDTO>>
                {
                    Success = true,
                    Data = restaurants,
                    StatusCode = 200
                };
            else
                return new ServiceResult<List<viewRestauantDTO>>
                {
                    Success = false,
                    Message = $"Restaurant not found with name {res_name}",
                    StatusCode = 404
                };
        }

        public async Task<ServiceResult<List<viewRestauantAndMenuDTO>>> GetRestauantMenuAndItmesService(string res_name)
        {

            var restaurants = await _dbContext.restaurants.AsNoTracking().Where(r => r.Restaurant_Name.Contains(res_name) && !r.IsDeleted)
            .Select(r => new viewRestauantAndMenuDTO
            {
                Name = r.Restaurant_Name,
                address = r.Address,
                Discription = r.Description,
                Open = r.IsOpen ? "Open Now" : "Clouse Now",
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


            if (restaurants.Count > 0)
                return new ServiceResult<List<viewRestauantAndMenuDTO>>
                {
                    Success = true,
                    Data = restaurants,
                    StatusCode = 200
                };
            else
                return new ServiceResult<List<viewRestauantAndMenuDTO>>
                {
                    Success = false,
                    Message = $"No Restaurant Found With name {res_name}",
                    StatusCode = 404
                };
        }

        public async Task<ServiceResult<string>> CreateResturantManagerServise(string user_id)
        {
            var existingRequest = await _dbContext.request_manager
               .AnyAsync(r => r.UserId == user_id);

            if (existingRequest)
            {
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
            await _dbContext.SaveChangesAsync();

            return
                new ServiceResult<string>
                {
                    Success = true,
                    Message = "Your request to become a restaurant manager has been submitted successfully.",
                    Data = new_request.UserId,
                    StatusCode = 201
                };

        }

        public async Task<ServiceResult<int>> AcceptRequestToManagerService(string user_id)
        {
            var user = await _userManager.FindByIdAsync(user_id);
            if (user == null)
            {
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "user not found",
                    StatusCode = 404
                };
            }
            // Add the user to the RestaurantManager role
            var roles = await _userManager.GetRolesAsync(user);

            var request = await _dbContext.request_manager.FirstOrDefaultAsync(r => r.UserId == user_id && r.Status == "Pending");
            if (request == null)
            {
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "No pending request found for this user.",
                    StatusCode = 404
                };

            }



            if (roles.Contains("Customer"))
            {
                await _userManager.RemoveFromRoleAsync(user, "Customer");
            }

            var result = await _userManager.AddToRoleAsync(user, "RestaurantManager");
            if (!result.Succeeded)
            {
                throw new Exception("Failed to assign role to user ");
            }

            // change the request status in the database
            request.Status = "Accepted";
            request.AcceptedAt = DateTime.Now;
            _dbContext.request_manager.Update(request);
            await _dbContext.SaveChangesAsync();

            return
                new ServiceResult<int>
                {
                    Success = true,
                    Message = "User has been promoted to Restaurant Manager.",
                    Data = user.userId,
                    StatusCode = 200

                };


        }

        public async Task<ServiceResult<int>> AproveRegectRestaurantService(int resid)
        {
            if (resid < 0)
            {
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "please input valid restaurant id",
                    StatusCode = 400
                };

            }
            var res = await _dbContext.restaurants.FirstOrDefaultAsync(r => r.RestaurantId == resid);

            if (res == null) return new ServiceResult<int>
            {
                Success = false,
                Message = "Restaurant not found",
                StatusCode = 404
            };


            switch (res.RestaurantStatus)
            {
                case RestaurantStatuss.Pending:
                    res.RestaurantStatus = RestaurantStatuss.Accepted;
                    break;

                case RestaurantStatuss.Accepted:
                    res.RestaurantStatus = RestaurantStatuss.Regected;
                    break;

                case RestaurantStatuss.Regected:
                    res.RestaurantStatus = RestaurantStatuss.Accepted;
                    break;

                default:
                    return new ServiceResult<int>
                    {
                        Success = false,
                        Message = "invalid status transmision",
                        StatusCode = 400
                    };
            }
            await _dbContext.SaveChangesAsync();
            return new ServiceResult<int>
            {
                Success = true,
                Message = $"{res.Restaurant_Name} is {res.RestaurantStatus.ToString()}  successfully",
                Data = res.RestaurantId,
                StatusCode = 200
            };

        }


    }
}