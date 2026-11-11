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

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    ALTER TABLE [EquipmentUseRegistrations] ADD [CancelIntentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    ALTER TABLE [EquipmentUseRegistrations] ADD [DepartmentIdSnapshot] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    ALTER TABLE [EquipmentUseRegistrations] ADD [DepartmentNameSnapshot] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    ALTER TABLE [EquipmentUseRegistrations] ADD [EquipmentCodeSnapshot] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    ALTER TABLE [EquipmentUseRegistrations] ADD [EquipmentNameSnapshot] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    ALTER TABLE [EquipmentUseRegistrations] ADD [FencingToken] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    ALTER TABLE [EquipmentUseRegistrations] ADD [RequestHash] binary(32) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    ALTER TABLE [EquipmentUseRegistrations] ADD [RequestPayloadJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    CREATE TABLE [EquipmentRetirementIntents] (
        [Id] uniqueidentifier NOT NULL,
        [EquipmentId] uniqueidentifier NOT NULL,
        [RetirementEpoch] bigint NOT NULL,
        [Reason] nvarchar(2000) NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [RequestKey] uniqueidentifier NOT NULL,
        [RequestHash] binary(32) NOT NULL,
        [RequiredSourcesJson] nvarchar(200) NOT NULL,
        [State] nvarchar(20) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [CancelRequestedByUserId] uniqueidentifier NULL,
        [CancelReason] nvarchar(2000) NULL,
        [CancelRequestedAtUtc] datetimeoffset NULL,
        [CancelledAtUtc] datetimeoffset NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [ContinueCount] int NOT NULL,
        [LastContinuedByUserId] uniqueidentifier NULL,
        [LastContinueReason] nvarchar(2000) NULL,
        [LastContinuedAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EquipmentRetirementIntents] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_EquipmentRetirementIntents_Id_EquipmentId_RetirementEpoch] UNIQUE ([Id], [EquipmentId], [RetirementEpoch]),
        CONSTRAINT [CK_RetirementIntents_Cancellation] CHECK (([State] IN (N'Cancelling',N'Cancelled') AND [CancelRequestedByUserId] IS NOT NULL AND [CancelRequestedAtUtc] IS NOT NULL AND [CancelReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CancelReason]))) > 0) OR ([State] NOT IN (N'Cancelling',N'Cancelled') AND [CancelRequestedByUserId] IS NULL AND [CancelRequestedAtUtc] IS NULL AND [CancelReason] IS NULL)),
        CONSTRAINT [CK_RetirementIntents_Cancelled] CHECK (([State] = N'Cancelled' AND [CancelledAtUtc] IS NOT NULL) OR ([State] <> N'Cancelled' AND [CancelledAtUtc] IS NULL)),
        CONSTRAINT [CK_RetirementIntents_Completion] CHECK (([State] = N'Completed' AND [CompletedAtUtc] IS NOT NULL) OR ([State] <> N'Completed' AND [CompletedAtUtc] IS NULL)),
        CONSTRAINT [CK_RetirementIntents_Coverage] CHECK ([RequiredSourcesJson] = N'{"Maintenance":1,"Incident":1}'),
        CONSTRAINT [CK_RetirementIntents_Epoch] CHECK ([RetirementEpoch] > 0 AND [ContinueCount] >= 0),
        CONSTRAINT [CK_RetirementIntents_Reason] CHECK (LEN(LTRIM(RTRIM([Reason]))) > 0),
        CONSTRAINT [CK_RetirementIntents_State] CHECK ([State] IN (N'Coordinating',N'Ready',N'Cancelling',N'Cancelled',N'Completed')),
        CONSTRAINT [CK_RetirementIntents_Utc] CHECK (DATEPART(TZOFFSET,[CreatedAtUtc])=0 AND DATEPART(TZOFFSET,[UpdatedAtUtc])=0 AND [UpdatedAtUtc] >= [CreatedAtUtc]),
        CONSTRAINT [FK_EquipmentRetirementIntents_EquipmentAssets_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [EquipmentAssets] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    CREATE TABLE [RetirementSourceReceipts] (
        [IntentId] uniqueidentifier NOT NULL,
        [SourceService] nvarchar(30) NOT NULL,
        [EquipmentId] uniqueidentifier NOT NULL,
        [RetirementEpoch] bigint NOT NULL,
        [CoverageVersion] int NOT NULL,
        [SourceGateVersion] bigint NOT NULL,
        [ReadyProofId] uniqueidentifier NULL,
        [CancelAcknowledgementId] uniqueidentifier NULL,
        [SourceState] nvarchar(20) NOT NULL,
        [ReceivedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RetirementSourceReceipts] PRIMARY KEY ([IntentId], [SourceService]),
        CONSTRAINT [CK_RetirementReceipts_Proof] CHECK (([SourceState] IN (N'Frozen',N'Blocked') AND [ReadyProofId] IS NULL AND [CancelAcknowledgementId] IS NULL) OR ([SourceState]=N'Sealed' AND [ReadyProofId] IS NOT NULL AND [CancelAcknowledgementId] IS NULL) OR ([SourceState]=N'CancelAck' AND [CancelAcknowledgementId] IS NOT NULL)),
        CONSTRAINT [CK_RetirementReceipts_Source] CHECK ([SourceService] IN (N'Maintenance',N'Incident')),
        CONSTRAINT [CK_RetirementReceipts_State] CHECK ([SourceState] IN (N'Frozen',N'Blocked',N'Sealed',N'CancelAck')),
        CONSTRAINT [CK_RetirementReceipts_Utc] CHECK (DATEPART(TZOFFSET,[ReceivedAtUtc])=0),
        CONSTRAINT [CK_RetirementReceipts_Version] CHECK ([CoverageVersion]=1 AND [RetirementEpoch]>0 AND [SourceGateVersion]>0),
        CONSTRAINT [FK_RetirementSourceReceipts_EquipmentRetirementIntents_IntentId_EquipmentId_RetirementEpoch] FOREIGN KEY ([IntentId], [EquipmentId], [RetirementEpoch]) REFERENCES [EquipmentRetirementIntents] ([Id], [EquipmentId], [RetirementEpoch])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    EXEC(N'ALTER TABLE [EquipmentUseRegistrations] ADD CONSTRAINT [CK_EquipmentUseRegistrations_Fence] CHECK ([FencingToken] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    EXEC(N'ALTER TABLE [EquipmentUseRegistrations] ADD CONSTRAINT [CK_EquipmentUseRegistrations_Payload] CHECK ([RequestPayloadJson] IS NULL OR (ISJSON([RequestPayloadJson]) = 1 AND [ResourceType] = N''WorkOrder'' AND [SourceService] = N''Maintenance'' AND [RequestHash] IS NOT NULL AND [FencingToken] > 0 AND [EquipmentCodeSnapshot] IS NOT NULL AND [EquipmentNameSnapshot] IS NOT NULL AND [DepartmentIdSnapshot] IS NOT NULL AND [DepartmentNameSnapshot] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    EXEC(N'ALTER TABLE [EquipmentUseRegistrations] ADD CONSTRAINT [CK_EquipmentUseRegistrations_ProtocolPair] CHECK (([RequestHash] IS NULL AND [RequestPayloadJson] IS NULL AND [FencingToken] = 0 AND [CancelIntentId] IS NULL AND [EquipmentCodeSnapshot] IS NULL AND [EquipmentNameSnapshot] IS NULL AND [DepartmentIdSnapshot] IS NULL AND [DepartmentNameSnapshot] IS NULL) OR ([RequestHash] IS NOT NULL AND [RequestPayloadJson] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EquipmentRetirementIntents_ActorUserId_RequestKey] ON [EquipmentRetirementIntents] ([ActorUserId], [RequestKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EquipmentRetirementIntents_EquipmentId_RetirementEpoch] ON [EquipmentRetirementIntents] ([EquipmentId], [RetirementEpoch]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    CREATE INDEX [IX_EquipmentRetirementIntents_State_CreatedAtUtc_Id] ON [EquipmentRetirementIntents] ([State], [CreatedAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_RetirementIntents_ActiveEquipment] ON [EquipmentRetirementIntents] ([EquipmentId]) WHERE [State] <> N''Cancelled'' AND [State] <> N''Completed''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    CREATE INDEX [IX_RetirementSourceReceipts_EquipmentId_RetirementEpoch_SourceService] ON [RetirementSourceReceipts] ([EquipmentId], [RetirementEpoch], [SourceService]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    CREATE INDEX [IX_RetirementSourceReceipts_IntentId_EquipmentId_RetirementEpoch] ON [RetirementSourceReceipts] ([IntentId], [EquipmentId], [RetirementEpoch]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106063402_AddEquipmentRetirementFoundation', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106164929_AddResourceAbortProtocol'
)
BEGIN
    ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT [CK_EquipmentUseRegistrations_Payload];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106164929_AddResourceAbortProtocol'
)
BEGIN
    EXEC(N'ALTER TABLE [EquipmentUseRegistrations] ADD CONSTRAINT [CK_EquipmentUseRegistrations_Payload] CHECK ([RequestPayloadJson] IS NULL OR (ISJSON([RequestPayloadJson]) = 1 AND (([ResourceType] IN (N''WorkOrder'',N''MaintenanceSchedule'') AND [SourceService] = N''Maintenance'') OR ([ResourceType]=N''Incident'' AND [SourceService]=N''Incident'')) AND [RequestHash] IS NOT NULL AND [FencingToken] > 0 AND [EquipmentCodeSnapshot] IS NOT NULL AND [EquipmentNameSnapshot] IS NOT NULL AND [DepartmentIdSnapshot] IS NOT NULL AND [DepartmentNameSnapshot] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106164929_AddResourceAbortProtocol'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106164929_AddResourceAbortProtocol', N'10.0.13');
END;

COMMIT;
GO
