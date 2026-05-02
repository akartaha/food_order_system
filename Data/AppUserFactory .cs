using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace food_order_system1.Data
{
    public class AppUserFactory : IDesignTimeDbContextFactory<AppUser>
    {
        public AppUser CreateDbContext(string[] args)
        {
            // Build configuration manually
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<AppUser>();

            optionsBuilder.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 26))
            );

            return new AppUser(optionsBuilder.Options);
        }
    }
}