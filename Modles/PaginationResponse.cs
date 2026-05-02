using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Modles
{
    public class PaginationResponse<T>
    {
        public int pageSize { get; set; }
        public int pageNumber { get; set; }
        public int totalCount { get; set;}
        public List<T> Data { get; set; } = new List<T>();

    }
}