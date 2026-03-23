using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Modles
{
    public class RestauranManagerRequest

    {
        [Key] 
        public int Id { get; set; }
        [Required]
        public string UserId { get; set; } = string.Empty;       // FK
        public ApplicationUser User { get; set; }=null!;      // Navigation property
        [Required]
        public string Status { get; set; } = "Pending";
         
         [Required]
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime AcceptedAt { get; set; } 
 
    }
}