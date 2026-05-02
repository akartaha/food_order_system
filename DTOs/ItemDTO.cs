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

        [Required]
        public string ItemName { get; set; } = string.Empty;

        [Required, Range(0.01, 9999999999999, ErrorMessage = "Price must be greater than 0")]
        public decimal ItemPrice { get; set; }


    }
    public class UpdateItemDTO
    {

        public string ItemName { get; set; } = string.Empty;

        [Range(0.01, 9999999999999, ErrorMessage = "Price must be greater than 0")]
        public decimal ItemPrice { get; set; }

    }
    public class ViewItemDTO
    {
        public string item_name { get; set; } = string.Empty;
        public int quantity { get; set; }
        public double price { get; set; }
    }

    public class ViewMostSelingItemDTO
    {
        public string itemName { get; set; } = string.Empty;
        public string restaurantName { get; set; } = string.Empty;
        public int timeOfOrders = 0;


    }

    public class ViewItemOrderItemDTO
    {
        public string item_name { get; set; } = string.Empty;
        public int quantity = 0;
        public DateTime timecreated { get; set; }
    }


    public class GetItemDTO
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal ItemPrice { get; set; }
        public int MenuId { get; set; }
        public int RestaurantId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        public bool IsAvailable { get; set; } = true;
    }

    public class ItemAuthorizationDTO
    {
        public int itemId { get; set; }
        public string ownerId { get; set; } = string.Empty;
    }


}