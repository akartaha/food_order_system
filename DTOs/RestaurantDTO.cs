using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using food_order_system1.Modles;

namespace food_order_system1.DTOs
{
    public class CreateRestaurantDTO
    {
        [Required, StringLength(100)]
        public string Restaurant_Name { get; set; } = string.Empty;
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;
        [Required, StringLength(150)]
        public string Address { get; set; } = string.Empty;
        public string? ManagerId {get;set;}



    }

    public class UpdateRestaurantDTO
    {
        [StringLength(100)]
        public string? Restaurant_Name { get; set; }
        [StringLength(200)]
        public string? Description { get; set; }
        [StringLength(150)]
        public string? Address { get; set; }
    }
    public class viewRestauantDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Discription { get; set; } = string.Empty;
        public string address { get; set; } = string.Empty;
        public string Open { get; set; } = string.Empty;
    }

      public class viewRestaurantAndMenuDTO
    {
        public string restaurantName { get; set; } = string.Empty;
        public string Discription { get; set; } = string.Empty;
        public string address { get; set; } = string.Empty;
        public string Open { get; set; } = string.Empty;
        public RestaurantStatuss restaurantStatus {get; set;} 
        public bool IsDeleted {get;set;}
        public List<ViewRestaurantMenuMenuDTO> Menus{get;set;}=new();
    }

    public class ViewRestaurantMenuMenuDTO
    {
           public string  MenuName {get;set;}=string.Empty;
  
           public List<ViewRestaurantMenuItemsDTO> Items{set;get;} =new();
           //Items = r.menu_category_items.Where(r => r.IsActive && !r.IsDeleted)
    }
    public class ViewRestaurantMenuItemsDTO
    {
           public string  ItemName {get;set;}=string.Empty;
           public double  ItemPrice {get;set;}
    }


    public class RestaurantAuthorizationDTO{
        public int RestaurantId{get;set;}
        public string ownerId{get;set;}=string.Empty;
    }


    public class GetRestaurantCacheDTO
    {
         public int RestaurantId{get;set;}
         public string restaurantName {get;set;}=string.Empty;
           public string ownerId{get;set;}=string.Empty;
        
    }


}