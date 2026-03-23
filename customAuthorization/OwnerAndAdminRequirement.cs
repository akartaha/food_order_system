using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using food_order_system1.Modles;
using Microsoft.AspNetCore.Authorization;

namespace food_order_system1.customAuthorization
{
    public class RestauantOwnerShipAndAdminRequirement : IAuthorizationRequirement
    {
        
    }

    public class OwnerAndAdminAuthorizationHandler : AuthorizationHandler<RestauantOwnerShipAndAdminRequirement, Restaurant>
    {  
    
         public OwnerAndAdminAuthorizationHandler()
        {
      
        }
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RestauantOwnerShipAndAdminRequirement requirement, Restaurant resource)
        {
               var user_id= context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (user_id == resource.UserId || context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }
}