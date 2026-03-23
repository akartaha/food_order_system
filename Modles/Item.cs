using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Modles
{
    public class Item
    {
        [Key]
        public int ItemId { get; set; }
        [Required,StringLength(100)]
        public string ItemName { get; set; }=string.Empty;
        [Required,Range(0.01, 9999999999999, ErrorMessage = "Price must be greater than 0")]
        public decimal ItemPrice { get; set;}
        
        public bool IsActive { get; set; }=true;
        [Required]
        public int MenuCategoryId { get; set; }
        public MenuCategory MenuCategory{ get; set; }=null!;

        public bool IsDeleted { get; set; } = false;
        
    }
}