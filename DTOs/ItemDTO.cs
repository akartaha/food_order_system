using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using food_order_system1.Modles;

namespace food_order_system1.DTOs
{
    public class CreateItemDTO
    {
        
        public string ItemName { get; set; }=string.Empty;

        [Required,Range(0.01, 9999999999999, ErrorMessage = "Price must be greater than 0")]
        public decimal ItemPrice { get; set;}
        
    
    }
    public class UpdateItemDTO
    {
        
        public string ItemName { get; set; }=string.Empty;

        [Range(0.01, 9999999999999, ErrorMessage = "Price must be greater than 0")]
        public decimal ItemPrice { get; set;}  

    }
    public class ViewItemDTO
    {
         public string item_name { get; set; }=string.Empty;
         public int quantity { get; set; }
         public double price { get; set; }
    }

   public class ViewMostSelingItemDTO
    {
       public string  item_name {get; set; } =string.Empty;
            public string    restaurantName {get; set; }=string.Empty;
            public int    time_of_orders = 0;
        
    }

    public class ViewItemOrderItemDTO{
         public string  item_name {get; set; } =string.Empty;
        public int   quantity=0;
        public DateTime   timecreated  {get; set; } 
    }

}