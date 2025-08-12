using Microsoft.EntityFrameworkCore;
using OrderAPI.Models;
using System.Collections.Generic;

namespace OrderAPI.DataBase
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Order> Order { get; set; }
    }
}
