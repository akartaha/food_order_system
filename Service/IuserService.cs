using System;
using System.Net;
using food_order_system1.Data;
using food_order_system1.DTOs;
using food_order_system1.Flters;
using food_order_system1.Modles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using static food_order_system1.Controllers.OrderController;

namespace food_order_system1.Service
{
    public interface IUserService
    {
        Task<ServiceResult<int>> UpdateProfileService(string userId, UpdateProfileDTO request);

        Task<ServiceResult<string>> ChangeEmailService(ChangeEmailDTO request, IEmailService emailService, string userId);

        Task<ServiceResult<bool>> ConfirmEmailChangeService(string userId, string newEmail, string token);

        Task<ServiceResult<PaginationResponse<GetUserRoleDTO>>> GetUsersService(PaginationParams p, UserFilter filter, string AdminId);

        Task<ServiceResult<bool>> DeactiveUserService(string userId, string CurentUserId, UserRolee role);


        Task<ApplicationUser?> GetUserById(string id);

        Task<ApplicationUser?> GetUserByEmail(string email);

        Task<string> GetUserRoles(string userId);
    }
    public class UserService : IUserService
    {
        private readonly AppUser _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserService> _logger;



        public UserService(AppUser dbContext,
         UserManager<ApplicationUser> userManager,
         IHttpContextAccessor httpContextAccessor,
         ILogger<UserService> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<ServiceResult<bool>> DeactiveUserService(string userId, string CurentUserId, UserRolee role)
        {
            var user = await GetUserById(userId);
            if (user == null)
            {
                _logger.LogWarning("deactive user request called with no existing user {userId}", userId);
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "User not found",
                    StatusCode = 404
                };
            }

            if (userId == CurentUserId)
            {
                _logger.LogWarning("user request deactive user for youself with userId {userid}", userId);
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "you can not  Deactive your own account",
                    StatusCode = 400
                };
            }
            if (!user.IsActive)
            {
                _logger.LogWarning("deactive user request called for already deactived user {userid}", userId);
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "user Deactive Already",
                    StatusCode = 400
                };

            }
            if (_dbContext.Database.IsRelational())
            {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                user.IsActive = false;
                if (role == UserRolee.Customer)
                {
                    await _dbContext.orders
                        .Where(o => o.UserId == user.Id && (o.Status == OrderStatuss.Accepted || o.Status == OrderStatuss.Pending))
                        .ExecuteUpdateAsync(m => m.SetProperty(c => c.Status, OrderStatuss.Cancled));
                }
                if (role == UserRolee.RestaurantManager)
                {
                    await _dbContext.restaurants
                    .Where(r => r.UserId == user.Id)
                    .ExecuteUpdateAsync(m =>
                    m.SetProperty(c => c.IsDeleted, true)
                    .SetProperty(c => c.IsOpen, false));

                    await _dbContext.menu_category
                    .Where(m => m.restaurant.UserId == user.Id)
                    .ExecuteUpdateAsync(m => m.SetProperty(c => c.IsDeleted, true));


                    await _dbContext.items
                    .Where(i => i.MenuCategory.restaurant.UserId == user.Id)
                    .ExecuteUpdateAsync(i =>
                    i.SetProperty(c => c.IsDeleted, true)
                    .SetProperty(c => c.IsActive, false)
                    );
                }

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogError("Problem while deactivating user {UserId}. Errors: {Errors}",
                        user.Id,
                        updateResult.Errors.Select(e => e.Description));
                    await transaction.RollbackAsync();
                    return new ServiceResult<bool>
                    {
                        Success = false,
                        Message = "can not change user Activate ",
                        StatusCode = 400
                    };
                }
                await transaction.CommitAsync();
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                _logger.LogError(e, "a probleam accured while deactive user {uswerId}", user.Id);
                throw;
            }
            }
            else
            {
                 user.IsActive = false;
                if (role == UserRolee.Customer)
                {
                 var orders=   await _dbContext.orders
                        .Where(o => o.UserId == user.Id && (o.Status == OrderStatuss.Accepted || o.Status == OrderStatuss.Pending)).ToListAsync();
                 foreach(var order in orders)
                 {
                     order.Status=OrderStatuss.Cancled;
                 }
                  await _dbContext.SaveChangesAsync();
                    
                }
                if (role == UserRolee.RestaurantManager)
                {
                    var restaurants=await _dbContext.restaurants
                    .FirstOrDefaultAsync(r => r.UserId == user.Id);
                    restaurants.IsDeleted=true;

                   var menus= await _dbContext.menu_category
                    .Where(m => m.restaurant.UserId == user.Id).ToListAsync();
                    foreach(var menu in menus)
                    {
                        menu.IsDeleted=true;
                    }


                   var items= await _dbContext.items
                    .Where(i => i.MenuCategory.restaurant.UserId == user.Id).ToListAsync();
             
                    foreach(var item in items)
                    {
                        item.IsDeleted=true;
                        item.IsActive=false;
                    }
                     await _dbContext.SaveChangesAsync();
                      
                }

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogError("Problem while deactivating user {UserId}. Errors: {Errors}",
                        user.Id,
                        updateResult.Errors.Select(e => e.Description));
                    return new ServiceResult<bool>
                    {
                        Success = false,
                        Message = "can not change user Activate ",
                        StatusCode = 400
                    };
                }
                
            }


            _logger.LogInformation("user deactive successfully user {Id}", user.Id);
            return new ServiceResult<bool>
            {
                Success = true,
                Message = $"{user.fullName} is deactived sucessfully",
                StatusCode = 200,
                Data = true
            };
        }



        public async Task<ServiceResult<string>> ChangeEmailService(ChangeEmailDTO request, IEmailService emailService, string user_id)
        {
            var user = await _userManager.FindByIdAsync(user_id);

            if (user != null)
            {
                var EmailExist = await GetUserByEmail(request.Email);

                if (EmailExist != null)
                {
                    _logger.LogWarning("Email already in use attempt. UserId: {UserId}", user.Id);
                    return new ServiceResult<string>
                    {
                        Success = true,
                        Message = "If user exists, confirmation email will be sent",
                        StatusCode = 200
                    };
                }
                if (user.Email.ToLower() == request.Email.ToLower())
                {
                    _logger.LogInformation("user  can not use same email to change its email  user {Id}", user.Id);
                    return new ServiceResult<string>
                    {
                        Success = false,
                        Message = "new eimal must be defferent form privious email",
                        StatusCode = 400
                    };

                }
                // confirm eimail 
                var token = await _userManager.GenerateChangeEmailTokenAsync(user, request.Email);

                var encodedToken = WebUtility.UrlEncode(token);
                var Request = _httpContextAccessor.HttpContext.Request;

                var link = $"{Request.Scheme}://{Request.Host}/MySYS/User/confirm/email_change?userId={user.Id}&newEmail={request.Email}&token={encodedToken}";

                // Send confirmation email
                try
                {
                    await emailService.SendEmailAsync(
                        request.Email,
                       "Confirm your email",
                       $"<h3>Welcome!</h3><p>Click <a href='{link}'>here</a> to confirm your new email address.</p>"
                     );
                }
                catch (Exception e)
                {

                    _logger.LogError(e, "an error accured while sending email to changing user eimail for user {Id}", user.Id);
                    throw;
                }

            }
            else
            {
                _logger.LogWarning("change eimail requesting for non existing user");
                return new ServiceResult<string>
                {
                    Success = true, // intentionally vague (security)
                    Message = "If user exists, confirmation email will be sent",
                    StatusCode = 200
                };
            }


            _logger.LogInformation("Confirmation email sent to email for user {Id}", user.Id);
            return new ServiceResult<string>
            {
                Success = true,
                Message = "if email exist ,Confirmation email sent to new email",
                Data = user.Id,
                StatusCode = 200
            };



        }

        public async Task<ServiceResult<bool>> ConfirmEmailChangeService(string userId, string newEmail, string token)
        {

            var user = await GetUserById(userId);
            if (user == null)
            {
                _logger.LogWarning("user not found to change email with user id {userId}", userId);
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "User not found",
                    StatusCode = 404
                };
            }
            // var decodedToken = WebUtility.UrlDecode(token);

            var resultEmail = await _userManager.ChangeEmailAsync(user, newEmail, token);
            if (!resultEmail.Succeeded)
            {
                _logger.LogWarning("User {UserId} failed to change email. Errors: {Errors}",
    user.Id,
    resultEmail.Errors.Select(e => e.Description));
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "Failed to change email",
                    StatusCode = 400
                };
            }
            var setUsernameResult = await _userManager.SetUserNameAsync(user, newEmail);

            if (!setUsernameResult.Succeeded)
            {
                _logger.LogWarning("User {UserId} failed to update username. Errors: {Errors}",
    user.Id,
    resultEmail.Errors.Select(e => e.Description));
                return new ServiceResult<bool>
                {
                    Success = false,
                    Message = "Failed to update username",
                    StatusCode = 400
                };
            }
            _logger.LogInformation("user {Id} successfully changed email ", user.Id);
            return new ServiceResult<bool>
            {
                Success = true,
                Message = "Email changed successfully",
                StatusCode = 200,
                Data = true
            };

        }

        public async Task<ServiceResult<PaginationResponse<GetUserRoleDTO>>> GetUsersService(PaginationParams pagination, UserFilter filter, string AdminId)
        {
            // var query=  _dbContext.Users.AsQueryable().AsNoTracking(); 

            var queryString =
                    from user in _dbContext.Users
                    join userRole in _dbContext.UserRoles on user.Id equals userRole.UserId
                    join role in _dbContext.Roles on userRole.RoleId equals role.Id
                    select new UserWithRoleDto
                    {
                        User = user,
                        Role = role.Name
                    };


            var query = queryString.AsQueryable();

            query = ApplyFilter(query, filter);

            var totalData = await query.CountAsync();

            var users = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize).ToListAsync();



            var result = users.Select(u => new GetUserRoleDTO
            {
                UserID = u.User.Id,
                UserName = u.User.UserName ?? "",
                Email = u.User.Email ?? "",
                IsConfirmEmail = u.User.EmailConfirmed ? "yse" : "no",
                PhoneNumber = u.User.PhoneNumber ?? "",
                Role = u.Role.ToString(),
                IsActivied = u.User.IsActive ? "yes" : "no",

            }).ToList();


            _logger.LogInformation("Users list retrieved successfully. Total count: {Count} by Admin Id {AdminId}", totalData, AdminId);
            return new ServiceResult<PaginationResponse<GetUserRoleDTO>>
            {
                Success = true,
                Data = new PaginationResponse<GetUserRoleDTO>
                {
                    pageSize = pagination.PageSize,
                    pageNumber = pagination.PageNumber,
                    totalCount = totalData,
                    Data = result
                },
                StatusCode = 200
            };
        }


        public async Task<ApplicationUser?> GetUserByEmail(string email)
        {
            return await _userManager.FindByEmailAsync(email);

        }

        public async Task<ApplicationUser?> GetUserById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            return user;
        }

        // this method used to apply filtering for GetAllItemsService  method
        private IQueryable<UserWithRoleDto> ApplyFilter(IQueryable<UserWithRoleDto> query, UserFilter filter)
        {

            if (!string.IsNullOrEmpty(filter.userId))
            {
                query = query.Where(u => u.User.Id == filter.userId);
                _logger.LogDebug("Applied user id filter: {UseId}", filter.userId);
            }
            if (!string.IsNullOrWhiteSpace(filter.FullName))
            {
                query = query.Where(u => u.User.fullName.ToLower().Contains(filter.FullName.ToLower()));
                _logger.LogDebug("Applied full name filter: {fullName}", filter.FullName);
            }
            if (!string.IsNullOrWhiteSpace(filter.email))
            {
                query = query.Where(u => u.User.Email.ToLower().Contains(filter.email.ToLower()));
                _logger.LogDebug("Applied email filter: {Email}", filter.email);
            }
            if (filter.IsActive.HasValue)
            {
                query = query.Where(u => u.User.IsActive == filter.IsActive);
                _logger.LogDebug("Applied Aactive filter: {IsActive}", filter.IsActive);
            }
            if (!string.IsNullOrEmpty(filter.SortBy))
            {
                query = ApplySorting(query, filter.SortBy, filter.FromLowToHigh);
                _logger.LogDebug("Applied sorting - SortBy: {SortBy}, FromLowToHigh: {FromLowToHigh}", filter.SortBy, filter.FromLowToHigh);
            }
            else
            {
                // default sorting by ID
                query = query.OrderByDescending(u => u.User.Id);
                _logger.LogInformation("Applied default sorting by userId in descending order");
            }
            return query;
        }

        // this method used to apply sorting for GetAllItemsService method 
        private IQueryable<UserWithRoleDto> ApplySorting(IQueryable<UserWithRoleDto> query, string sort_by, bool? from_low_to_high)
        {
            bool ascending = from_low_to_high.HasValue ? from_low_to_high.Value : true; // default to ascending if not specified

            switch (sort_by.ToLower())
            {
                case "id":
                    query = ascending ? query.OrderBy(r => r.User.Id) : query.OrderByDescending(r => r.User.Id);
                    _logger.LogDebug("Sorting by user id in {Order} order", ascending ? "ascending" : "descending");
                    break;
                case "full name":
                    query = ascending ? query.OrderBy(r => r.User.fullName) : query.OrderByDescending(r => r.User.fullName);
                    _logger.LogDebug("Sorting by full name in {Order} order", ascending ? "ascending" : "descending");
                    break;

                default:
                    // Default sorting if sort_by value is unrecognized
                    query = ascending ? query.OrderBy(u => u.User.Id) : query.OrderByDescending(u => u.User.Id);
                    _logger.LogDebug("Sorting by ID in {Order} order (default)", ascending ? "ascending" : "descending");
                    break;
            }

            return query;
        }


        public async Task<ServiceResult<int>> UpdateProfileService(string userId, UpdateProfileDTO request)
        {
            var user = await GetUserById(userId);

            if (user == null)
            {
                _logger.LogWarning("user update requested for non existing user with userId {userId}", userId);
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "User not found",
                    StatusCode = 404
                };
            }
            if (!string.IsNullOrEmpty(request.fullName))
            {
                user.fullName = request.fullName;
            }

            if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                user.PhoneNumber = request.PhoneNumber;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogWarning("User {UserId} failed to update profile. Errors: {Errors}",
      user.Id,
      result.Errors.Select(e => e.Description));
                return new ServiceResult<int>
                {
                    Success = false,
                    Message = "Failed to update profile",
                    StatusCode = 400
                };
            }

            _logger.LogInformation("user {Id} profile update successfully", user.Id);
            return new ServiceResult<int>
            {
                Success = true,
                Message = "Profile updated successfully",
                StatusCode = 200
            };
        }

        public async Task<string> GetUserRoles(string userId)
        {
            var user = await GetUserById(userId);
            if (user == null)
            {
                _logger.LogWarning("GetUserRoles called for non-existing user {UserId}", userId);
                return null;
            }
            var roles = await _userManager.GetRolesAsync(user);
            _logger.LogInformation("Roles retrieved for user {UserId}", userId);
            return roles.FirstOrDefault();

        }


    }
}