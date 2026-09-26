using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Security.Claims;
using System.Text.Json;

namespace Andalos.API.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // ===== 1. الجداول (DbSets) =====
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
        public DbSet<VisitorPass> VisitorPasses { get; set; }
        public DbSet<EntryLog> EntryLogs { get; set; }
        public DbSet<Setting> Settings { get; set; }
        public DbSet<NumberSequence> NumberSequences { get; set; }
        public DbSet<Refund> Refunds { get; set; }
        public DbSet<VisitorBlacklist> VisitorBlacklists { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<ComplaintReply> ComplaintReplies { get; set; }
        public DbSet<ContractFee> ContractFees { get; set; }
        public DbSet<BankTransferRequest> BankTransferRequests { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PushSubscription> PushSubscriptions { get; set; }
        public DbSet<NotificationPreference> NotificationPreferences { get; set; }
        public DbSet<PassTransaction> PassTransactions { get; set; }
        public DbSet<TenantSettlement> TenantSettlements { get; set; }
        public DbSet<GatekeeperShift> GatekeeperShifts { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; } // 👈 جدول سجل التدقيق والمراقبة
        public DbSet<PermissionPackage> PermissionPackages { get; set; }
        public DbSet<PermissionPackageItem> PermissionPackageItems { get; set; }
        public DbSet<UserPermissionPackage> UserPermissionPackages { get; set; }
        public DbSet<Circular> Circulars { get; set; }
        public DbSet<TenantCharge> TenantCharges { get; set; } // 👈 جديد: متعلقات المستأجر (تحميلات الصيانة والخدمات)

        // ===== 2. التكوينات والعلاقات (OnModelCreating) =====
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User Configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasIndex(e => e.UserName).IsUnique();
                entity.Property(e => e.Role).HasConversion<int>();

                entity.HasOne(u => u.Tenant)
                      .WithMany()
                      .HasForeignKey(u => u.TenantId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // UserPermission
            modelBuilder.Entity<UserPermission>(entity =>
            {
                entity.ToTable("UserPermissions");
                entity.HasIndex(e => new { e.UserId, e.PermissionKey }).IsUnique();

                entity.HasOne(up => up.User)
                      .WithMany(u => u.Permissions)
                      .HasForeignKey(up => up.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Circular
            modelBuilder.Entity<Circular>(entity =>
            {
                entity.ToTable("Circulars");
                entity.Property(e => e.Priority).HasConversion<int>();
                entity.HasIndex(e => e.IsPublished);
                entity.HasIndex(e => e.PublishAt);
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

            modelBuilder.Entity<PermissionPackage>(entity =>
            {
                entity.ToTable("PermissionPackages");
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<PermissionPackageItem>(entity =>
            {
                entity.ToTable("PermissionPackageItems");
                entity.HasIndex(e => new { e.PackageId, e.PermissionKey }).IsUnique();

                entity.HasOne(i => i.Package)
                      .WithMany(p => p.Items)
                      .HasForeignKey(i => i.PackageId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserPermissionPackage>(entity =>
            {
                entity.ToTable("UserPermissionPackages");
                entity.HasIndex(e => new { e.UserId, e.PackageId }).IsUnique();

                entity.HasOne(x => x.User)
                      .WithMany()
                      .HasForeignKey(x => x.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Package)
                      .WithMany(p => p.Users)
                      .HasForeignKey(x => x.PackageId)
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

            // ContractFee
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

            // BankTransferRequest
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

            // VisitorBlacklist
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

                entity.Property(m => m.BilledAmount).HasColumnType("decimal(18,2)");
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

                entity.HasOne(e => e.Tenant)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.SetNull);

                // 👈 جديد: ربط المصروف بطلب الصيانة (تكلفة الصيانة المسجلة تلقائياً)
                entity.HasOne<MaintenanceRequest>()
                      .WithMany()
                      .HasForeignKey(e => e.MaintenanceRequestId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // 👈 جديد: متعلقات المستأجر (تحميلات الصيانة والخدمات)
            modelBuilder.Entity<TenantCharge>(entity =>
            {
                entity.ToTable("TenantCharges");
                entity.HasIndex(e => e.ChargeNumber).IsUnique();
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SettledAmount).HasColumnType("decimal(18,2)");

                entity.HasOne(c => c.Tenant)
                      .WithMany()
                      .HasForeignKey(c => c.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Unit)
                      .WithMany()
                      .HasForeignKey(c => c.UnitId)
                      .OnDelete(DeleteBehavior.SetNull);

                // 👈 ربط بطلب الصيانة (التحميل الناتج عن صيانة)
                entity.HasOne(c => c.MaintenanceRequest)
                      .WithMany()
                      .HasForeignKey(c => c.MaintenanceRequestId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(c => c.MaintenanceRequestId);
                entity.HasIndex(c => c.TenantId);
                entity.HasIndex(c => c.IsSettled);
            });

            // VisitorPass
            modelBuilder.Entity<VisitorPass>(entity =>
            {
                entity.ToTable("VisitorPasses");
                entity.HasIndex(e => e.PassCode).IsUnique();
                entity.Property(e => e.VisitorType).HasConversion<int>();
                entity.Property(e => e.Status).HasConversion<int>();
                entity.Property(e => e.WalletStatus).HasConversion<int>();

                entity.HasOne(p => p.Unit)
                      .WithMany()
                      .HasForeignKey(p => p.UnitId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(p => p.IssuedByUser)
                      .WithMany()
                      .HasForeignKey(p => p.IssuedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // PassTransaction
            modelBuilder.Entity<PassTransaction>(entity =>
            {
                entity.ToTable("PassTransactions");

                entity.HasOne(t => t.VisitorPass)
                      .WithMany(p => p.Transactions)
                      .HasForeignKey(t => t.VisitorPassId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.Tenant)
                      .WithMany()
                      .HasForeignKey(t => t.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Settlement)
                      .WithMany(s => s.Transactions)
                      .HasForeignKey(t => t.SettlementId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // TenantSettlement
            modelBuilder.Entity<TenantSettlement>(entity =>
            {
                entity.ToTable("TenantSettlements");
                entity.Property(e => e.SettlementMethod).HasConversion<int>();

                entity.HasOne(s => s.Tenant)
                      .WithMany()
                      .HasForeignKey(s => s.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.ProcessedByUser)
                      .WithMany()
                      .HasForeignKey(s => s.ProcessedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // GatekeeperShift
            modelBuilder.Entity<GatekeeperShift>(entity =>
            {
                entity.ToTable("GatekeeperShifts");

                entity.HasOne(s => s.User)
                      .WithMany()
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // EntryLog
            modelBuilder.Entity<EntryLog>(entity =>
            {
                entity.ToTable("EntryLogs");

                entity.HasOne(l => l.VisitorPass)
                      .WithMany(p => p.EntryLogs)
                      .HasForeignKey(l => l.VisitorPassId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }

        // =========================================================
        // 3. تتبع الحركات تلقائياً (Audit Trail Engine)
        // =========================================================
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var auditEntries = OnBeforeSaveChanges();
            var result = await base.SaveChangesAsync(cancellationToken);
            await OnAfterSaveChanges(auditEntries);
            return result;
        }

        private List<AuditEntry> OnBeforeSaveChanges()
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();

            int? userId = null;
            var userIdString = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdString, out int parsedId))
                userId = parsedId;

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry)
                {
                    TableName = entry.Entity.GetType().Name,
                    UserId = userId
                };
                auditEntries.Add(auditEntry);

                foreach (var property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.AuditType = AuditType.Create;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            break;

                        case EntityState.Deleted:
                            auditEntry.AuditType = AuditType.Delete;
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified && property.OriginalValue?.ToString() != property.CurrentValue?.ToString())
                            {
                                auditEntry.ChangedColumns.Add(propertyName);
                                auditEntry.AuditType = AuditType.Update;
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }
            }

            foreach (var auditEntry in auditEntries.Where(_ => !_.HasTemporaryProperties))
            {
                AuditLogs.Add(auditEntry.ToAudit());
            }

            return auditEntries.Where(_ => _.HasTemporaryProperties).ToList();
        }

        private Task OnAfterSaveChanges(List<AuditEntry> auditEntries)
        {
            if (auditEntries == null || auditEntries.Count == 0)
                return Task.CompletedTask;

            foreach (var auditEntry in auditEntries)
            {
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }
                AuditLogs.Add(auditEntry.ToAudit());
            }

            return SaveChangesAsync();
        }
    }

    // =========================================================
    // 4. الكلاس المساعد لترجمة كائنات الـ Audit
    // =========================================================
    public class AuditEntry
    {
        public AuditEntry(EntityEntry entry) { Entry = entry; }
        public EntityEntry Entry { get; }
        public int? UserId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public Dictionary<string, object?> KeyValues { get; } = new();
        public Dictionary<string, object?> OldValues { get; } = new();
        public Dictionary<string, object?> NewValues { get; } = new();
        public AuditType AuditType { get; set; }
        public List<string> ChangedColumns { get; } = new();
        public List<PropertyEntry> TemporaryProperties { get; } = new();

        public bool HasTemporaryProperties => TemporaryProperties.Any();

        // داخل كلاس AuditEntry في أسفل AppDbContext.cs:
        public AuditLog ToAudit()
        {
            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
            };

            var audit = new AuditLog
            {
                UserId = UserId,
                AuditType = AuditType.ToString(),
                TableName = TableName,
                CreatedAt = DateTimeHelper.LibyaNow, // 👈 تم التحديث ليحفظ التوقيت بـ +2 ساعات
                PrimaryKey = JsonSerializer.Serialize(KeyValues, jsonOptions),
                OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues, jsonOptions),
                NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues, jsonOptions),
                AffectedColumns = ChangedColumns.Count == 0 ? null : JsonSerializer.Serialize(ChangedColumns, jsonOptions)
            };
            return audit;
        }
    }
}