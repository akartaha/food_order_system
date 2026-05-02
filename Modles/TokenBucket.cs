using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Modles
{
    public class TokenBucket
    {
        public double tokens { get; set; }
        public DateTime lastRefill { get; set; }
    }
}