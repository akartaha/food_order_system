
using System.Security.Claims;
using food_order_system1.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace food_order_system1.customAuthorization
{
    public class CartOwnerShipRequirement : IAuthorizationRequirement
    {
        
    }

    public class CartOwnerShipAuthorizationHandler : AuthorizationHandler<CartOwnerShipRequirement, CartAuthorizationDTO>{
      
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CartOwnerShipRequirement requirement , CartAuthorizationDTO resource)
        {

            var user_id= context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(user_id) && user_id == resource.ownerId)
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }
}