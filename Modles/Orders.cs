using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Modles
{
    public class Orders
    { 
        [Key]
        public int OrderId { get; set; }

         [Required]
        public string UserId { get; set; }=null!;
        public ApplicationUser User {get;set;}=null!;

        [Required]
        public int CartId{get;set;}
        public Cart Cart{get;set;}=null!;

         [Required]
        public int RestaurantId { get; set; }
        public Restaurant Restaurant{ get; set; }=null!;
   
        [Required]
        public OrderStatuss Status { get; set; } = OrderStatuss.Pending;
       
        [Required]
        public string Address { get; set; }=string.Empty;

        [Required]
        public DateTime CreateAt{get;set; }=DateTime.UtcNow;

        [Required]
        public decimal TotalPrice {get;set; }

        public List<OrderItem> OrderItems { get; set; }=new();
    }

    
       public enum OrderStatuss
        {
            Pending ,
            Accepted ,
            Delivered 
        }
}