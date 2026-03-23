using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Exceptions;
using food_order_system1.Modles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace food_order_system1.Service
{
    public interface IItemService
    {
        Task<ServiceResult<int>> CreateItemService(int MenuCategoryId, CreateItemDTO request_item, ClaimsPrincipal User);
        Task<ServiceResult<int>> UpdateItemService(int item_id, UpdateItemDTO dto, ClaimsPrincipal User);
        Task<ServiceResult<bool>> ActivateDeactivateItemService(int item_id, ClaimsPrincipal User);
        Task<ServiceResult<bool>> DeleteItemService(int item_id, ClaimsPrincipal User);
    }
    public class ItemService : IItemService
    {
        private readonly AppUser _dbContext;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<ApplicationUser> _userManager;


        public ItemService(AppUser context,
         IAuthorizationService authorizationService,
         UserManager<ApplicationUser> userManager)
        {
            _dbContext = context;
            _authorizationService = authorizationService;
            _userManager = userManager;
        }

        public async Task<ServiceResult<bool>> ActivateDeactivateItemService(int item_id, ClaimsPrincipal User)
        {
            var item = await _dbContext.items.FirstOrDefaultAsync(i => i.ItemId == item_id && !i.IsDeleted);
            if (item == null) return new ServiceResult<bool>
            {
                Success = false,
                Message = "item not found",
                Data = false,
                StatusCode = 404
            };

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              item.MenuCategory.restaurant,
             "RestauantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "You are not authorized to activate/deactivate this item",
                    Data = false,
                    StatusCode = 403
                };

            item.IsActive = !item.IsActive;
            await _dbContext.SaveChangesAsync();
            return new ServiceResult<bool>
            {
                Success = true,
                Message = $"Item is now {(item.IsActive ? "active" : "inactive")}",
                Data = true,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<int>> CreateItemService(int MenuCategoryId, CreateItemDTO request_item, ClaimsPrincipal User)
        {
            var menu = await _dbContext.menu_category
              .Select(m => new
              {
                  m.CategoryId,
                  m.restaurant.UserId,
                  m.restaurant

              })
              .FirstOrDefaultAsync(m => m.CategoryId == MenuCategoryId);

            if (menu == null) return new ServiceResult<int>
            {
                Success = false,
                Message = "menu not found",
                StatusCode = 404,
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
                };

            var item_is_exist = await _dbContext.items.AnyAsync(m => m.ItemName == request_item.ItemName && m.MenuCategoryId == MenuCategoryId);

            if (item_is_exist) return new ServiceResult<int>
            {
                Success = false,
                Message = "Item already exists in this menu category",
                StatusCode = 400
            };
            if (request_item.ItemPrice <= 0)
            {
                throw new BusinessException("Price must be greater than 0");
            }
            var new_item = new Item
            {
                ItemName = request_item.ItemName,
                ItemPrice = request_item.ItemPrice,
                MenuCategoryId = MenuCategoryId,
            };

            _dbContext.items.Add(new_item);
            await _dbContext.SaveChangesAsync();
            return new ServiceResult<int>
            {
                Success = true,
                Message = "new item created",
                Data = new_item.ItemId,
                StatusCode = 201
            };
        }

        public async Task<ServiceResult<bool>> DeleteItemService(int item_id, ClaimsPrincipal User)
        {
            var item = await _dbContext.items
             .Include(i => i.MenuCategory)
             .ThenInclude(m => m.restaurant)
             .FirstOrDefaultAsync(i => i.ItemId == item_id);

            if (item == null) return new ServiceResult<bool>
            {
                Success = false,
                Message = "item not found",
                Data = false,
                StatusCode = 404
            };

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              item.MenuCategory.restaurant,
             "RestauantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "You are not authorized to delete this item",
                    Data = false,
                    StatusCode = 403
                };

            item.IsDeleted = true;
            item.IsActive = false;

            await _dbContext.SaveChangesAsync();
            return new ServiceResult<bool>
            {
                Success = true,
                Message = "item deleted sucessfully",
                Data = true,
                StatusCode = 200
            };
        }

        public async Task<ServiceResult<int>> UpdateItemService(int item_id, UpdateItemDTO dto, ClaimsPrincipal User)
        {
            var item = await _dbContext.items
             .Include(i => i.MenuCategory)
             .ThenInclude(m => m.restaurant)
             .FirstOrDefaultAsync(i => i.ItemId == item_id);

            if (item == null) return new ServiceResult<int>
            {
                Success = false,
                Message = "item not found",
                StatusCode = 404
            };

            var authResult = await _authorizationService.AuthorizeAsync(
              User,
              item.MenuCategory.restaurant,
             "RestauantOwnerShipAndAdminPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "You are not authorized to update this item",
                    StatusCode = 403
                };

            if (!string.IsNullOrWhiteSpace(dto.ItemName))
            {
                var nameExists = await _dbContext.items
                    .AnyAsync(i => i.ItemName == dto.ItemName
                                && i.MenuCategoryId == item.MenuCategoryId
                                && i.ItemId != item_id);


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
                throw new BusinessException("Price must be greater than 0");
            }

            if (string.IsNullOrWhiteSpace(dto.ItemName) && dto.ItemPrice <= 0)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "nothing to update",
                    StatusCode = 400
                };

            await _dbContext.SaveChangesAsync();
            return new ServiceResult<int>
            {
                Success = true,
                Message = "Item updated successfully",
                Data = item.ItemId,
                StatusCode = 201
            };
        }
    }
}