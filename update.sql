IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822203043_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(100) NOT NULL,
        [Email] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [Role] int NOT NULL,
        [IsLocked] bit NOT NULL,
        [FailedLoginAttempts] int NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [LockoutEnd] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822203043_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822203043_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822203043_InitialCreate', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822213123_AddUnitsTable'
)
BEGIN
    CREATE TABLE [Units] (
        [Id] int NOT NULL IDENTITY,
        [UnitNumber] nvarchar(20) NOT NULL,
        [UnitName] nvarchar(100) NOT NULL,
        [UnitType] int NOT NULL,
        [Status] int NOT NULL,
        [Area] decimal(10,2) NOT NULL,
        [Floor] nvarchar(50) NULL,
        [Building] nvarchar(50) NULL,
        [Description] nvarchar(500) NULL,
        [Notes] nvarchar(500) NULL,
        [ElectricityMeterStart] decimal(12,2) NULL,
        [WaterMeterStart] decimal(12,2) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Units] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822213123_AddUnitsTable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Units_UnitNumber] ON [Units] ([UnitNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822213123_AddUnitsTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822213123_AddUnitsTable', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822214326_AddTenantsTable'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(150) NOT NULL,
        [NationalId] nvarchar(50) NOT NULL,
        [Phone] nvarchar(20) NOT NULL,
        [ContactPerson] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822214326_AddTenantsTable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenants_NationalId] ON [Tenants] ([NationalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822214326_AddTenantsTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822214326_AddTenantsTable', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822215804_AddContractsTables'
)
BEGIN
    CREATE TABLE [Contracts] (
        [Id] int NOT NULL IDENTITY,
        [ContractNumber] nvarchar(50) NOT NULL,
        [TenantId] int NOT NULL,
        [UnitId] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [RentAmount] decimal(18,2) NOT NULL,
        [RentCycle] int NOT NULL,
        [DepositAmount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [AutoRenew] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Contracts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Contracts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Contracts_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822215804_AddContractsTables'
)
BEGIN
    CREATE TABLE [ContractDocuments] (
        [Id] int NOT NULL IDENTITY,
        [ContractId] int NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [FileType] nvarchar(50) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ContractDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractDocuments_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822215804_AddContractsTables'
)
BEGIN
    CREATE TABLE [ContractItems] (
        [Id] int NOT NULL IDENTITY,
        [ContractId] int NOT NULL,
        [ItemName] nvarchar(150) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Notes] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ContractItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractItems_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822215804_AddContractsTables'
)
BEGIN
    CREATE INDEX [IX_ContractDocuments_ContractId] ON [ContractDocuments] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822215804_AddContractsTables'
)
BEGIN
    CREATE INDEX [IX_ContractItems_ContractId] ON [ContractItems] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822215804_AddContractsTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Contracts_ContractNumber] ON [Contracts] ([ContractNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822215804_AddContractsTables'
)
BEGIN
    CREATE INDEX [IX_Contracts_TenantId] ON [Contracts] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822215804_AddContractsTables'
)
BEGIN
    CREATE INDEX [IX_Contracts_UnitId] ON [Contracts] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822215804_AddContractsTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822215804_AddContractsTables', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823012504_AddPaymentsTable'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [ReceiptNumber] nvarchar(30) NOT NULL,
        [ContractId] int NOT NULL,
        [TenantId] int NOT NULL,
        [UnitId] int NOT NULL,
        [PaymentType] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentMethod] int NOT NULL,
        [ReferenceNumber] nvarchar(100) NULL,
        [PaymentDate] datetime2 NOT NULL,
        [Notes] nvarchar(300) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823012504_AddPaymentsTable'
)
BEGIN
    CREATE INDEX [IX_Payments_ContractId] ON [Payments] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823012504_AddPaymentsTable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_ReceiptNumber] ON [Payments] ([ReceiptNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823012504_AddPaymentsTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260823012504_AddPaymentsTable', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823013624_AddMaintenanceAndExpensesTables'
)
BEGIN
    CREATE TABLE [Expenses] (
        [Id] int NOT NULL IDENTITY,
        [ExpenseNumber] nvarchar(50) NOT NULL,
        [UnitId] int NULL,
        [ExpenseType] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ExpenseDate] datetime2 NOT NULL,
        [PaidTo] nvarchar(150) NULL,
        [Description] nvarchar(500) NOT NULL,
        [InvoiceNumber] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Expenses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Expenses_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823013624_AddMaintenanceAndExpensesTables'
)
BEGIN
    CREATE TABLE [MaintenanceRequests] (
        [Id] int NOT NULL IDENTITY,
        [RequestNumber] nvarchar(50) NOT NULL,
        [UnitId] int NOT NULL,
        [TenantId] int NULL,
        [Type] int NOT NULL,
        [Priority] int NOT NULL,
        [Status] int NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Cost] decimal(18,2) NOT NULL,
        [RequestDate] datetime2 NOT NULL,
        [CompletionDate] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_MaintenanceRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaintenanceRequests_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_MaintenanceRequests_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823013624_AddMaintenanceAndExpensesTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Expenses_ExpenseNumber] ON [Expenses] ([ExpenseNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823013624_AddMaintenanceAndExpensesTables'
)
BEGIN
    CREATE INDEX [IX_Expenses_UnitId] ON [Expenses] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823013624_AddMaintenanceAndExpensesTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MaintenanceRequests_RequestNumber] ON [MaintenanceRequests] ([RequestNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823013624_AddMaintenanceAndExpensesTables'
)
BEGIN
    CREATE INDEX [IX_MaintenanceRequests_TenantId] ON [MaintenanceRequests] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823013624_AddMaintenanceAndExpensesTables'
)
BEGIN
    CREATE INDEX [IX_MaintenanceRequests_UnitId] ON [MaintenanceRequests] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823013624_AddMaintenanceAndExpensesTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260823013624_AddMaintenanceAndExpensesTables', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823014714_AddVisitorPassesTables'
)
BEGIN
    CREATE TABLE [VisitorPasses] (
        [Id] int NOT NULL IDENTITY,
        [PassCode] nvarchar(64) NOT NULL,
        [VisitorName] nvarchar(150) NOT NULL,
        [VisitorPhone] nvarchar(20) NOT NULL,
        [NationalId] nvarchar(50) NULL,
        [VisitorType] int NOT NULL,
        [UnitId] int NULL,
        [ValidDate] datetime2 NOT NULL,
        [MaxEntries] int NOT NULL,
        [UsedCount] int NOT NULL,
        [Status] int NOT NULL,
        [Purpose] nvarchar(300) NULL,
        [Notes] nvarchar(300) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_VisitorPasses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VisitorPasses_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823014714_AddVisitorPassesTables'
)
BEGIN
    CREATE TABLE [EntryLogs] (
        [Id] int NOT NULL IDENTITY,
        [VisitorPassId] int NOT NULL,
        [ScanTime] datetime2 NOT NULL,
        [GateName] nvarchar(50) NOT NULL,
        [ScannedBy] nvarchar(100) NULL,
        [IsAllowed] bit NOT NULL,
        [RejectReason] nvarchar(255) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_EntryLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EntryLogs_VisitorPasses_VisitorPassId] FOREIGN KEY ([VisitorPassId]) REFERENCES [VisitorPasses] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823014714_AddVisitorPassesTables'
)
BEGIN
    CREATE INDEX [IX_EntryLogs_VisitorPassId] ON [EntryLogs] ([VisitorPassId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823014714_AddVisitorPassesTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VisitorPasses_PassCode] ON [VisitorPasses] ([PassCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823014714_AddVisitorPassesTables'
)
BEGIN
    CREATE INDEX [IX_VisitorPasses_UnitId] ON [VisitorPasses] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823014714_AddVisitorPassesTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260823014714_AddVisitorPassesTables', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823023246_AddSettingsAndNumberSequences'
)
BEGIN
    CREATE TABLE [NumberSequences] (
        [Id] int NOT NULL IDENTITY,
        [SequenceKey] nvarchar(100) NOT NULL,
        [CurrentYear] int NOT NULL,
        [LastNumber] int NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_NumberSequences] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823023246_AddSettingsAndNumberSequences'
)
BEGIN
    CREATE TABLE [Settings] (
        [Id] int NOT NULL IDENTITY,
        [SettingKey] nvarchar(200) NOT NULL,
        [SettingValue] nvarchar(max) NULL,
        [SettingGroup] nvarchar(100) NOT NULL,
        [SettingSubGroup] nvarchar(100) NULL,
        [DataType] nvarchar(50) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [DefaultValue] nvarchar(max) NULL,
        [IsRequired] bit NOT NULL,
        [IsEncrypted] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Settings] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823023246_AddSettingsAndNumberSequences'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NumberSequences_SequenceKey] ON [NumberSequences] ([SequenceKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823023246_AddSettingsAndNumberSequences'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Settings_SettingKey] ON [Settings] ([SettingKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823023246_AddSettingsAndNumberSequences'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260823023246_AddSettingsAndNumberSequences', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823193837_LinkUserToTenant'
)
BEGIN
    ALTER TABLE [Users] ADD [TenantId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823193837_LinkUserToTenant'
)
BEGIN
    CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823193837_LinkUserToTenant'
)
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823193837_LinkUserToTenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260823193837_LinkUserToTenant', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825232529_UpdateUserName'
)
BEGIN
    EXEC sp_rename N'[Users].[Email]', N'UserName', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825232529_UpdateUserName'
)
BEGIN
    EXEC sp_rename N'[Users].[IX_Users_Email]', N'IX_Users_UserName', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825232529_UpdateUserName'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825232529_UpdateUserName', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825235701_UpdateUnitRemoveWaterAndRenameType'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Units]') AND [c].[name] = N'WaterMeterStart');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Units] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Units] DROP COLUMN [WaterMeterStart];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825235701_UpdateUnitRemoveWaterAndRenameType'
)
BEGIN
    EXEC sp_rename N'[Units].[UnitType]', N'ActivityType', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825235701_UpdateUnitRemoveWaterAndRenameType'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Units]') AND [c].[name] = N'UnitName');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Units] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Units] ALTER COLUMN [UnitName] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825235701_UpdateUnitRemoveWaterAndRenameType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825235701_UpdateUnitRemoveWaterAndRenameType', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826050344_Update'
)
BEGIN
    ALTER TABLE [Expenses] ADD [IsChargedToTenant] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826050344_Update'
)
BEGIN
    ALTER TABLE [Expenses] ADD [TenantId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826050344_Update'
)
BEGIN
    CREATE INDEX [IX_Expenses_TenantId] ON [Expenses] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826050344_Update'
)
BEGIN
    ALTER TABLE [Expenses] ADD CONSTRAINT [FK_Expenses_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826050344_Update'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260826050344_Update', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826063616_AddExpenseAttachmentUrl'
)
BEGIN
    ALTER TABLE [Expenses] ADD [AttachmentUrl] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826063616_AddExpenseAttachmentUrl'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260826063616_AddExpenseAttachmentUrl', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234254_SetupFinalVisitorBlacklistTable'
)
BEGIN
    CREATE TABLE [Refunds] (
        [Id] int NOT NULL IDENTITY,
        [RefundNumber] nvarchar(30) NOT NULL,
        [ContractId] int NOT NULL,
        [TenantId] int NOT NULL,
        [UnitId] int NOT NULL,
        [OriginalPaymentId] int NULL,
        [RefundType] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [RefundMethod] int NOT NULL,
        [RefundDate] datetime2 NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Notes] nvarchar(300) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Refunds] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Refunds_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Refunds_Payments_OriginalPaymentId] FOREIGN KEY ([OriginalPaymentId]) REFERENCES [Payments] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234254_SetupFinalVisitorBlacklistTable'
)
BEGIN
    CREATE TABLE [VisitorBlacklists] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(150) NOT NULL,
        [NationalId] nvarchar(50) NULL,
        [Phone] nvarchar(20) NULL,
        [Reason] nvarchar(500) NOT NULL,
        [IncidentDate] datetime2 NOT NULL,
        [IsPermanent] bit NOT NULL,
        [ExpiresAt] datetime2 NULL,
        [Notes] nvarchar(300) NULL,
        [AttachmentUrl] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_VisitorBlacklists] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234254_SetupFinalVisitorBlacklistTable'
)
BEGIN
    CREATE INDEX [IX_Refunds_ContractId] ON [Refunds] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234254_SetupFinalVisitorBlacklistTable'
)
BEGIN
    CREATE INDEX [IX_Refunds_OriginalPaymentId] ON [Refunds] ([OriginalPaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234254_SetupFinalVisitorBlacklistTable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Refunds_RefundNumber] ON [Refunds] ([RefundNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234254_SetupFinalVisitorBlacklistTable'
)
BEGIN
    CREATE INDEX [IX_VisitorBlacklists_NationalId] ON [VisitorBlacklists] ([NationalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234254_SetupFinalVisitorBlacklistTable'
)
BEGIN
    CREATE INDEX [IX_VisitorBlacklists_Phone] ON [VisitorBlacklists] ([Phone]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234254_SetupFinalVisitorBlacklistTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260827234254_SetupFinalVisitorBlacklistTable', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902183123_AddComplaintsTable'
)
BEGIN
    CREATE TABLE [Complaints] (
        [Id] int NOT NULL IDENTITY,
        [Subject] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [TenantId] int NOT NULL,
        [UserId] int NOT NULL,
        [Status] int NOT NULL,
        [SubmittedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Complaints] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Complaints_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Complaints_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902183123_AddComplaintsTable'
)
BEGIN
    CREATE TABLE [ComplaintReplies] (
        [Id] int NOT NULL IDENTITY,
        [ComplaintId] int NOT NULL,
        [RepliedByUserId] int NOT NULL,
        [ReplyText] nvarchar(2000) NOT NULL,
        [RepliedAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ComplaintReplies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ComplaintReplies_Complaints_ComplaintId] FOREIGN KEY ([ComplaintId]) REFERENCES [Complaints] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ComplaintReplies_Users_RepliedByUserId] FOREIGN KEY ([RepliedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902183123_AddComplaintsTable'
)
BEGIN
    CREATE INDEX [IX_ComplaintReplies_ComplaintId] ON [ComplaintReplies] ([ComplaintId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902183123_AddComplaintsTable'
)
BEGIN
    CREATE INDEX [IX_ComplaintReplies_RepliedByUserId] ON [ComplaintReplies] ([RepliedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902183123_AddComplaintsTable'
)
BEGIN
    CREATE INDEX [IX_Complaints_TenantId] ON [Complaints] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902183123_AddComplaintsTable'
)
BEGIN
    CREATE INDEX [IX_Complaints_UserId] ON [Complaints] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260902183123_AddComplaintsTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260902183123_AddComplaintsTable', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905211633_AddTenantCreditBalance'
)
BEGIN
    ALTER TABLE [Tenants] ADD [CreditBalance] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905211633_AddTenantCreditBalance'
)
BEGIN
    ALTER TABLE [NumberSequences] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905211633_AddTenantCreditBalance'
)
BEGIN
    ALTER TABLE [NumberSequences] ADD [CreatedBy] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905211633_AddTenantCreditBalance'
)
BEGIN
    ALTER TABLE [NumberSequences] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905211633_AddTenantCreditBalance'
)
BEGIN
    ALTER TABLE [NumberSequences] ADD [LastYear] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905211633_AddTenantCreditBalance'
)
BEGIN
    ALTER TABLE [NumberSequences] ADD [UpdatedBy] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905211633_AddTenantCreditBalance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905211633_AddTenantCreditBalance', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905214538_AddContractFees'
)
BEGIN
    CREATE TABLE [ContractFees] (
        [Id] int NOT NULL IDENTITY,
        [ContractId] int NOT NULL,
        [FeeName] nvarchar(150) NOT NULL,
        [ValueType] int NOT NULL,
        [Frequency] int NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [Notes] nvarchar(250) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ContractFees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractFees_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905214538_AddContractFees'
)
BEGIN
    CREATE INDEX [IX_ContractFees_ContractId] ON [ContractFees] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905214538_AddContractFees'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905214538_AddContractFees', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905220244_AddContractFee'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[NumberSequences]') AND [c].[name] = N'UpdatedAt');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [NumberSequences] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [NumberSequences] ALTER COLUMN [UpdatedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905220244_AddContractFee'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905220244_AddContractFee', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905222224_AddBankTransferRequests'
)
BEGIN
    CREATE TABLE [BankTransferRequests] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [RequestedAmount] decimal(18,2) NOT NULL,
        [ApprovedAmount] decimal(18,2) NULL,
        [TransferDate] datetime2 NOT NULL,
        [BankName] nvarchar(100) NULL,
        [ReferenceNumber] nvarchar(100) NULL,
        [ReceiptFilePath] nvarchar(500) NOT NULL,
        [Status] int NOT NULL,
        [AdminNotes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_BankTransferRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BankTransferRequests_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905222224_AddBankTransferRequests'
)
BEGIN
    CREATE INDEX [IX_BankTransferRequests_TenantId] ON [BankTransferRequests] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905222224_AddBankTransferRequests'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905222224_AddBankTransferRequests', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906203746_AddContractRenewalAndIncrease'
)
BEGIN
    ALTER TABLE [Contracts] ADD [AnnualIncreasePercentage] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906203746_AddContractRenewalAndIncrease'
)
BEGIN
    ALTER TABLE [Contracts] ADD [ParentContractId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906203746_AddContractRenewalAndIncrease'
)
BEGIN
    CREATE INDEX [IX_Contracts_ParentContractId] ON [Contracts] ([ParentContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906203746_AddContractRenewalAndIncrease'
)
BEGIN
    ALTER TABLE [Contracts] ADD CONSTRAINT [FK_Contracts_Contracts_ParentContractId] FOREIGN KEY ([ParentContractId]) REFERENCES [Contracts] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906203746_AddContractRenewalAndIncrease'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260906203746_AddContractRenewalAndIncrease', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906222247_MoveActivityTypeToContract'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Units]') AND [c].[name] = N'ActivityType');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Units] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Units] DROP COLUMN [ActivityType];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906222247_MoveActivityTypeToContract'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Units]') AND [c].[name] = N'UnitName');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Units] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Units] DROP COLUMN [UnitName];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906222247_MoveActivityTypeToContract'
)
BEGIN
    ALTER TABLE [Contracts] ADD [ActivityType] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906222247_MoveActivityTypeToContract'
)
BEGIN
    ALTER TABLE [Contracts] ADD [TradeName] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906222247_MoveActivityTypeToContract'
)
BEGIN
    CREATE TABLE [UserPermissions] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [PermissionKey] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_UserPermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPermissions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906222247_MoveActivityTypeToContract'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPermissions_UserId_PermissionKey] ON [UserPermissions] ([UserId], [PermissionKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906222247_MoveActivityTypeToContract'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260906222247_MoveActivityTypeToContract', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    CREATE TABLE [NotificationPreferences] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [TenantId] int NULL,
        [NotificationType] int NOT NULL,
        [InAppEnabled] bit NOT NULL,
        [PushEnabled] bit NOT NULL,
        [EmailEnabled] bit NOT NULL,
        [SmsEnabled] bit NOT NULL,
        [QuietHoursStart] time NULL,
        [QuietHoursEnd] time NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationPreferences_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_NotificationPreferences_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [TenantId] int NULL,
        [TargetGroup] nvarchar(50) NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [Type] int NOT NULL,
        [Priority] int NOT NULL,
        [Icon] nvarchar(50) NULL,
        [ActionUrl] nvarchar(500) NULL,
        [ImageUrl] nvarchar(500) NULL,
        [RelatedEntityId] int NULL,
        [RelatedEntityType] nvarchar(50) NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [Channel] int NOT NULL,
        [ScheduledFor] datetime2 NULL,
        [IsSent] bit NOT NULL,
        [SentAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    CREATE TABLE [PushSubscriptions] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [TenantId] int NULL,
        [Endpoint] nvarchar(500) NOT NULL,
        [P256dh] nvarchar(200) NOT NULL,
        [Auth] nvarchar(100) NOT NULL,
        [DeviceInfo] nvarchar(200) NULL,
        [BrowserType] nvarchar(50) NULL,
        [LastUsedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_PushSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PushSubscriptions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PushSubscriptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    CREATE INDEX [IX_NotificationPreferences_TenantId] ON [NotificationPreferences] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_NotificationPreferences_UserId_TenantId_NotificationType] ON [NotificationPreferences] ([UserId], [TenantId], [NotificationType]) WHERE [UserId] IS NOT NULL AND [TenantId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    CREATE INDEX [IX_Notifications_CreatedAt] ON [Notifications] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    CREATE INDEX [IX_Notifications_IsRead] ON [Notifications] ([IsRead]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    CREATE INDEX [IX_Notifications_TenantId] ON [Notifications] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PushSubscriptions_Endpoint] ON [PushSubscriptions] ([Endpoint]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    CREATE INDEX [IX_PushSubscriptions_TenantId] ON [PushSubscriptions] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    CREATE INDEX [IX_PushSubscriptions_UserId] ON [PushSubscriptions] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906230922_AddNotificationsSystem'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260906230922_AddNotificationsSystem', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    ALTER TABLE [VisitorPasses] ADD [InitialBalance] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    ALTER TABLE [VisitorPasses] ADD [IsPaidPass] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    ALTER TABLE [VisitorPasses] ADD [IssuedByUserId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    ALTER TABLE [VisitorPasses] ADD [RemainingBalance] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    ALTER TABLE [VisitorPasses] ADD [WalletStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    CREATE TABLE [GatekeeperShifts] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NULL,
        [TotalPassesIssued] int NOT NULL,
        [TotalCashCollected] decimal(18,2) NOT NULL,
        [IsHandedOver] bit NOT NULL,
        [HandedOverAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_GatekeeperShifts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GatekeeperShifts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    CREATE TABLE [TenantSettlements] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [SettlementDate] datetime2 NOT NULL,
        [SettlementMethod] int NOT NULL,
        [ProcessedByUserId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_TenantSettlements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantSettlements_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TenantSettlements_Users_ProcessedByUserId] FOREIGN KEY ([ProcessedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    CREATE TABLE [PassTransactions] (
        [Id] int NOT NULL IDENTITY,
        [VisitorPassId] int NOT NULL,
        [TenantId] int NULL,
        [UnitId] int NULL,
        [Amount] decimal(18,2) NOT NULL,
        [TransactionDate] datetime2 NOT NULL,
        [IsSettled] bit NOT NULL,
        [SettlementId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_PassTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PassTransactions_TenantSettlements_SettlementId] FOREIGN KEY ([SettlementId]) REFERENCES [TenantSettlements] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_PassTransactions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PassTransactions_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]),
        CONSTRAINT [FK_PassTransactions_VisitorPasses_VisitorPassId] FOREIGN KEY ([VisitorPassId]) REFERENCES [VisitorPasses] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    CREATE INDEX [IX_VisitorPasses_IssuedByUserId] ON [VisitorPasses] ([IssuedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    CREATE INDEX [IX_GatekeeperShifts_UserId] ON [GatekeeperShifts] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    CREATE INDEX [IX_PassTransactions_SettlementId] ON [PassTransactions] ([SettlementId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    CREATE INDEX [IX_PassTransactions_TenantId] ON [PassTransactions] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    CREATE INDEX [IX_PassTransactions_UnitId] ON [PassTransactions] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    CREATE INDEX [IX_PassTransactions_VisitorPassId] ON [PassTransactions] ([VisitorPassId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    CREATE INDEX [IX_TenantSettlements_ProcessedByUserId] ON [TenantSettlements] ([ProcessedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    CREATE INDEX [IX_TenantSettlements_TenantId] ON [TenantSettlements] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    ALTER TABLE [VisitorPasses] ADD CONSTRAINT [FK_VisitorPasses_Users_IssuedByUserId] FOREIGN KEY ([IssuedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913225502_AddVisitorWalletSystem'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260913225502_AddVisitorWalletSystem', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919191012_AddTenantMaxEntriesLimit'
)
BEGIN
    ALTER TABLE [Tenants] ADD [MaxAllowedEntriesPerPass] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919191012_AddTenantMaxEntriesLimit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260919191012_AddTenantMaxEntriesLimit', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919212759_AddAuditLogs'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [AuditType] nvarchar(50) NOT NULL,
        [TableName] nvarchar(100) NOT NULL,
        [PrimaryKey] nvarchar(50) NOT NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [AffectedColumns] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919212759_AddAuditLogs'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919212759_AddAuditLogs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260919212759_AddAuditLogs', N'8.0.26');
END;
GO

COMMIT;
GO


IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    -- 👈 1. أعمدة تحميل المستأجر على طلبات الصيانة
    ALTER TABLE [MaintenanceRequests] ADD [BilledAmount] decimal(18,2) NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    ALTER TABLE [MaintenanceRequests] ADD [BilledToTenant] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    -- 👈 2. ربط المصروف بطلب الصيانة (تكلفة الصيانة المسجلة تلقائياً)
    ALTER TABLE [Expenses] ADD [MaintenanceRequestId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    -- 👈 3. جدول متعلقات المستأجر (التحميلات المالية)
    CREATE TABLE [TenantCharges] (
        [Id] int NOT NULL IDENTITY,
        [ChargeNumber] nvarchar(50) NOT NULL,
        [TenantId] int NOT NULL,
        [UnitId] int NULL,
        [ContractId] int NULL,
        [MaintenanceRequestId] int NULL,
        [Amount] decimal(18,2) NOT NULL,
        [SettledAmount] decimal(18,2) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [ChargeDate] datetime2 NOT NULL,
        [IsSettled] bit NOT NULL,
        [SettlementReceiptNumber] nvarchar(50) NULL,
        [Notes] nvarchar(300) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_TenantCharges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantCharges_MaintenanceRequests_MaintenanceRequestId] FOREIGN KEY ([MaintenanceRequestId]) REFERENCES [MaintenanceRequests] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_TenantCharges_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TenantCharges_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantCharges_ChargeNumber] ON [TenantCharges] ([ChargeNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    CREATE INDEX [IX_TenantCharges_IsSettled] ON [TenantCharges] ([IsSettled]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    CREATE INDEX [IX_TenantCharges_MaintenanceRequestId] ON [TenantCharges] ([MaintenanceRequestId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    CREATE INDEX [IX_TenantCharges_TenantId] ON [TenantCharges] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    CREATE INDEX [IX_TenantCharges_UnitId] ON [TenantCharges] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    CREATE INDEX [IX_Expenses_MaintenanceRequestId] ON [Expenses] ([MaintenanceRequestId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    ALTER TABLE [Expenses] ADD CONSTRAINT [FK_Expenses_MaintenanceRequests_MaintenanceRequestId] FOREIGN KEY ([MaintenanceRequestId]) REFERENCES [MaintenanceRequests] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926210000_AddMaintenanceBillingAndTenantCharges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260926210000_AddMaintenanceBillingAndTenantCharges', N'8.0.26');
END;
GO
