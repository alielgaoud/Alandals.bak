using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<ContractItem> ContractItems { get; set; }
        public DbSet<ContractDocument> ContractDocuments { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<VisitorPass> VisitorPasses { get; set; } // 👈 جديد
        public DbSet<EntryLog> EntryLogs { get; set; }         // 👈 جديد
        public DbSet<Setting> Settings { get; set; }           // 👈 جديد
        public DbSet<NumberSequence> NumberSequences { get; set; } // 👈 جديد
        public DbSet<Refund> Refunds { get; set; }
        public DbSet<VisitorBlacklist> VisitorBlacklists { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User Configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasIndex(e => e.UserName).IsUnique(); // 👈 الفهرس الفريد أصبح لاسم المستخدم لمنع التكرار
                entity.Property(e => e.Role).HasConversion<int>();

                entity.HasOne(u => u.Tenant)
                      .WithMany()
                      .HasForeignKey(u => u.TenantId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<VisitorBlacklist>(entity =>
            {
                entity.ToTable("VisitorBlacklists");
                entity.HasIndex(e => e.Phone);
                entity.HasIndex(e => e.NationalId);
            });

            // Unit Configuration
            modelBuilder.Entity<Unit>(entity =>
            {
                entity.ToTable("Units");
                entity.HasIndex(e => e.UnitNumber).IsUnique();
                entity.Property(e => e.ActivityType).HasConversion<int>(); // 👈 تم التغيير
                entity.Property(e => e.Status).HasConversion<int>();
                entity.Property(e => e.Area).HasColumnType("decimal(10,2)");
                entity.Property(e => e.ElectricityMeterStart).HasColumnType("decimal(12,2)");
                // 👈 تم حذف سطر WaterMeterStart
            });

            // Tenant
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasIndex(e => e.NationalId).IsUnique();
            });

            // Setting
            modelBuilder.Entity<Setting>(entity =>
            {
                entity.ToTable("Settings");
                entity.HasIndex(e => e.SettingKey).IsUnique();
            });

            // NumberSequence
            modelBuilder.Entity<NumberSequence>(entity =>
            {
                entity.ToTable("NumberSequences");
                entity.HasIndex(e => e.SequenceKey).IsUnique();
            });

            // Contract
            modelBuilder.Entity<Contract>(entity =>
            {
                entity.ToTable("Contracts");
                entity.HasIndex(e => e.ContractNumber).IsUnique();
                entity.Property(e => e.RentAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DepositAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.RentCycle).HasConversion<int>();
                entity.Property(e => e.Status).HasConversion<int>();

                entity.HasOne(c => c.Tenant)
                      .WithMany()
                      .HasForeignKey(c => c.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Unit)
                      .WithMany()
                      .HasForeignKey(c => c.UnitId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ContractItem
            modelBuilder.Entity<ContractItem>(entity =>
            {
                entity.ToTable("ContractItems");
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");

                entity.HasOne(i => i.Contract)
                      .WithMany(c => c.ContractItems)
                      .HasForeignKey(i => i.ContractId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ContractDocument
            modelBuilder.Entity<ContractDocument>(entity =>
            {
                entity.ToTable("ContractDocuments");

                entity.HasOne(d => d.Contract)
                      .WithMany(c => c.ContractDocuments)
                      .HasForeignKey(d => d.ContractId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Payment
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.ToTable("Payments");
                entity.HasIndex(e => e.ReceiptNumber).IsUnique();
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PaymentType).HasConversion<int>();
                entity.Property(e => e.PaymentMethod).HasConversion<int>();

                entity.HasOne(p => p.Contract)
                      .WithMany()
                      .HasForeignKey(p => p.ContractId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // MaintenanceRequest
            modelBuilder.Entity<MaintenanceRequest>(entity =>
            {
                entity.ToTable("MaintenanceRequests");
                entity.HasIndex(e => e.RequestNumber).IsUnique();
                entity.Property(e => e.Cost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Type).HasConversion<int>();
                entity.Property(e => e.Priority).HasConversion<int>();
                entity.Property(e => e.Status).HasConversion<int>();

                entity.HasOne(m => m.Unit)
                      .WithMany()
                      .HasForeignKey(m => m.UnitId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Tenant)
                      .WithMany()
                      .HasForeignKey(m => m.TenantId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Refund
            modelBuilder.Entity<Refund>(entity =>
            {
                entity.ToTable("Refunds");
                entity.HasIndex(e => e.RefundNumber).IsUnique();
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.RefundType).HasConversion<int>();
                entity.Property(e => e.RefundMethod).HasConversion<int>();

                entity.HasOne(r => r.Contract)
                      .WithMany()
                      .HasForeignKey(r => r.ContractId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Expense
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.ToTable("Expenses");
                entity.HasIndex(e => e.ExpenseNumber).IsUnique();
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ExpenseType).HasConversion<int>();

                entity.HasOne(e => e.Unit)
                      .WithMany()
                      .HasForeignKey(e => e.UnitId)
                      .OnDelete(DeleteBehavior.SetNull);

                // 👈 الجديد: علاقة المصروف بالمستأجر عند تحميل التكلفة عليه
                entity.HasOne(e => e.Tenant)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.SetNull);
            });
            // 👈 VisitorPass
            modelBuilder.Entity<VisitorPass>(entity =>
            {
                entity.ToTable("VisitorPasses");
                entity.HasIndex(e => e.PassCode).IsUnique();
                entity.Property(e => e.VisitorType).HasConversion<int>();
                entity.Property(e => e.Status).HasConversion<int>();

                entity.HasOne(p => p.Unit)
                      .WithMany()
                      .HasForeignKey(p => p.UnitId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // 👈 EntryLog
            modelBuilder.Entity<EntryLog>(entity =>
            {
                entity.ToTable("EntryLogs");

                entity.HasOne(l => l.VisitorPass)
                      .WithMany(p => p.EntryLogs)
                      .HasForeignKey(l => l.VisitorPassId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}