using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using food_order_system1.Modles;

namespace food_order_system1.DTOs
{
    public class CreateOrderDTO
    {
        [Required]
        public string addrees { get; set; } = string.Empty;
    }


    public class ViewOrderOrderItemDTO
    {
         public string user {get;set;}=string.Empty;
         public int cart {get;set;}
         public string restaurant {get;set;} = string.Empty;
         public OrderStatuss statis {get;set;}
         public string  address {get;set;}=string.Empty;
         public DateTime time {get;set;}=DateTime.UtcNow;
        public List<ViewItemDTO> items  {get;set;}=new();
        public double totalPrice {get;set;}
    }

    public class ViewOrderDTO
    {
          public string UserName {get;set;}=string.Empty;
          public string restaurant {get;set;} = string.Empty;
          public DateTime time {get;set;}=DateTime.UtcNow;
          public double OrderPrice {get;set;}

    
    }
    public class ViewOrderStatisticDTO
    {
        
             public List<ViewOrderDTO> orders {get;set;}=new();
              public string from {get;set;}=string.Empty;
               public string to {get;set;}=string.Empty;
               public int OrderNumbers = 0;
    }

    public class ViewRestaurantOrderDTO
    { 
        public String RestaurantName {get;set;}=string.Empty;
          public List<ViewItemOrderItemDTO> Orders =new();
        
    }
}