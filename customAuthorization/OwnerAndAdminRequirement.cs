using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using food_order_system1.DTOs;
using food_order_system1.Modles;
using Microsoft.AspNetCore.Authorization;

namespace food_order_system1.customAuthorization
{
    public class RestauantOwnerShipAndAdminRequirement : IAuthorizationRequirement
    {
        
    }

    public class OwnerAndAdminAuthorizationHandler : AuthorizationHandler<RestauantOwnerShipAndAdminRequirement, RestaurantAuthorizationDTO>
    {  
    
         public OwnerAndAdminAuthorizationHandler()
        {
      
        }
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RestauantOwnerShipAndAdminRequirement requirement, RestaurantAuthorizationDTO resource)
        {
               var user_id= context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(user_id) && (user_id == resource.ownerId || context.User.IsInRole("Admin")))
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }
}