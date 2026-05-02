using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using food_order_system1.Modles;
using static food_order_system1.Controllers.OrderController;

namespace food_order_system1.Flters
{
    public class ItemFilter
    {
        public int? MinPrice { get; set; }
        public int? MaxPrice { get; set; }
        public string? SortBy { get; set; }
        public bool? FromLowToHigh { get; set; }
        public int? RestaurantId { get; set; }
  
    }

     public class CartFilter
    {
        public int? cartId { get; set; }
        public string? cartName { get; set; }
         public int? RestaurantId { get; set; }
        public string? SortBy { get; set; }
        public bool? FromLowToHigh { get; set; }
       
    }
 
  
   public class MenuFilter
    {
         public int? MenuId { get; set; }
        public string? MenuName { get; set; }
         public int? RestaurantId { get; set; }
        public string? SortBy { get; set; }
        public bool? FromLowToHigh { get; set; }  
    }

  public class OrderFilter
    {
        public int? orderId { get; set; }
        public string? userName { get; set; }
        public string? restaurantName { get; set; }
         public int? RestaurantId { get; set; }
        public string? SortBy { get; set; }
        public bool? FromLowToHigh { get; set; }  

        public OrderStatuss Status {get;set;}=OrderStatuss.Pending;
        
    }

    public class RestaurantFilter
    {
        public int? restaurantId { get; set; }
        public string? Adress { get; set; }
        public string? restaurantName { get; set; }
        public string? SortBy { get; set; }
        public bool? FromLowToHigh { get; set; }  
        public RestaurantStatuss Status {get;set;}=RestaurantStatuss.Accepted;  
    }

  public class UserFilter
    {
       public string? userId {get;set;} 
       public string? FullName {get;set;}
       public string? email {get;set;}
       public bool? IsActive {get;set;}

         public string? SortBy { get; set; }
        public bool? FromLowToHigh { get; set; }

        public UserRolee role {get;set;}

    }

    
}