using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Modles
{
    public class OrderStatus
    {
   
    [Key]
    public int StatusId { get; set; }
    [Required]
    public OrderStatuss StatusName { get; set; } = OrderStatuss.Pending;

    [Required]
    public int OrderId { get; set; }
    public Orders order { get; set; }=null!;

    public DateTime OrderTime  { get; set; }=DateTime.UtcNow;

    }



}