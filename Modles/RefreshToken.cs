using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Modles
{
    public class RefreshToken
    {   
        [Key]
        public int Id {get;set;}
        [Required]
        public string Token { get; set;}=string.Empty;
        [Required]
        public string UserId { get; set;}=null!;
        public ApplicationUser User { get; set;}=new ApplicationUser();
        public DateTime ExpiresAt { get; set;}
        public bool IsRevoked {get; set;}=false;
    }
}