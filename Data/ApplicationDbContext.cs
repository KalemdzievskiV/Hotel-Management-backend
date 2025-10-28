using HotelManagement.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets for all entities
        public DbSet<Hotel> Hotels { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Guest> Guests { get; set; }
        public DbSet<Reservation> Reservations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure cascade delete behavior to avoid cycles
            
            // Hotel -> Rooms: CASCADE (deleting hotel deletes rooms)
            modelBuilder.Entity<Room>()
                .HasOne(r => r.Hotel)
                .WithMany(h => h.Rooms)
                .HasForeignKey(r => r.HotelId)
                .OnDelete(DeleteBehavior.Cascade);

            // Room -> Reservations: CASCADE (deleting room deletes reservations)
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Room)
                .WithMany(rm => rm.Reservations)
                .HasForeignKey(r => r.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            // Hotel -> Reservations: NO ACTION (prevent multiple cascade paths)
            // When hotel is deleted, rooms are deleted first (cascade), which then deletes reservations
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Hotel)
                .WithMany(h => h.Reservations)
                .HasForeignKey(r => r.HotelId)
                .OnDelete(DeleteBehavior.NoAction);

            // Guest -> User (registered user link): RESTRICT (optional, nullable FK)
            modelBuilder.Entity<Guest>()
                .HasOne(g => g.User)
                .WithMany()
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Guest -> Hotel (walk-in guest ownership): RESTRICT (optional, nullable FK)
            modelBuilder.Entity<Guest>()
                .HasOne(g => g.Hotel)
                .WithMany()
                .HasForeignKey(g => g.HotelId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Guest -> CreatedBy User (who created the walk-in guest): RESTRICT (optional, nullable FK)
            modelBuilder.Entity<Guest>()
                .HasOne(g => g.CreatedBy)
                .WithMany()
                .HasForeignKey(g => g.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Guest -> Reservations: CASCADE (deleting guest deletes their reservations)
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Guest)
                .WithMany(g => g.Reservations)
                .HasForeignKey(r => r.GuestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Reservation -> CreatedBy User (who created the reservation): RESTRICT
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.CreatedBy)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(true);

            // ApplicationUser -> Hotel (staff assignment): RESTRICT (optional, nullable FK)
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Hotel)
                .WithMany()
                .HasForeignKey(u => u.HotelId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        }
    }
}