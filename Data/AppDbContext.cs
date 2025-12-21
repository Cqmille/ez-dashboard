using Microsoft.EntityFrameworkCore;
using MamyDashboard.Models;

namespace MamyDashboard.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Message> Messages => Set<Message>();
}
