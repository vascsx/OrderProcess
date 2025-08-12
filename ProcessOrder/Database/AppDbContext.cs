using Microsoft.EntityFrameworkCore;
using ProcessOrder.Models;
using System.Collections.Generic;

namespace ProcessOrder.DataBase
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
