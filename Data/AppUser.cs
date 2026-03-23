using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using food_order_system1.Modles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace food_order_system1.Data
{
    public class AppUser : IdentityDbContext<ApplicationUser>
    {
       // constructor    
         public AppUser(DbContextOptions<AppUser> options) : base(options)
        {

        }

     public DbSet<Restaurant> restaurants{ get; set; }
     public DbSet<MenuCategory> menu_category{ get;set;}
     public DbSet<Item> items{ get; set; }
     public DbSet<Cart> carts{ get; set; }
     public DbSet<CartItem> cart_items{ get; set; }
     public DbSet<Orders> orders{ get; set; }
     public DbSet<OrderStatus> order_statuses{ get; set; }
     public DbSet<OrderItem> orderItems{ get; set; }

     public DbSet<RefreshToken> refreshTokens{ get; set; }

     public DbSet<RestauranManagerRequest> request_manager {get;set;}




     protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ✅ Create unique constraint on UserId + RestaurantId
        modelBuilder.Entity<Cart>()
            .HasIndex(c => new { c.UserId, c.RestaurantId })
            .IsUnique();
    }


    }
}