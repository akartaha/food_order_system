using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Modles
{
    public class Cart
    {
        [Key]
        public int CartId { get; set; }
        
        [Required,StringLength(100)]
        public string CartName{get;set;}=string.Empty;

        [Required]
        public string UserId {get; set; }=null!;
        public ApplicationUser User {get; set; }=null!;

        [Required]
        public int RestaurantId { get; set; }
        public Restaurant Restaurant{ get; set; }=null!;

        public List<CartItem> CartItem{get;set; }=new();

   

    }
}