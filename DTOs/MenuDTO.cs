using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using food_order_system1.Modles;

namespace food_order_system1.DTOs
{
    public class CreateMenuDTO
    {
        [Required,StringLength(100)]
        public string CategoryName { get; set;}=string.Empty;

        
    }

    public class UpdateMenuCategoryDTO
    {
  
        [Required,StringLength(100)]
        public string? CategoryName { get; set;}
        
    }


    public class GetMenuDTO
    {
        public int MenuCategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int RestaurantId { get; set; }
        public string UserId { get; set; }=string.Empty;
        public List<GetItemDTO> Items { get; set; } = new List<GetItemDTO>();
    }
}