using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using food_order_system1.Data;
using food_order_system1.Modles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace food_order_system1.customAuthorization
{
    public class UserOwnerShipRequirement : IAuthorizationRequirement
    {
        
    }

    public class UserOwnerShipAuthorizationHandler : AuthorizationHandler<UserOwnerShipRequirement, ApplicationUser>{
      
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, UserOwnerShipRequirement requirement , ApplicationUser resource)
        {

            var user_id= context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (user_id == resource.Id)
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }
}