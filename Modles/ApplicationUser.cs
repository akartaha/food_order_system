using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace food_order_system1.Modles
{
    public class ApplicationUser : IdentityUser
    { 
            
        [Required]
        public int userId { get; set; }
        [Required]
        public string fullName { get; set;}=String.Empty;
        public bool IsActive {get; set;}=false;

        
    }
}