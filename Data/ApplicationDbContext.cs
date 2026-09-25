using HotelManagement.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<HousekeepingTask> HousekeepingTasks { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<SubscriptionEvent> SubscriptionEvents { get; set; }
        public DbSet<ProcessedBillingEvent> ProcessedBillingEvents { get; set; }

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

            // Room numbers are unique within a hotel
            modelBuilder.Entity<Room>()
                .HasIndex(r => new { r.HotelId, r.RoomNumber })
                .IsUnique();

            // Reservation -> Payments: CASCADE (reservations with money on them can't be deleted anyway)
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Reservation)
                .WithMany(r => r.Payments)
                .HasForeignKey(p => p.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Payment -> CreatedBy: SET NULL so staff accounts can be removed without losing the ledger
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.CreatedBy)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // InventoryItem -> Hotel: CASCADE
            modelBuilder.Entity<InventoryItem>()
                .HasOne(i => i.Hotel)
                .WithMany()
                .HasForeignKey(i => i.HotelId)
                .OnDelete(DeleteBehavior.Cascade);

            // InventoryTransaction -> InventoryItem: CASCADE
            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.InventoryItem)
                .WithMany(i => i.Transactions)
                .HasForeignKey(t => t.InventoryItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // InventoryTransaction -> Room: SET NULL
            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.Room)
                .WithMany()
                .HasForeignKey(t => t.RoomId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // InventoryTransaction -> CreatedBy: RESTRICT
            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.CreatedBy)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // HousekeepingTask -> Room: CASCADE
            modelBuilder.Entity<HousekeepingTask>()
                .HasOne(h => h.Room)
                .WithMany()
                .HasForeignKey(h => h.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            // HousekeepingTask -> AssignedTo: SET NULL
            modelBuilder.Entity<HousekeepingTask>()
                .HasOne(h => h.AssignedTo)
                .WithMany()
                .HasForeignKey(h => h.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // HousekeepingTask -> CreatedBy: RESTRICT
            modelBuilder.Entity<HousekeepingTask>()
                .HasOne(h => h.CreatedBy)
                .WithMany()
                .HasForeignKey(h => h.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ApplicationUser -> Hotel (staff assignment): RESTRICT (optional, nullable FK)
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Hotel)
                .WithMany()
                .HasForeignKey(u => u.HotelId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Owner -> Subscription: one per owner, removed with the owner
            modelBuilder.Entity<Subscription>()
                .HasOne(s => s.Owner)
                .WithMany()
                .HasForeignKey(s => s.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Subscription>()
                .HasIndex(s => s.OwnerId)
                .IsUnique();

            // Subscription -> history: CASCADE
            modelBuilder.Entity<SubscriptionEvent>()
                .HasOne(e => e.Subscription)
                .WithMany(s => s.Events)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            // History entry -> the user who made the change: kept if that user is deleted
            modelBuilder.Entity<SubscriptionEvent>()
                .HasOne(e => e.Actor)
                .WithMany()
                .HasForeignKey(e => e.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // Normalize all DateTime values to UTC before writing to PostgreSQL timestamptz columns
            var utcConverter = new ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v == null ? v : v.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v.Value.ToUniversalTime(),
                v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                        property.SetValueConverter(utcConverter);
                    else if (property.ClrType == typeof(DateTime?))
                        property.SetValueConverter(nullableUtcConverter);
                }
            }
        }
    }
}