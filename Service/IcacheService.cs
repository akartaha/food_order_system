using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace food_order_system1.Service
{
    public interface IcacheService
    {  
        Task UpdateKeyVersionForMenu(int menu_category_id);
        Task UpdateKeyVersionForItem(int item_id);
        Task UpdateKeyVersionForItemPagiation(int? res_id);
       Task UpdateKeyVersionForCartPagiation(string user_id);
       Task UpdateKeyVersionForMenuPagination(int restaurant_id );
       Task UpdateKeyVersionForRestaurantPagination(int restaurant_id );
       Task UpdateKeyVersionForRestaurant(string user_id );
    }

    public class CacheService : IcacheService
    {

        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _redisCache;
        private readonly ILogger<CacheService> _logger;

        public CacheService(
        IMemoryCache memoryCache,
        IDistributedCache redisCache,
        ILogger<CacheService> logger)
    {
        _memoryCache = memoryCache;
        _redisCache = redisCache;
        _logger = logger;
    }

       

      


        public async Task UpdateKeyVersionForCartPagiation(string user_id)
        {
                await _redisCache.SetStringAsync($"Carts_Version_User{user_id}", Guid.NewGuid().ToString());
            

            _logger.LogInformation($"key version is update for cart pagination");
            
        }

        public async Task UpdateKeyVersionForMenuPagination(int restaurant_id)
        {
        
                await _redisCache.SetStringAsync($"Menues_Version_Restaurant_{restaurant_id}", Guid.NewGuid().ToString());
        

            _logger.LogInformation($"key version is update for item pagination");  
           
        }

        public async Task UpdateKeyVersionForItem(int item_id)
        {
            await _redisCache.SetStringAsync($"Item_Version_{item_id}", Guid.NewGuid().ToString());
    
            _logger.LogInformation($"key version is update for item ");
        }

        public async Task UpdateKeyVersionForItemPagiation(int? res_id)
        {
          await _redisCache.SetStringAsync("Items_Version_Global", Guid.NewGuid().ToString());

           if (res_id.HasValue)
            {
                await _redisCache.SetStringAsync($"Items_Version_Restaurant_{res_id}", Guid.NewGuid().ToString());
            }

            _logger.LogInformation($"key version is update for item pagination");
        }

        public async Task UpdateKeyVersionForMenu(int menu_category_id)
        {
              await _redisCache.SetStringAsync($"Menu_Version_{menu_category_id}", Guid.NewGuid().ToString());
    
            _logger.LogInformation($"key version is update for menu ");

           
        }

        public async Task UpdateKeyVersionForRestaurantPagination(int restaurant_id)
        {
            await _redisCache.SetStringAsync($"Restaurant_Version_{restaurant_id}", Guid.NewGuid().ToString());
    
            _logger.LogInformation($"key version is update for restaurant pagination ");
        }

        public async Task UpdateKeyVersionForRestaurant(string user_id)
        {
           await _redisCache.SetStringAsync($"User_Restaurant_Version_{user_id}", Guid.NewGuid().ToString());
    
            _logger.LogInformation($"key version is update for restaurant  "); 
        }
    }
}