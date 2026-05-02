using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using food_order_system1.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace food_order_system1.customAuthorization
{
    public class UserOwnerShipRequirement: IAuthorizationRequirement
    {
        
    }

    public class UserOwnerShipAuthorizationHandler : AuthorizationHandler<UserOwnerShipRequirement, string>
    {
      
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, UserOwnerShipRequirement requirement , string userId)
        {

            var user_id= context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(user_id) && user_id == userId)
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }
}