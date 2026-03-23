using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;

namespace food_order_system1.Modles
{
    public class MenuCategory
    {
        [Key]
        public int CategoryId { get; set; }
        [Required,StringLength(100)]
        public string CategoryName { get; set;}=string.Empty;

        [Required]
        public int RestaurantId { get; set; }
        public Restaurant restaurant{ get; set; }=null!;

        public List<Item> menu_category_items { get; set; }=new();

        public bool IsDeleted { get; set; } = false;
    }
}