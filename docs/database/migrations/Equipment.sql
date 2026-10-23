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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [ActorService] nvarchar(50) NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(128) NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [CorrelationId] nvarchar(100) NOT NULL,
        [ChangesJson] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AuditEntries_Json] CHECK ([ChangesJson] IS NULL OR ISJSON([ChangesJson]) = 1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [UpdatedByUserId] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE TABLE [EquipmentCategories] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [UpdatedByUserId] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EquipmentCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE TABLE [IdempotencyRequests] (
        [OperationId] uniqueidentifier NOT NULL,
        [ActorScope] nvarchar(128) NOT NULL,
        [OperationName] nvarchar(80) NOT NULL,
        [IdempotencyKey] nvarchar(128) NOT NULL,
        [RequestHash] binary(32) NOT NULL,
        [ResourceId] uniqueidentifier NULL,
        [State] nvarchar(20) NOT NULL,
        [ResponseStatusCode] int NULL,
        [ResponseJson] nvarchar(max) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_IdempotencyRequests] PRIMARY KEY ([OperationId]),
        CONSTRAINT [CK_IdempotencyRequests_Expiration] CHECK ([ExpiresAtUtc] > [CreatedAtUtc]),
        CONSTRAINT [CK_IdempotencyRequests_Json] CHECK ([ResponseJson] IS NULL OR ISJSON([ResponseJson]) = 1),
        CONSTRAINT [CK_IdempotencyRequests_State] CHECK ([State] IN (N'Pending',N'Committed',N'Aborted'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE TABLE [InboxMessages] (
        [ConsumerName] nvarchar(100) NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(150) NOT NULL,
        [SchemaVersion] int NOT NULL,
        [ProcessedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InboxMessages] PRIMARY KEY ([ConsumerName], [EventId]),
        CONSTRAINT [CK_InboxMessages_SchemaVersion] CHECK ([SchemaVersion] >= 1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE TABLE [OutboxMessages] (
        [EventId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(150) NOT NULL,
        [SchemaVersion] int NOT NULL,
        [AggregateType] nvarchar(100) NOT NULL,
        [AggregateId] uniqueidentifier NOT NULL,
        [AggregateVersion] bigint NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [CorrelationId] nvarchar(100) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [AttemptCount] int NOT NULL,
        [NextAttemptAtUtc] datetimeoffset NULL,
        [LeaseOwner] nvarchar(100) NULL,
        [LeaseUntilUtc] datetimeoffset NULL,
        [PublishedAtUtc] datetimeoffset NULL,
        [LastError] nvarchar(2000) NULL,
        CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([EventId]),
        CONSTRAINT [CK_OutboxMessages_Json] CHECK (ISJSON([PayloadJson]) = 1),
        CONSTRAINT [CK_OutboxMessages_Status] CHECK ([Status] IN (N'Pending',N'Processing',N'Published',N'Failed')),
        CONSTRAINT [CK_OutboxMessages_Versions] CHECK ([SchemaVersion] >= 1 AND [AggregateVersion] >= 1 AND [AttemptCount] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE TABLE [ReconciliationCheckpoints] (
        [SourceService] nvarchar(30) NOT NULL,
        [SnapshotId] uniqueidentifier NULL,
        [SnapshotWatermark] nvarchar(256) NULL,
        [Cursor] nvarchar(512) NULL,
        [State] nvarchar(20) NOT NULL,
        [StartedAtUtc] datetimeoffset NULL,
        [LastCompletedAtUtc] datetimeoffset NULL,
        [LastError] nvarchar(2000) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ReconciliationCheckpoints] PRIMARY KEY ([SourceService]),
        CONSTRAINT [CK_ReconciliationCheckpoints_SourceService] CHECK ([SourceService] IN (N'Incident',N'Maintenance')),
        CONSTRAINT [CK_ReconciliationCheckpoints_State] CHECK ([State] IN (N'Idle',N'Running',N'Completed',N'Failed'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE TABLE [EquipmentAssets] (
        [Id] uniqueidentifier NOT NULL,
        [AssetCode] nvarchar(50) NOT NULL,
        [SerialNumber] nvarchar(100) NULL,
        [Name] nvarchar(200) NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [DepartmentId] uniqueidentifier NOT NULL,
        [Manufacturer] nvarchar(200) NULL,
        [Model] nvarchar(100) NULL,
        [PurchasedOn] date NULL,
        [WarrantyUntil] date NULL,
        [IsRetired] bit NOT NULL,
        [OperationalStatus] nvarchar(30) NOT NULL,
        [RetiredAtUtc] datetimeoffset NULL,
        [RetiredByUserId] uniqueidentifier NULL,
        [RetirementReason] nvarchar(2000) NULL,
        [AggregateVersion] bigint NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [UpdatedByUserId] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EquipmentAssets] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EquipmentAssets_AggregateVersion] CHECK ([AggregateVersion] >= 1),
        CONSTRAINT [CK_EquipmentAssets_OperationalStatus] CHECK ([OperationalStatus] IN (N'Operational',N'UnderMaintenance',N'Broken',N'Retired')),
        CONSTRAINT [CK_EquipmentAssets_Retirement] CHECK (([IsRetired] = 1 AND [OperationalStatus] = N'Retired' AND [RetiredAtUtc] IS NOT NULL AND [RetiredByUserId] IS NOT NULL) OR ([IsRetired] = 0 AND [OperationalStatus] <> N'Retired' AND [RetiredAtUtc] IS NULL)),
        CONSTRAINT [FK_EquipmentAssets_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]),
        CONSTRAINT [FK_EquipmentAssets_EquipmentCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [EquipmentCategories] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE TABLE [EquipmentStateProjections] (
        [SourceService] nvarchar(30) NOT NULL,
        [SourceAggregateId] uniqueidentifier NOT NULL,
        [EquipmentId] uniqueidentifier NOT NULL,
        [SourceStatus] nvarchar(30) NOT NULL,
        [IsBlocking] bit NOT NULL,
        [SourceVersion] bigint NOT NULL,
        [LastEventId] uniqueidentifier NOT NULL,
        [LastOccurredAtUtc] datetimeoffset NOT NULL,
        [AppliedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EquipmentStateProjections] PRIMARY KEY ([SourceService], [SourceAggregateId]),
        CONSTRAINT [CK_EquipmentStateProjections_SourceService] CHECK ([SourceService] IN (N'Incident',N'Maintenance')),
        CONSTRAINT [CK_EquipmentStateProjections_SourceVersion] CHECK ([SourceVersion] >= 1),
        CONSTRAINT [FK_EquipmentStateProjections_EquipmentAssets_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [EquipmentAssets] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE TABLE [EquipmentStatusHistories] (
        [Id] uniqueidentifier NOT NULL,
        [EquipmentId] uniqueidentifier NOT NULL,
        [ChangeKind] nvarchar(30) NOT NULL,
        [FromStatus] nvarchar(30) NULL,
        [ToStatus] nvarchar(30) NULL,
        [FromDepartmentId] uniqueidentifier NULL,
        [ToDepartmentId] uniqueidentifier NULL,
        [FromDepartmentNameSnapshot] nvarchar(200) NULL,
        [ToDepartmentNameSnapshot] nvarchar(200) NULL,
        [Reason] nvarchar(2000) NULL,
        [ActorUserId] uniqueidentifier NULL,
        [SourceEventId] uniqueidentifier NULL,
        [ChangedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_EquipmentStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EquipmentStatusHistories_Departments_FromDepartmentId] FOREIGN KEY ([FromDepartmentId]) REFERENCES [Departments] ([Id]),
        CONSTRAINT [FK_EquipmentStatusHistories_Departments_ToDepartmentId] FOREIGN KEY ([ToDepartmentId]) REFERENCES [Departments] ([Id]),
        CONSTRAINT [FK_EquipmentStatusHistories_EquipmentAssets_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [EquipmentAssets] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE TABLE [EquipmentUseRegistrations] (
        [Id] uniqueidentifier NOT NULL,
        [EquipmentId] uniqueidentifier NOT NULL,
        [SourceService] nvarchar(30) NOT NULL,
        [ResourceType] nvarchar(30) NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [SourceOperationId] uniqueidentifier NOT NULL,
        [State] nvarchar(20) NOT NULL,
        [SourceVersion] bigint NOT NULL,
        [RegisteredAtUtc] datetimeoffset NOT NULL,
        [ConfirmedAtUtc] datetimeoffset NULL,
        [ReleasedAtUtc] datetimeoffset NULL,
        [LastVerifiedAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EquipmentUseRegistrations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EquipmentUseRegistrations_ResourceType] CHECK ([ResourceType] IN (N'Incident',N'MaintenanceSchedule',N'WorkOrder')),
        CONSTRAINT [CK_EquipmentUseRegistrations_SourceService] CHECK ([SourceService] IN (N'Incident',N'Maintenance')),
        CONSTRAINT [CK_EquipmentUseRegistrations_SourceVersion] CHECK ([SourceVersion] >= 0),
        CONSTRAINT [CK_EquipmentUseRegistrations_State] CHECK ([State] IN (N'Pending',N'Active',N'Released',N'Aborted')),
        CONSTRAINT [FK_EquipmentUseRegistrations_EquipmentAssets_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [EquipmentAssets] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_EntityType_EntityId_OccurredAtUtc] ON [AuditEntries] ([EntityType], [EntityId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Departments_Code] ON [Departments] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EquipmentAssets_CategoryId] ON [EquipmentAssets] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EquipmentAssets_DepartmentId_OperationalStatus_Id] ON [EquipmentAssets] ([DepartmentId], [OperationalStatus], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_EquipmentAssets_AssetCode] ON [EquipmentAssets] ([AssetCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_EquipmentAssets_SerialNumber] ON [EquipmentAssets] ([SerialNumber]) WHERE [SerialNumber] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_EquipmentCategories_Code] ON [EquipmentCategories] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EquipmentStateProjections_EquipmentId_IsBlocking] ON [EquipmentStateProjections] ([EquipmentId], [IsBlocking]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EquipmentStatusHistories_EquipmentId_ChangedAtUtc_Id] ON [EquipmentStatusHistories] ([EquipmentId], [ChangedAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EquipmentStatusHistories_FromDepartmentId] ON [EquipmentStatusHistories] ([FromDepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EquipmentStatusHistories_ToDepartmentId] ON [EquipmentStatusHistories] ([ToDepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_EquipmentStatusHistories_SourceEventId] ON [EquipmentStatusHistories] ([SourceEventId]) WHERE [SourceEventId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EquipmentUseRegistrations_EquipmentId_State] ON [EquipmentUseRegistrations] ([EquipmentId], [State]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_EquipmentUseRegistrations_SourceService_ResourceType_ResourceId] ON [EquipmentUseRegistrations] ([SourceService], [ResourceType], [ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_EquipmentUseRegistrations_SourceService_SourceOperationId] ON [EquipmentUseRegistrations] ([SourceService], [SourceOperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_IdempotencyRequests_ActorScope_OperationName_IdempotencyKey] ON [IdempotencyRequests] ([ActorScope], [OperationName], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_Status_NextAttemptAtUtc_OccurredAtUtc] ON [OutboxMessages] ([Status], [NextAttemptAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044354_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261023044354_InitialCreate', N'10.0.13');
END;

COMMIT;
GO
