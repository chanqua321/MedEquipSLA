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
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
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
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
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
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE TABLE [MaintenanceSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [EquipmentId] uniqueidentifier NOT NULL,
        [EquipmentCodeSnapshot] nvarchar(50) NOT NULL,
        [EquipmentNameSnapshot] nvarchar(200) NOT NULL,
        [DepartmentIdSnapshot] uniqueidentifier NOT NULL,
        [DepartmentNameSnapshot] nvarchar(200) NOT NULL,
        [WorkType] nvarchar(20) NOT NULL,
        [AnchorAtUtc] datetimeoffset NOT NULL,
        [IntervalDays] int NOT NULL,
        [NextOccurrenceAtUtc] datetimeoffset NOT NULL,
        [ReminderLeadMinutes] int NOT NULL,
        [CompletionWindowMinutes] int NOT NULL,
        [IsActive] bit NOT NULL,
        [EquipmentUseRegistrationId] uniqueidentifier NOT NULL,
        [AggregateVersion] bigint NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [UpdatedByUserId] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MaintenanceSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MaintenanceSchedules_AggregateVersion] CHECK ([AggregateVersion] >= 1),
        CONSTRAINT [CK_MaintenanceSchedules_Intervals] CHECK ([IntervalDays] > 0 AND [ReminderLeadMinutes] >= 0 AND [CompletionWindowMinutes] >= 0),
        CONSTRAINT [CK_MaintenanceSchedules_WorkType] CHECK ([WorkType] IN (N'Maintenance',N'Calibration'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
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
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE TABLE [ScheduleOccurrences] (
        [Id] uniqueidentifier NOT NULL,
        [ScheduleId] uniqueidentifier NOT NULL,
        [ScheduledOccurrenceAtUtc] datetimeoffset NOT NULL,
        [DueAtUtc] datetimeoffset NOT NULL,
        [State] nvarchar(20) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [CancelledAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ScheduleOccurrences] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ScheduleOccurrences_Deadline] CHECK ([DueAtUtc] >= [ScheduledOccurrenceAtUtc]),
        CONSTRAINT [CK_ScheduleOccurrences_State] CHECK ([State] IN (N'Planned',N'WorkOrderCreated',N'Cancelled')),
        CONSTRAINT [FK_ScheduleOccurrences_MaintenanceSchedules_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [MaintenanceSchedules] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE TABLE [OccurrenceReminders] (
        [Id] uniqueidentifier NOT NULL,
        [OccurrenceId] uniqueidentifier NOT NULL,
        [ReminderKind] nvarchar(20) NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [TriggeredAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_OccurrenceReminders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OccurrenceReminders_ReminderKind] CHECK ([ReminderKind] IN (N'DueSoon',N'Overdue')),
        CONSTRAINT [FK_OccurrenceReminders_ScheduleOccurrences_OccurrenceId] FOREIGN KEY ([OccurrenceId]) REFERENCES [ScheduleOccurrences] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE TABLE [WorkOrders] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [OccurrenceId] uniqueidentifier NULL,
        [EquipmentId] uniqueidentifier NOT NULL,
        [EquipmentCodeSnapshot] nvarchar(50) NOT NULL,
        [EquipmentNameSnapshot] nvarchar(200) NOT NULL,
        [DepartmentIdSnapshot] uniqueidentifier NOT NULL,
        [DepartmentNameSnapshot] nvarchar(200) NOT NULL,
        [WorkType] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [AssignedTechnicianId] uniqueidentifier NULL,
        [ScheduledForUtc] datetimeoffset NOT NULL,
        [DueAtUtc] datetimeoffset NOT NULL,
        [StartedAtUtc] datetimeoffset NULL,
        [ClosedAtUtc] datetimeoffset NULL,
        [CancelledAtUtc] datetimeoffset NULL,
        [CancellationReason] nvarchar(2000) NULL,
        [IsBlocking] bit NOT NULL,
        [EquipmentUseRegistrationId] uniqueidentifier NOT NULL,
        [AggregateVersion] bigint NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [UpdatedByUserId] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_WorkOrders_AggregateVersion] CHECK ([AggregateVersion] >= 1),
        CONSTRAINT [CK_WorkOrders_Deadline] CHECK ([DueAtUtc] >= [ScheduledForUtc]),
        CONSTRAINT [CK_WorkOrders_Status] CHECK ([Status] IN (N'Scheduled',N'InProgress',N'Completed',N'Closed',N'Cancelled')),
        CONSTRAINT [CK_WorkOrders_WorkType] CHECK ([WorkType] IN (N'Maintenance',N'Calibration')),
        CONSTRAINT [FK_WorkOrders_ScheduleOccurrences_OccurrenceId] FOREIGN KEY ([OccurrenceId]) REFERENCES [ScheduleOccurrences] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE TABLE [WorkOrderActivities] (
        [Id] uniqueidentifier NOT NULL,
        [WorkOrderId] uniqueidentifier NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [FromStatus] nvarchar(20) NULL,
        [ToStatus] nvarchar(20) NULL,
        [FromTechnicianId] uniqueidentifier NULL,
        [ToTechnicianId] uniqueidentifier NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Reason] nvarchar(2000) NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_WorkOrderActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkOrderActivities_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE TABLE [WorkOrderAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [WorkOrderId] uniqueidentifier NOT NULL,
        [AttemptNumber] int NOT NULL,
        [PerformedByUserId] uniqueidentifier NOT NULL,
        [CompletedAtUtc] datetimeoffset NOT NULL,
        [ResultStatus] nvarchar(20) NOT NULL,
        [Notes] nvarchar(4000) NOT NULL,
        [Cost] decimal(18,2) NULL,
        [CurrencyCode] char(3) NULL,
        [ReviewDecision] nvarchar(20) NOT NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedAtUtc] datetimeoffset NULL,
        [ReviewReason] nvarchar(2000) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkOrderAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_WorkOrderAttempts_AttemptNumber] CHECK ([AttemptNumber] > 0),
        CONSTRAINT [CK_WorkOrderAttempts_Cost] CHECK ([Cost] IS NULL OR [Cost] >= 0),
        CONSTRAINT [CK_WorkOrderAttempts_RejectedReason] CHECK ([ReviewDecision] <> N'Rejected' OR NULLIF(LTRIM(RTRIM([ReviewReason])),N'') IS NOT NULL),
        CONSTRAINT [CK_WorkOrderAttempts_ResultStatus] CHECK ([ResultStatus] IN (N'Passed',N'Failed')),
        CONSTRAINT [CK_WorkOrderAttempts_Review] CHECK (([ReviewDecision] = N'Pending' AND [ReviewedByUserId] IS NULL AND [ReviewedAtUtc] IS NULL) OR ([ReviewDecision] IN (N'Accepted',N'Rejected') AND [ReviewedByUserId] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL AND [ReviewedAtUtc] >= [CompletedAtUtc])),
        CONSTRAINT [CK_WorkOrderAttempts_ReviewDecision] CHECK ([ReviewDecision] IN (N'Pending',N'Accepted',N'Rejected')),
        CONSTRAINT [FK_WorkOrderAttempts_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE TABLE [CalibrationMeasurements] (
        [Id] uniqueidentifier NOT NULL,
        [WorkOrderAttemptId] uniqueidentifier NOT NULL,
        [ParameterCode] nvarchar(50) NOT NULL,
        [ParameterName] nvarchar(200) NOT NULL,
        [Unit] nvarchar(30) NOT NULL,
        [MeasuredValue] decimal(18,6) NOT NULL,
        [MinimumAllowed] decimal(18,6) NULL,
        [MaximumAllowed] decimal(18,6) NULL,
        [Passed] bit NOT NULL,
        CONSTRAINT [PK_CalibrationMeasurements] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CalibrationMeasurements_Result] CHECK (([Passed] = 1 AND ([MinimumAllowed] IS NULL OR [MeasuredValue] >= [MinimumAllowed]) AND ([MaximumAllowed] IS NULL OR [MeasuredValue] <= [MaximumAllowed])) OR ([Passed] = 0 AND (([MinimumAllowed] IS NOT NULL AND [MeasuredValue] < [MinimumAllowed]) OR ([MaximumAllowed] IS NOT NULL AND [MeasuredValue] > [MaximumAllowed])))),
        CONSTRAINT [CK_CalibrationMeasurements_Thresholds] CHECK (([MinimumAllowed] IS NOT NULL OR [MaximumAllowed] IS NOT NULL) AND ([MinimumAllowed] IS NULL OR [MaximumAllowed] IS NULL OR [MinimumAllowed] <= [MaximumAllowed])),
        CONSTRAINT [FK_CalibrationMeasurements_WorkOrderAttempts_WorkOrderAttemptId] FOREIGN KEY ([WorkOrderAttemptId]) REFERENCES [WorkOrderAttempts] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_EntityType_EntityId_OccurredAtUtc] ON [AuditEntries] ([EntityType], [EntityId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CalibrationMeasurements_WorkOrderAttemptId_ParameterCode] ON [CalibrationMeasurements] ([WorkOrderAttemptId], [ParameterCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_IdempotencyRequests_ActorScope_OperationName_IdempotencyKey] ON [IdempotencyRequests] ([ActorScope], [OperationName], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MaintenanceSchedules_IsActive_NextOccurrenceAtUtc] ON [MaintenanceSchedules] ([IsActive], [NextOccurrenceAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_MaintenanceSchedules_Code] ON [MaintenanceSchedules] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_OccurrenceReminders_EventId] ON [OccurrenceReminders] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_OccurrenceReminders_OccurrenceId_ReminderKind] ON [OccurrenceReminders] ([OccurrenceId], [ReminderKind]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_Status_NextAttemptAtUtc_OccurredAtUtc] ON [OutboxMessages] ([Status], [NextAttemptAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScheduleOccurrences_State_ScheduledOccurrenceAtUtc] ON [ScheduleOccurrences] ([State], [ScheduledOccurrenceAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ScheduleOccurrences_ScheduleId_ScheduledOccurrenceAtUtc] ON [ScheduleOccurrences] ([ScheduleId], [ScheduledOccurrenceAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WorkOrderActivities_WorkOrderId_OccurredAtUtc_Id] ON [WorkOrderActivities] ([WorkOrderId], [OccurredAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_WorkOrderAttempts_WorkOrderId] ON [WorkOrderAttempts] ([WorkOrderId]) WHERE [ReviewDecision] = N''Accepted''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_WorkOrderAttempts_WorkOrderId_AttemptNumber] ON [WorkOrderAttempts] ([WorkOrderId], [AttemptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_AssignedTechnicianId_Status] ON [WorkOrders] ([AssignedTechnicianId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_EquipmentId_Status] ON [WorkOrders] ([EquipmentId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_Status_DueAtUtc_Id] ON [WorkOrders] ([Status], [DueAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_WorkOrders_Code] ON [WorkOrders] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_WorkOrders_OccurrenceId] ON [WorkOrders] ([OccurrenceId]) WHERE [OccurrenceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044400_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261023044400_InitialCreate', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105101458_AddOccurrenceReminderWindow'
)
BEGIN
    ALTER TABLE [ScheduleOccurrences] ADD [ReminderWindowStartAtUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105101458_AddOccurrenceReminderWindow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105101458_AddOccurrenceReminderWindow', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106073414_AddSourceRetirementGate'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [EquipmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106073414_AddSourceRetirementGate'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [RequestPayloadJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106073414_AddSourceRetirementGate'
)
BEGIN
    CREATE TABLE [EquipmentRetirementGates] (
        [EquipmentId] uniqueidentifier NOT NULL,
        [CurrentIntentId] uniqueidentifier NULL,
        [HighestRetirementEpoch] bigint NOT NULL,
        [Phase] nvarchar(20) NOT NULL,
        [SourceGateVersion] bigint NOT NULL,
        [ReadyProofId] uniqueidentifier NULL,
        [CancelAcknowledgementId] uniqueidentifier NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EquipmentRetirementGates] PRIMARY KEY ([EquipmentId]),
        CONSTRAINT [CK_SourceRetirementGate_Binding] CHECK (([HighestRetirementEpoch]=0 AND [CurrentIntentId] IS NULL AND [Phase]=N'Open' AND [ReadyProofId] IS NULL AND [CancelAcknowledgementId] IS NULL) OR ([HighestRetirementEpoch]>0 AND [CurrentIntentId] IS NOT NULL)),
        CONSTRAINT [CK_SourceRetirementGate_Phase] CHECK ([Phase] IN (N'Open',N'Frozen',N'Sealed',N'Retired')),
        CONSTRAINT [CK_SourceRetirementGate_Proof] CHECK (([Phase]=N'Frozen' AND [ReadyProofId] IS NULL AND [CancelAcknowledgementId] IS NULL) OR ([Phase] IN (N'Sealed',N'Retired') AND [ReadyProofId] IS NOT NULL AND [CancelAcknowledgementId] IS NULL) OR ([Phase]=N'Open' AND ([HighestRetirementEpoch]=0 OR [CancelAcknowledgementId] IS NOT NULL))),
        CONSTRAINT [CK_SourceRetirementGate_Utc] CHECK (DATEPART(TZOFFSET,[CreatedAtUtc])=0 AND DATEPART(TZOFFSET,[UpdatedAtUtc])=0 AND [UpdatedAtUtc]>=[CreatedAtUtc]),
        CONSTRAINT [CK_SourceRetirementGate_Version] CHECK ([HighestRetirementEpoch]>=0 AND [SourceGateVersion]>=0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106073414_AddSourceRetirementGate'
)
BEGIN
    CREATE INDEX [IX_IdempotencyRequests_EquipmentId_State_OperationName] ON [IdempotencyRequests] ([EquipmentId], [State], [OperationName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106073414_AddSourceRetirementGate'
)
BEGIN
    EXEC(N'ALTER TABLE [IdempotencyRequests] ADD CONSTRAINT [CK_IdempotencyRequests_RequestPayload] CHECK ([RequestPayloadJson] IS NULL OR ISJSON([RequestPayloadJson])=1)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106073414_AddSourceRetirementGate'
)
BEGIN
    CREATE INDEX [IX_EquipmentRetirementGates_Phase_UpdatedAtUtc_EquipmentId] ON [EquipmentRetirementGates] ([Phase], [UpdatedAtUtc], [EquipmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106073414_AddSourceRetirementGate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106073414_AddSourceRetirementGate', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [AttemptCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [CancelIntentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [CancelReason] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [CancelRequestedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [CurrentAttemptId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [FencingToken] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [InterventionRequired] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [LastAttemptAtUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [LastError] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [LeaseOwner] nvarchar(128) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [LeaseUntilUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [NextRetryAtUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [OccurrenceId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [TotalAttemptCount] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD [UpdatedAtUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_IdempotencyRequests_OccurrenceId] ON [IdempotencyRequests] ([OccurrenceId]) WHERE [OccurrenceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    CREATE INDEX [IX_IdempotencyRequests_State_InterventionRequired_NextRetryAtUtc_OperationId] ON [IdempotencyRequests] ([State], [InterventionRequired], [NextRetryAtUtc], [OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    EXEC(N'ALTER TABLE [IdempotencyRequests] ADD CONSTRAINT [CK_IdempotencyRequests_Cancellation] CHECK (([CancelIntentId] IS NULL AND [CancelRequestedByUserId] IS NULL AND [CancelReason] IS NULL) OR ([CancelIntentId] IS NOT NULL AND [CancelRequestedByUserId] IS NOT NULL AND LEN([CancelReason])>0))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    EXEC(N'ALTER TABLE [IdempotencyRequests] ADD CONSTRAINT [CK_IdempotencyRequests_D3Counters] CHECK ([AttemptCount]>=0 AND [TotalAttemptCount]>=[AttemptCount] AND [FencingToken]>=0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    EXEC(N'ALTER TABLE [IdempotencyRequests] ADD CONSTRAINT [CK_IdempotencyRequests_Lease] CHECK (([LeaseOwner] IS NULL AND [LeaseUntilUtc] IS NULL) OR ([LeaseOwner] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL AND [CurrentAttemptId] IS NOT NULL AND [FencingToken]>0))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    EXEC(N'ALTER TABLE [IdempotencyRequests] ADD CONSTRAINT [CK_IdempotencyRequests_WorkOrderPayload] CHECK ([OccurrenceId] IS NULL OR ([OperationName]=N''CreateWorkOrder'' AND [EquipmentId] IS NOT NULL AND [RequestPayloadJson] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    ALTER TABLE [IdempotencyRequests] ADD CONSTRAINT [FK_IdempotencyRequests_ScheduleOccurrences_OccurrenceId] FOREIGN KEY ([OccurrenceId]) REFERENCES [ScheduleOccurrences] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106154420_AddWorkOrderD3Operations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106154420_AddWorkOrderD3Operations', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106162215_AddWorkOrderOperationPhoto'
)
BEGIN
    ALTER TABLE [WorkOrderAttempts] ADD [OperationPhoto] varbinary(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106162215_AddWorkOrderOperationPhoto'
)
BEGIN
    ALTER TABLE [WorkOrderAttempts] ADD [OperationPhotoContentType] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106162215_AddWorkOrderOperationPhoto'
)
BEGIN
    EXEC(N'ALTER TABLE [WorkOrderAttempts] ADD CONSTRAINT [CK_WorkOrderAttempts_Photo] CHECK (([OperationPhoto] IS NULL AND [OperationPhotoContentType] IS NULL) OR ([OperationPhoto] IS NOT NULL AND [OperationPhotoContentType] IS NOT NULL AND DATALENGTH([OperationPhoto]) BETWEEN 1 AND 5242880 AND [OperationPhotoContentType] IN (N''image/png'',N''image/jpeg'')))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106162215_AddWorkOrderOperationPhoto'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106162215_AddWorkOrderOperationPhoto', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106170013_AddScheduleTermination'
)
BEGIN
    ALTER TABLE [MaintenanceSchedules] ADD [EndReason] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106170013_AddScheduleTermination'
)
BEGIN
    ALTER TABLE [MaintenanceSchedules] ADD [EndedAtUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106170013_AddScheduleTermination'
)
BEGIN
    ALTER TABLE [MaintenanceSchedules] ADD [EndedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106170013_AddScheduleTermination'
)
BEGIN
    ALTER TABLE [MaintenanceSchedules] ADD [RegistrationReleasedAtUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106170013_AddScheduleTermination'
)
BEGIN
    CREATE INDEX [IX_MaintenanceSchedules_EndedAtUtc_RegistrationReleasedAtUtc_Id] ON [MaintenanceSchedules] ([EndedAtUtc], [RegistrationReleasedAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106170013_AddScheduleTermination'
)
BEGIN
    EXEC(N'ALTER TABLE [MaintenanceSchedules] ADD CONSTRAINT [CK_MaintenanceSchedules_End] CHECK (([EndedAtUtc] IS NULL AND [EndedByUserId] IS NULL AND [EndReason] IS NULL AND [RegistrationReleasedAtUtc] IS NULL) OR ([EndedAtUtc] IS NOT NULL AND [EndedByUserId] IS NOT NULL AND [EndReason] IS NOT NULL AND LEN(LTRIM(RTRIM([EndReason])))>0 AND [IsActive]=0 AND ([RegistrationReleasedAtUtc] IS NULL OR [RegistrationReleasedAtUtc]>=[EndedAtUtc])))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106170013_AddScheduleTermination'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106170013_AddScheduleTermination', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106185052_AddUserAccessInbox'
)
BEGIN
    ALTER TABLE [WorkOrders] ADD [AssignedAuthorizationVersion] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106185052_AddUserAccessInbox'
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
    WHERE [MigrationId] = N'20261106185052_AddUserAccessInbox'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106185052_AddUserAccessInbox', N'10.0.13');
END;

COMMIT;
GO
