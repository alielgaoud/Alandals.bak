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
        public DbSet<UserPermission> UserPermissions { get; set; }
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
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<ComplaintReply> ComplaintReplies { get; set; }
        public DbSet<ContractFee> ContractFees { get; set; }
        public DbSet<BankTransferRequest> BankTransferRequests { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PushSubscription> PushSubscriptions { get; set; }
        public DbSet<NotificationPreference> NotificationPreferences { get; set; }


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


            // Notification
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.Property(e => e.Type).HasConversion<int>();
                entity.Property(e => e.Priority).HasConversion<int>();
                entity.Property(e => e.Channel).HasConversion<int>();

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.IsRead);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(n => n.User)
                      .WithMany()
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(n => n.Tenant)
                      .WithMany()
                      .HasForeignKey(n => n.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // PushSubscription
            modelBuilder.Entity<PushSubscription>(entity =>
            {
                entity.ToTable("PushSubscriptions");
                entity.HasIndex(e => e.Endpoint).IsUnique();

                entity.HasOne(p => p.User)
                      .WithMany()
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.Tenant)
                      .WithMany()
                      .HasForeignKey(p => p.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // NotificationPreference
            modelBuilder.Entity<NotificationPreference>(entity =>
            {
                entity.ToTable("NotificationPreferences");
                entity.Property(e => e.NotificationType).HasConversion<int>();

                entity.HasIndex(e => new { e.UserId, e.TenantId, e.NotificationType }).IsUnique();

                entity.HasOne(np => np.User)
                      .WithMany()
                      .HasForeignKey(np => np.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(np => np.Tenant)
                      .WithMany()
                      .HasForeignKey(np => np.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ContractFee>(entity =>
            {
                entity.ToTable("ContractFees");
                entity.Property(e => e.Value).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ValueType).HasConversion<int>();
                entity.Property(e => e.Frequency).HasConversion<int>();

                entity.HasOne(f => f.Contract)
                      .WithMany(c => c.ContractFees)
                      .HasForeignKey(f => f.ContractId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BankTransferRequest>(entity =>
            {
                entity.ToTable("BankTransferRequests");
                entity.Property(e => e.RequestedAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ApprovedAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Status).HasConversion<int>();

                entity.HasOne(r => r.Tenant)
                      .WithMany()
                      .HasForeignKey(r => r.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== أضف هذا داخل OnModelCreating =====

            // Complaint
            modelBuilder.Entity<Complaint>(entity =>
            {
                entity.ToTable("Complaints");
                entity.Property(e => e.Status).HasConversion<int>();

                entity.HasOne(c => c.Tenant)
                      .WithMany()
                      .HasForeignKey(c => c.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.User)
                      .WithMany()
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ComplaintReply
            modelBuilder.Entity<ComplaintReply>(entity =>
            {
                entity.ToTable("ComplaintReplies");

                entity.HasOne(r => r.Complaint)
                      .WithMany(c => c.Replies)
                      .HasForeignKey(r => r.ComplaintId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.RepliedByUser)
                      .WithMany()
                      .HasForeignKey(r => r.RepliedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
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
                entity.Property(e => e.Status).HasConversion<int>();
                entity.Property(e => e.Area).HasColumnType("decimal(10,2)");
                entity.Property(e => e.ElectricityMeterStart).HasColumnType("decimal(12,2)");
                // 👈 تم حذف سطر WaterMeterStart
            });

            modelBuilder.Entity<UserPermission>(entity =>
            {
                entity.ToTable("UserPermissions");
                entity.HasIndex(e => new { e.UserId, e.PermissionKey }).IsUnique(); // منع تكرار نفس الصلاحية للمستخدم

                entity.HasOne(up => up.User)
                      .WithMany(u => u.Permissions)
                      .HasForeignKey(up => up.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
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
                entity.Property(e => e.ActivityType).HasConversion<int>();

                entity.HasOne(c => c.Tenant)
                      .WithMany()
                      .HasForeignKey(c => c.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Unit)
                      .WithMany()
                      .HasForeignKey(c => c.UnitId)
                      .OnDelete(DeleteBehavior.Restrict);

                // 👈 ربط العقد الجديد بالعقد السابق (Parent/Child Contract)
                entity.HasOne(c => c.ParentContract)
                      .WithMany()
                      .HasForeignKey(c => c.ParentContractId)
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