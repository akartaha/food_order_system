using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Service
{

    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public int StatusCode { get; set; }
    }
}
