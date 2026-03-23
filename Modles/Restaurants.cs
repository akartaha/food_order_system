using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Modles
{
    public class Restaurant
    {
        [Key]
        public int RestaurantId { get; set; }
        [Required,StringLength(50)]
        public string Restaurant_Name { get;set;}=string.Empty;
        [StringLength(150)]
        public string Description { get; set; }=string.Empty;
        [Required,StringLength(100)]
        public string Address { get; set; }=string.Empty ;
        public bool IsOpen { get; set; }=true;
        [Required]
        public string UserId { get; set; }=string.Empty;
        public ApplicationUser User { get; set; }=null!;

        public RestaurantStatuss RestaurantStatus {get;set; }=RestaurantStatuss.Pending;
        
       // public int menu_category_id { get; set; }
        public List<MenuCategory> Category { get; set; }=new();

        public bool IsDeleted { get; set; } = false;

        public static explicit operator Restaurant(List<object> v)
        {
            throw new NotImplementedException();
        }
    }
   public enum RestaurantStatuss
        {
            Pending ,
            Accepted ,
            Regected 
        }

}