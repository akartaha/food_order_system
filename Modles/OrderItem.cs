using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Modles
{
    public class OrderItem
    {
        [Key]

        public int OrdertemId{get;set;}
         [Required,Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000")]
        public int  Quantity{get;set;}
        
         [Required]
        public int OrderId{get;set;}
        public Orders Order{get;set;}=null!;
      
         [Required]
        public int ItemId {get;set;}
        public Item Item{get;set; }=null!;
        

         [Required]
        public decimal PriceAtPurchase { get; set; } // snapshot
    }



}