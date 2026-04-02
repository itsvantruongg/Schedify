using Microsoft.EntityFrameworkCore;
using backend_test.Models;

namespace backend_test.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Calendar> Calendars { get; set; }
    public DbSet<Event> Events { get; set; }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<User>().HasIndex(u => u.Username).IsUnique();
        m.Entity<User>().HasIndex(u => u.Email).IsUnique();

        m.Entity<Session>()
            .HasOne(s => s.User).WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);

        m.Entity<Calendar>()
            .HasOne(c => c.User).WithMany(u => u.Calendars)
            .HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);

        m.Entity<Event>()
            .HasOne(e => e.Calendar).WithMany(c => c.Events)
            .HasForeignKey(e => e.CalendarId).OnDelete(DeleteBehavior.Cascade);

        m.Entity<Event>()
            .HasOne(e => e.User).WithMany()
            .HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction);

        //m.Entity<Event>()
        //    .HasIndex(e => new { e.UserId, e.SourceId })
        //    .IsUnique().HasFilter("[SourceId] != ''"); 
    }
}