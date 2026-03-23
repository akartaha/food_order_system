using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using food_order_system1.Modles;

namespace food_order_system1.DTOs
{
    public class CreateCartDTO
    {
        [Required, StringLength(100)]
        public string CartName { get; set; } = string.Empty;


    }

    public class AddItemToCartDTO
    {
        [Required, Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000")]
        public int Quantity { get; set; }
        [Required]
        public int ItemId { get; set; }

    }
    public class UpdateCartItem
    {
        [Required, Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000")]
        public int NewQuantity { get; set; }

    }
    public class ViewCartItemDTO
    {
        [Required]
        public string CartName { get; set; } = string.Empty;
         [Required]
        public string RestaurantName { get; set; } = string.Empty;
        public List<viewCartItemItemDTO> Items { get; set; } = new();
         [Required]
        public string Username{get;set;}=string.Empty;
         [Required]
        public double cartTotalPrice { get; set; }
    }

    public class viewCartItemItemDTO
    {
         [Required]
        public string ItemName { get; set; } = string.Empty;
         [Required]
        public decimal ItemPrice { get; set; }   
         [Required]   
        public int Quantity { get; set; }
    }
}