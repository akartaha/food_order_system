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
    public interface IMenuService
    {
        Task<ServiceResult<int>> CreaetMenuService(string user_id, CreateMenuDTO request_menu);
        Task<ServiceResult<int>> UpdateMenuService(int menu_id, UpdateMenuCategoryDTO dto, ClaimsPrincipal User);
        Task<ServiceResult<bool>> DeleteMenuService(int menu_id, ClaimsPrincipal User);



    }

    public class MenuService : IMenuService
    {
        private readonly AppUser _dbContext;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<ApplicationUser> _userManager;




        public MenuService(AppUser context,
         IAuthorizationService authorizationService,
         UserManager<ApplicationUser> userManager)
        {
            _dbContext = context;
            _authorizationService = authorizationService;
            _userManager = userManager;

        }

        public async Task<ServiceResult<int>> CreaetMenuService(string user_id, CreateMenuDTO request_menu)
        {
            var restaurant = await _dbContext.restaurants
              .FirstOrDefaultAsync(r => r.UserId == user_id);

            if (restaurant == null) return new ServiceResult<int>
            {
                Success = false,
                Message = "you do not own a restaurant to add menu to it",
                StatusCode = 403
            };


            var name_exist = await _dbContext.menu_category
               .AnyAsync(m => m.CategoryName == request_menu.CategoryName && m.RestaurantId == restaurant.RestaurantId);

            if (name_exist) return new ServiceResult<int>
            {
                Success = false,
                Message = "Category name already exists for this restaurant",
                StatusCode = 400
            };

            var new_menu = new MenuCategory
            {
                CategoryName = request_menu.CategoryName,
                RestaurantId = restaurant.RestaurantId,
            };
            _dbContext.menu_category.Add(new_menu);
            await _dbContext.SaveChangesAsync();
            return new ServiceResult<int>
            {
                Success = true,
                Message = $"{request_menu.CategoryName}    is create ",
                Data = new_menu.CategoryId,
                StatusCode = 201
            };

        }

        public async Task<ServiceResult<bool>> DeleteMenuService(int menu_id, ClaimsPrincipal User)
        {
            var menu = await _dbContext.menu_category
               .Include(m => m.restaurant)
               .FirstOrDefaultAsync(m => m.CategoryId == menu_id);

            if (menu == null) return new ServiceResult<bool>
            {
                Success = false,
                Message = "Menu category not found",
                StatusCode = 404,
                Data = false
            }
             ;

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              menu.restaurant,
             "RestauantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "You are not authorized to add items to this menu category",
                    StatusCode = 403,
                    Data = false
                };

            menu.IsDeleted = true;

            var items = await _dbContext.items
            .Where(i => i.MenuCategoryId == menu_id)
            .ExecuteUpdateAsync(i =>
            i.SetProperty(c => c.IsDeleted, true)
            .SetProperty(c => c.IsActive, false));

            await _dbContext.SaveChangesAsync();
            return new ServiceResult<bool>
            {
                Success = true,
                Message = "Menu category deleted sucess fully",
                Data = true,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<int>> UpdateMenuService(int menu_id, UpdateMenuCategoryDTO dto, ClaimsPrincipal User)
        {
            var menu = await _dbContext.menu_category
          .Include(m => m.restaurant)
          .FirstOrDefaultAsync(m => m.CategoryId == menu_id);

            if (menu == null) return new ServiceResult<int>
            {
                Success = false,
                Message = "menu not found",
                StatusCode = 404
            };


            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              menu.restaurant,
             "RestauantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "You are not authorized to add items to this menu category",
                    StatusCode = 403
                }

                   ;

            // return Ok("Menu category updated successfully");

            if (!string.IsNullOrWhiteSpace(dto.CategoryName))
            {
                var nameExists = await _dbContext.menu_category
                    .AnyAsync(r => r.CategoryName == dto.CategoryName
                                && r.RestaurantId == menu.RestaurantId
                                && r.CategoryId != menu_id);


                if (nameExists || dto.CategoryName == menu.CategoryName)
                    return new ServiceResult<int>
                    {
                        Success = false,
                        Message = "menu category name already exist",
                        StatusCode = 400
                    };

                menu.CategoryName = dto.CategoryName;
            }

            await _dbContext.SaveChangesAsync();
            return new ServiceResult<int>
            {
                Success = true,
                Message = "Menu category updated successfully",
                Data = menu.CategoryId,
                StatusCode = 200
            };
        }
    }
}