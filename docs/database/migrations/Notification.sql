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
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
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
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
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
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
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
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE TABLE [NotificationPreferences] (
        [UserId] uniqueidentifier NOT NULL,
        [NotificationKind] nvarchar(50) NOT NULL,
        [InAppEnabled] bit NOT NULL,
        [EmailEnabled] bit NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY ([UserId], [NotificationKind])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [RecipientUserId] uniqueidentifier NOT NULL,
        [SourceEventId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(50) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [ResourceType] nvarchar(30) NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [DepartmentIdSnapshot] uniqueidentifier NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [ReadAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE TABLE [EmailDeliveries] (
        [Id] uniqueidentifier NOT NULL,
        [NotificationId] uniqueidentifier NOT NULL,
        [ToAddressSnapshot] nvarchar(256) NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [MessageId] nvarchar(200) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [AttemptCount] int NOT NULL,
        [NextAttemptAtUtc] datetimeoffset NULL,
        [LeaseOwner] nvarchar(100) NULL,
        [LeaseUntilUtc] datetimeoffset NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [SentAtUtc] datetimeoffset NULL,
        [LastError] nvarchar(2000) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EmailDeliveries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EmailDeliveries_AttemptCount] CHECK ([AttemptCount] >= 0),
        CONSTRAINT [CK_EmailDeliveries_Sent] CHECK (([Status] = N'Sent' AND [SentAtUtc] IS NOT NULL) OR ([Status] <> N'Sent' AND [SentAtUtc] IS NULL)),
        CONSTRAINT [CK_EmailDeliveries_Status] CHECK ([Status] IN (N'Pending',N'Processing',N'Sent',N'Failed')),
        CONSTRAINT [FK_EmailDeliveries_Notifications_NotificationId] FOREIGN KEY ([NotificationId]) REFERENCES [Notifications] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE TABLE [EmailDeliveryAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [EmailDeliveryId] uniqueidentifier NOT NULL,
        [AttemptNumber] int NOT NULL,
        [StartedAtUtc] datetimeoffset NOT NULL,
        [FinishedAtUtc] datetimeoffset NULL,
        [Outcome] nvarchar(20) NOT NULL,
        [SmtpResponse] nvarchar(2000) NULL,
        [ErrorCode] nvarchar(100) NULL,
        CONSTRAINT [PK_EmailDeliveryAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EmailDeliveryAttempts_AttemptNumber] CHECK ([AttemptNumber] > 0),
        CONSTRAINT [CK_EmailDeliveryAttempts_Outcome] CHECK ([Outcome] IN (N'Processing',N'Sent',N'Failed',N'Unknown')),
        CONSTRAINT [FK_EmailDeliveryAttempts_EmailDeliveries_EmailDeliveryId] FOREIGN KEY ([EmailDeliveryId]) REFERENCES [EmailDeliveries] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_EntityType_EntityId_OccurredAtUtc] ON [AuditEntries] ([EntityType], [EntityId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EmailDeliveries_Status_NextAttemptAtUtc] ON [EmailDeliveries] ([Status], [NextAttemptAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_EmailDeliveries_MessageId] ON [EmailDeliveries] ([MessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_EmailDeliveries_NotificationId] ON [EmailDeliveries] ([NotificationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_EmailDeliveryAttempts_EmailDeliveryId_AttemptNumber] ON [EmailDeliveryAttempts] ([EmailDeliveryId], [AttemptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_IdempotencyRequests_ActorScope_OperationName_IdempotencyKey] ON [IdempotencyRequests] ([ActorScope], [OperationName], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_RecipientUserId_ReadAtUtc_CreatedAtUtc_Id] ON [Notifications] ([RecipientUserId], [ReadAtUtc], [CreatedAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Notifications_SourceEventId_RecipientUserId_Kind] ON [Notifications] ([SourceEventId], [RecipientUserId], [Kind]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023044411_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261023044411_InitialCreate', N'10.0.13');
END;

COMMIT;
GO
