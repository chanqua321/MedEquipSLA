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
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
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
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
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
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
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
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE TABLE [SlaPolicies] (
        [Id] uniqueidentifier NOT NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [UpdatedByUserId] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SlaPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SlaPolicies_Priority] CHECK ([Priority] IN (N'Critical',N'High',N'Medium',N'Low'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE TABLE [SlaPolicyVersions] (
        [SlaPolicyId] uniqueidentifier NOT NULL,
        [Version] int NOT NULL,
        [ResponseDurationMinutes] int NOT NULL,
        [ResolutionDurationMinutes] int NOT NULL,
        [EffectiveFromUtc] datetimeoffset NOT NULL,
        [EffectiveToUtc] datetimeoffset NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SlaPolicyVersions] PRIMARY KEY ([SlaPolicyId], [Version]),
        CONSTRAINT [CK_SlaPolicyVersions_Durations] CHECK ([Version] > 0 AND [ResponseDurationMinutes] > 0 AND [ResolutionDurationMinutes] >= [ResponseDurationMinutes]),
        CONSTRAINT [CK_SlaPolicyVersions_EffectiveInterval] CHECK ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]),
        CONSTRAINT [FK_SlaPolicyVersions_SlaPolicies_SlaPolicyId] FOREIGN KEY ([SlaPolicyId]) REFERENCES [SlaPolicies] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE TABLE [Incidents] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [EquipmentId] uniqueidentifier NOT NULL,
        [EquipmentCodeSnapshot] nvarchar(50) NOT NULL,
        [EquipmentNameSnapshot] nvarchar(200) NOT NULL,
        [DepartmentIdSnapshot] uniqueidentifier NOT NULL,
        [DepartmentNameSnapshot] nvarchar(200) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [ReportedByUserId] uniqueidentifier NOT NULL,
        [AssignedTechnicianId] uniqueidentifier NULL,
        [RelatedIncidentId] uniqueidentifier NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [SlaPolicyId] uniqueidentifier NOT NULL,
        [SlaPolicyVersion] int NOT NULL,
        [SlaPrioritySnapshot] nvarchar(20) NOT NULL,
        [ResponseDurationMinutesSnapshot] int NOT NULL,
        [ResolutionDurationMinutesSnapshot] int NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [ResponseDeadlineAtUtc] datetimeoffset NOT NULL,
        [ResolutionDeadlineAtUtc] datetimeoffset NOT NULL,
        [FirstRespondedAtUtc] datetimeoffset NULL,
        [ClosedAtUtc] datetimeoffset NULL,
        [ResponseOutcome] nvarchar(20) NOT NULL,
        [ResolutionOutcome] nvarchar(20) NOT NULL,
        [SlaFinalizedAtUtc] datetimeoffset NULL,
        [EquipmentUseRegistrationId] uniqueidentifier NOT NULL,
        [AggregateVersion] bigint NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [UpdatedByUserId] uniqueidentifier NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Incidents] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Incidents_AggregateVersion] CHECK ([AggregateVersion] >= 1),
        CONSTRAINT [CK_Incidents_Finalization] CHECK (([Status] = N'Closed' AND [ClosedAtUtc] IS NOT NULL AND [SlaFinalizedAtUtc] IS NOT NULL AND [ResponseOutcome] <> N'Pending' AND [ResolutionOutcome] <> N'Pending') OR ([Status] <> N'Closed' AND [ClosedAtUtc] IS NULL AND [SlaFinalizedAtUtc] IS NULL)),
        CONSTRAINT [CK_Incidents_Priority] CHECK ([Priority] IN (N'Critical',N'High',N'Medium',N'Low')),
        CONSTRAINT [CK_Incidents_RelatedIncident] CHECK ([RelatedIncidentId] IS NULL OR [RelatedIncidentId] <> [Id]),
        CONSTRAINT [CK_Incidents_ResolutionOutcome] CHECK ([ResolutionOutcome] IN (N'Pending',N'Met',N'Breached')),
        CONSTRAINT [CK_Incidents_ResponseOutcome] CHECK ([ResponseOutcome] IN (N'Pending',N'Met',N'Breached')),
        CONSTRAINT [CK_Incidents_SlaDeadlines] CHECK ([ResponseDeadlineAtUtc] = DATEADD(minute,[ResponseDurationMinutesSnapshot],[CreatedAtUtc]) AND [ResolutionDeadlineAtUtc] = DATEADD(minute,[ResolutionDurationMinutesSnapshot],[CreatedAtUtc])),
        CONSTRAINT [CK_Incidents_SlaDurations] CHECK ([ResponseDurationMinutesSnapshot] > 0 AND [ResolutionDurationMinutesSnapshot] >= [ResponseDurationMinutesSnapshot]),
        CONSTRAINT [CK_Incidents_SlaPrioritySnapshot] CHECK ([SlaPrioritySnapshot] IN (N'Critical',N'High',N'Medium',N'Low')),
        CONSTRAINT [CK_Incidents_Status] CHECK ([Status] IN (N'Open',N'InProgress',N'Resolved',N'Closed')),
        CONSTRAINT [FK_Incidents_Incidents_RelatedIncidentId] FOREIGN KEY ([RelatedIncidentId]) REFERENCES [Incidents] ([Id]),
        CONSTRAINT [FK_Incidents_SlaPolicyVersions_SlaPolicyId_SlaPolicyVersion] FOREIGN KEY ([SlaPolicyId], [SlaPolicyVersion]) REFERENCES [SlaPolicyVersions] ([SlaPolicyId], [Version])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE TABLE [IncidentActivities] (
        [Id] uniqueidentifier NOT NULL,
        [IncidentId] uniqueidentifier NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [FromStatus] nvarchar(20) NULL,
        [ToStatus] nvarchar(20) NULL,
        [FromTechnicianId] uniqueidentifier NULL,
        [ToTechnicianId] uniqueidentifier NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Reason] nvarchar(2000) NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_IncidentActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IncidentActivities_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [Incidents] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE TABLE [ResolutionAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [IncidentId] uniqueidentifier NOT NULL,
        [AttemptNumber] int NOT NULL,
        [PerformedByUserId] uniqueidentifier NOT NULL,
        [ResolutionNotes] nvarchar(4000) NOT NULL,
        [ResultSummary] nvarchar(2000) NOT NULL,
        [CompletedAtUtc] datetimeoffset NOT NULL,
        [ReviewDecision] nvarchar(20) NOT NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedAtUtc] datetimeoffset NULL,
        [ReviewReason] nvarchar(2000) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ResolutionAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ResolutionAttempts_AttemptNumber] CHECK ([AttemptNumber] > 0),
        CONSTRAINT [CK_ResolutionAttempts_RejectedReason] CHECK ([ReviewDecision] <> N'Rejected' OR NULLIF(LTRIM(RTRIM([ReviewReason])),N'') IS NOT NULL),
        CONSTRAINT [CK_ResolutionAttempts_Review] CHECK (([ReviewDecision] = N'Pending' AND [ReviewedByUserId] IS NULL AND [ReviewedAtUtc] IS NULL) OR ([ReviewDecision] IN (N'Accepted',N'Rejected') AND [ReviewedByUserId] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL AND [ReviewedAtUtc] >= [CompletedAtUtc])),
        CONSTRAINT [CK_ResolutionAttempts_ReviewDecision] CHECK ([ReviewDecision] IN (N'Pending',N'Accepted',N'Rejected')),
        CONSTRAINT [FK_ResolutionAttempts_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [Incidents] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE TABLE [SlaDeadlineAlerts] (
        [Id] uniqueidentifier NOT NULL,
        [IncidentId] uniqueidentifier NOT NULL,
        [AlertKind] nvarchar(20) NOT NULL,
        [DeadlineAtUtc] datetimeoffset NOT NULL,
        [DetectedAtUtc] datetimeoffset NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SlaDeadlineAlerts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SlaDeadlineAlerts_AfterDeadline] CHECK ([DetectedAtUtc] > [DeadlineAtUtc]),
        CONSTRAINT [CK_SlaDeadlineAlerts_AlertKind] CHECK ([AlertKind] IN (N'Response',N'Resolution')),
        CONSTRAINT [FK_SlaDeadlineAlerts_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [Incidents] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_EntityType_EntityId_OccurredAtUtc] ON [AuditEntries] ([EntityType], [EntityId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_IdempotencyRequests_ActorScope_OperationName_IdempotencyKey] ON [IdempotencyRequests] ([ActorScope], [OperationName], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_IncidentActivities_IncidentId_OccurredAtUtc_Id] ON [IncidentActivities] ([IncidentId], [OccurredAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incidents_AssignedTechnicianId_Status] ON [Incidents] ([AssignedTechnicianId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incidents_DepartmentIdSnapshot_CreatedAtUtc_Id] ON [Incidents] ([DepartmentIdSnapshot], [CreatedAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incidents_EquipmentId_Status] ON [Incidents] ([EquipmentId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incidents_RelatedIncidentId] ON [Incidents] ([RelatedIncidentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incidents_SlaPolicyId_SlaPolicyVersion] ON [Incidents] ([SlaPolicyId], [SlaPolicyVersion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incidents_Status_ResolutionDeadlineAtUtc] ON [Incidents] ([Status], [ResolutionDeadlineAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incidents_Status_ResponseDeadlineAtUtc] ON [Incidents] ([Status], [ResponseDeadlineAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Incidents_Code] ON [Incidents] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_Status_NextAttemptAtUtc_OccurredAtUtc] ON [OutboxMessages] ([Status], [NextAttemptAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ResolutionAttempts_IncidentId] ON [ResolutionAttempts] ([IncidentId]) WHERE [ReviewDecision] = N''Accepted''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ResolutionAttempts_IncidentId_AttemptNumber] ON [ResolutionAttempts] ([IncidentId], [AttemptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_SlaDeadlineAlerts_EventId] ON [SlaDeadlineAlerts] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_SlaDeadlineAlerts_IncidentId_AlertKind] ON [SlaDeadlineAlerts] ([IncidentId], [AlertKind]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_SlaPolicies_Priority] ON [SlaPolicies] ([Priority]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_SlaPolicyVersions_SlaPolicyId_EffectiveFromUtc] ON [SlaPolicyVersions] ([SlaPolicyId], [EffectiveFromUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044406_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261023044406_InitialCreate', N'10.0.13');
END;

COMMIT;
GO
