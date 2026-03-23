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
    public interface ICartService
    {
        Task<ServiceResult<int>> CreateCartService(string user_id, int restaurantId, CreateCartDTO request_cart);
        Task<ServiceResult<int>> AddItemToCartService(int cart_id, AddItemToCartDTO request_item, ClaimsPrincipal User);
        Task<ServiceResult<int>> UpdateItemCartService(int cartItemId, UpdateCartItem dto, ClaimsPrincipal User);
        Task<ServiceResult<string>> DeleteItemCartService(int cartItemId, ClaimsPrincipal User);
        Task<ServiceResult<List<ViewCartItemDTO>>> ViewCartItemsService(int cart_id, ClaimsPrincipal User);
        Task<ServiceResult<List<ViewCartItemDTO>>> ViewAllCartItemsService(string user_id, string role);



    }

    public class CartService : ICartService

    {

        private readonly AppUser _dbContext;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<ApplicationUser> _userManager;


        public CartService(AppUser context,
         IAuthorizationService authorizationService,
         UserManager<ApplicationUser> userManager)
        {
            _dbContext = context;
            _authorizationService = authorizationService;
            _userManager = userManager;
        }


        public async Task<ServiceResult<int>> CreateCartService(string userId, int restaurantId, CreateCartDTO request_cart)
        {
            var restaurant = await _dbContext.restaurants
               .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId && !r.IsDeleted);

            if (restaurant == null)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "restaurant not found",
                    StatusCode = 404
                };

            var cartExist = await _dbContext.carts
                .AnyAsync(c =>
                    c.UserId == userId &&
                    c.RestaurantId == restaurantId);

            if (cartExist)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "you already have a cart for this restaurant",
                    StatusCode = 400
                };

            var newCart = new Cart
            {
                CartName = request_cart.CartName,
                UserId = userId,
                RestaurantId = restaurantId
            };

            _dbContext.carts.Add(newCart);
            await _dbContext.SaveChangesAsync();

            return new ServiceResult<int>
            {
                Success = true,
                Message = "new cart create sucessfuly ",
                Data = newCart.CartId,
                StatusCode = 201
            };
        }
        public async Task<ServiceResult<int>> AddItemToCartService(int cart_id, AddItemToCartDTO request_item, ClaimsPrincipal User)
        {

            var cart = await _dbContext.carts
             .Include(c => c.User)
             .FirstOrDefaultAsync(c => c.CartId == cart_id);

            if (cart == null)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "cart not found",
                    StatusCode = 404
                };

            if (cart.User == null)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "user not found for this cart",
                    StatusCode = 404
                };

            var authResult = await _authorizationService.AuthorizeAsync(
               User,
               cart.User,
              "UserOwnerShipPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "You are not authorized to modify this cart.",
                    StatusCode = 403
                };


            var item = await _dbContext.items.FirstOrDefaultAsync(r => r.ItemId == request_item.ItemId && !r.IsDeleted);
            if (item == null)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "Item not found",
                    StatusCode = 404
                };

            var menu = await _dbContext.menu_category
            .AnyAsync(m => m.CategoryId == item.MenuCategoryId && m.RestaurantId == cart.RestaurantId && !m.IsDeleted);

            if (!menu)
            {
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "Item does not belong to this restaurant",
                    StatusCode = 404
                };
            }

            var cart_item = await _dbContext.cart_items.FirstOrDefaultAsync(c => c.CartId == cart_id && c.ItemId == request_item.ItemId);

            if (cart_item != null)
            {
                cart_item.Quantity += request_item.Quantity;
            }
            else
            {
                var new_cart_item = new CartItem
                {
                    Quantity = request_item.Quantity,
                    CartId = cart_id,
                    ItemId = request_item.ItemId
                };
                _dbContext.cart_items.Add(new_cart_item);
            }
            await _dbContext.SaveChangesAsync();

            return new ServiceResult<int>
            {
                Success = true,
                Message = $"{item.ItemName}  is added to  {cart.CartName}  sucessfully",
                Data = item.ItemId,
                StatusCode = 200
            };


        }

        public async Task<ServiceResult<int>> UpdateItemCartService(int cartItemId, UpdateCartItem dto, ClaimsPrincipal User)
        {
            var CartItem = await _dbContext.cart_items
              .Include(ci => ci.Cart)
              .ThenInclude(c => c.User)
              .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId);

            if (CartItem == null) return new ServiceResult<int>
            {
                Success = false,
                Message = "cart item not found",
                StatusCode = 404
            };

            var authResult = await _authorizationService.AuthorizeAsync(
                User,
                CartItem.Cart.User,
               "UserOwnerShipPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "You are not authorized to modify this cart item.",
                    StatusCode = 403
                };

            if (dto.NewQuantity <= 0)
            {
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "quantity should be grater than 0",
                    StatusCode = 400
                };
            }
            CartItem.Quantity = dto.NewQuantity;

            await _dbContext.SaveChangesAsync();

            return new ServiceResult<int>
            {
                Success = true,
                Message = "item quantity is updated ",
                Data = CartItem.Quantity,
                StatusCode = 200
            };

        }



        public async Task<ServiceResult<string>> DeleteItemCartService(int cartItemId, ClaimsPrincipal User)
        {
            var CartItem = await _dbContext.cart_items
           .Include(i => i.Cart)
             .ThenInclude(c => c.User)
           .FirstOrDefaultAsync(i => i.CartItemId == cartItemId);

            if (CartItem == null) return new ServiceResult<string>
            {
                Success = false,
                Message = "cart item not found",
                StatusCode = 404
            };

            if (CartItem.Cart.User == null)
            {
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "Cart item does not belong to any user.",
                    StatusCode = 404
                };
            }

            var authResult = await _authorizationService.AuthorizeAsync(
                User,
                CartItem.Cart.User,
               "UserOwnerShipPolicy");

            if (!authResult.Succeeded)
                return new ServiceResult<string>
                {
                    Success = false,
                    Message = "You are not authorized to delete this cart item.",
                    StatusCode = 403
                };



            _dbContext.cart_items.Remove(CartItem);
            await _dbContext.SaveChangesAsync();

            return new ServiceResult<string>
            {
                Success = true,
                Message = $"{CartItem.Item.ItemName} is removed sucessfully",
                StatusCode = 200


            };
        }

        public async Task<ServiceResult<List<ViewCartItemDTO>>> ViewCartItemsService(int cart_id, ClaimsPrincipal User)
        {
            var cart = await _dbContext.carts
                 .Include(c => c.User)
                 .FirstOrDefaultAsync(c => c.CartId == cart_id);

            if (cart == null) return new ServiceResult<List<ViewCartItemDTO>>
            {
                Success = false,
                Message = "cart not found",
                StatusCode = 404
            };

            if (cart.User == null)
            {
                return new ServiceResult<List<ViewCartItemDTO>>
                {
                    Success = false,
                    Message = "Cart does not belong to any user",
                    StatusCode = 404

                };
            }

            var authResult1 = await _authorizationService.AuthorizeAsync(
               User,
               cart.User,
              "UserOwnerShipPolicy");

            if (!authResult1.Succeeded)
                return new ServiceResult<List<ViewCartItemDTO>>
                {
                    Success = false,
                    Message = "You are not authorized to view this cart.",
                    StatusCode = 403
                };

            var cart_items = await _dbContext.carts.AsNoTracking()
            .Include(c => c.CartItem)
            .Where(c => c.UserId == cart.UserId && c.CartId == cart_id)
            .Select(c => new ViewCartItemDTO
            {
                CartName = c.CartName,
                RestaurantName = c.Restaurant.Restaurant_Name,
                Items = c.CartItem.Select(ci => new viewCartItemItemDTO
                {
                    ItemName = ci.Item.ItemName,
                    ItemPrice = ci.Item.ItemPrice,
                    Quantity = ci.Quantity,


                }).ToList(),
                cartTotalPrice = (double)c.CartItem.Sum(ci => ci.Item.ItemPrice * ci.Quantity)

            }).ToListAsync();

            if (cart_items.Count <= 0)
            {
                return new ServiceResult<List<ViewCartItemDTO>>
                {
                    Success = false,
                    Message = "No cart items found for this user",
                    StatusCode = 404
                };
            }
            return new ServiceResult<List<ViewCartItemDTO>>
            {
                Success = true,
                Data = cart_items,
                StatusCode = 200
            };


        }

        public async Task<ServiceResult<List<ViewCartItemDTO>>> ViewAllCartItemsService(string user_id, string role)
        {


            var query = _dbContext.carts.AsNoTracking().AsQueryable();

            if (role == "Customer")
            {
                query = query.Where(c => c.UserId == user_id);
            }

            var all_cart_items = await query
             .Select(c => new ViewCartItemDTO
             {
                 CartName = c.CartName,
                 RestaurantName = c.Restaurant.Restaurant_Name,
                 Username = c.User.UserName,
                 Items = c.CartItem.Select(ci => new viewCartItemItemDTO
                 {
                     ItemName = ci.Item.ItemName,
                     ItemPrice = ci.Item.ItemPrice,
                     Quantity = ci.Quantity
                 }).ToList(),
                 cartTotalPrice = c.CartItem.Any()
                   ? (double)c.CartItem.Sum(ci => ci.Item.ItemPrice * ci.Quantity)
                   : 0

             }).ToListAsync();


            return new ServiceResult<List<ViewCartItemDTO>>
            {
                Success = true,
                Data = all_cart_items,
                StatusCode = 200,
            };



        }
    }
}