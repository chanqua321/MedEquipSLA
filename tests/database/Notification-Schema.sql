-- Generated from approved erd-model.json. Read-only schema assertions.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @expected TABLE (TableName sysname, ColumnName sysname, SqlType nvarchar(100), IsNullable bit);
INSERT INTO @expected VALUES
(N'Notifications',N'Id',N'uniqueidentifier',0),
(N'Notifications',N'RecipientUserId',N'uniqueidentifier',0),
(N'Notifications',N'SourceEventId',N'uniqueidentifier',0),
(N'Notifications',N'Kind',N'nvarchar(50)',0),
(N'Notifications',N'Title',N'nvarchar(200)',0),
(N'Notifications',N'Body',N'nvarchar(4000)',0),
(N'Notifications',N'ResourceType',N'nvarchar(30)',0),
(N'Notifications',N'ResourceId',N'uniqueidentifier',0),
(N'Notifications',N'DepartmentIdSnapshot',N'uniqueidentifier',1),
(N'Notifications',N'CreatedAtUtc',N'datetimeoffset',0),
(N'Notifications',N'ReadAtUtc',N'datetimeoffset',1),
(N'Notifications',N'RowVersion',N'rowversion',0),
(N'NotificationPreferences',N'UserId',N'uniqueidentifier',0),
(N'NotificationPreferences',N'NotificationKind',N'nvarchar(50)',0),
(N'NotificationPreferences',N'InAppEnabled',N'bit',0),
(N'NotificationPreferences',N'EmailEnabled',N'bit',0),
(N'NotificationPreferences',N'UpdatedAtUtc',N'datetimeoffset',0),
(N'NotificationPreferences',N'RowVersion',N'rowversion',0),
(N'EmailDeliveries',N'Id',N'uniqueidentifier',0),
(N'EmailDeliveries',N'NotificationId',N'uniqueidentifier',0),
(N'EmailDeliveries',N'ToAddressSnapshot',N'nvarchar(256)',0),
(N'EmailDeliveries',N'Subject',N'nvarchar(200)',0),
(N'EmailDeliveries',N'Body',N'nvarchar(max)',0),
(N'EmailDeliveries',N'MessageId',N'nvarchar(200)',0),
(N'EmailDeliveries',N'Status',N'nvarchar(20)',0),
(N'EmailDeliveries',N'AttemptCount',N'int',0),
(N'EmailDeliveries',N'NextAttemptAtUtc',N'datetimeoffset',1),
(N'EmailDeliveries',N'LeaseOwner',N'nvarchar(100)',1),
(N'EmailDeliveries',N'LeaseUntilUtc',N'datetimeoffset',1),
(N'EmailDeliveries',N'CreatedAtUtc',N'datetimeoffset',0),
(N'EmailDeliveries',N'SentAtUtc',N'datetimeoffset',1),
(N'EmailDeliveries',N'LastError',N'nvarchar(2000)',1),
(N'EmailDeliveries',N'RowVersion',N'rowversion',0),
(N'EmailDeliveryAttempts',N'Id',N'uniqueidentifier',0),
(N'EmailDeliveryAttempts',N'EmailDeliveryId',N'uniqueidentifier',0),
(N'EmailDeliveryAttempts',N'AttemptNumber',N'int',0),
(N'EmailDeliveryAttempts',N'StartedAtUtc',N'datetimeoffset',0),
(N'EmailDeliveryAttempts',N'FinishedAtUtc',N'datetimeoffset',1),
(N'EmailDeliveryAttempts',N'Outcome',N'nvarchar(20)',0),
(N'EmailDeliveryAttempts',N'SmtpResponse',N'nvarchar(2000)',1),
(N'EmailDeliveryAttempts',N'ErrorCode',N'nvarchar(100)',1),
(N'AuditEntries',N'Id',N'uniqueidentifier',0),
(N'AuditEntries',N'ActorUserId',N'uniqueidentifier',1),
(N'AuditEntries',N'ActorService',N'nvarchar(50)',1),
(N'AuditEntries',N'Action',N'nvarchar(100)',0),
(N'AuditEntries',N'EntityType',N'nvarchar(100)',0),
(N'AuditEntries',N'EntityId',N'nvarchar(128)',0),
(N'AuditEntries',N'OccurredAtUtc',N'datetimeoffset',0),
(N'AuditEntries',N'CorrelationId',N'nvarchar(100)',0),
(N'AuditEntries',N'ChangesJson',N'nvarchar(max)',1),
(N'IdempotencyRequests',N'OperationId',N'uniqueidentifier',0),
(N'IdempotencyRequests',N'ActorScope',N'nvarchar(128)',0),
(N'IdempotencyRequests',N'OperationName',N'nvarchar(80)',0),
(N'IdempotencyRequests',N'IdempotencyKey',N'nvarchar(128)',0),
(N'IdempotencyRequests',N'RequestHash',N'binary(32)',0),
(N'IdempotencyRequests',N'ResourceId',N'uniqueidentifier',1),
(N'IdempotencyRequests',N'State',N'nvarchar(20)',0),
(N'IdempotencyRequests',N'ResponseStatusCode',N'int',1),
(N'IdempotencyRequests',N'ResponseJson',N'nvarchar(max)',1),
(N'IdempotencyRequests',N'CreatedAtUtc',N'datetimeoffset',0),
(N'IdempotencyRequests',N'ExpiresAtUtc',N'datetimeoffset',0),
(N'IdempotencyRequests',N'RowVersion',N'rowversion',0),
(N'InboxMessages',N'ConsumerName',N'nvarchar(100)',0),
(N'InboxMessages',N'EventId',N'uniqueidentifier',0),
(N'InboxMessages',N'EventType',N'nvarchar(150)',0),
(N'InboxMessages',N'SchemaVersion',N'int',0),
(N'InboxMessages',N'ProcessedAtUtc',N'datetimeoffset',0);

DECLARE @actual TABLE (TableName sysname, ColumnName sysname, SqlType nvarchar(100), IsNullable bit);
INSERT INTO @actual
SELECT t.name,c.name,CASE
 WHEN ty.name IN ('nvarchar','nchar') THEN ty.name+'('+CASE WHEN c.max_length=-1 THEN 'max' ELSE CONVERT(varchar(10),c.max_length/2) END+')'
 WHEN ty.name IN ('varchar','char','binary','varbinary') THEN ty.name+'('+CASE WHEN c.max_length=-1 THEN 'max' ELSE CONVERT(varchar(10),c.max_length) END+')'
 WHEN ty.name IN ('decimal','numeric') THEN ty.name+'('+CONVERT(varchar(10),c.precision)+','+CONVERT(varchar(10),c.scale)+')'
 WHEN ty.name='timestamp' THEN 'rowversion' ELSE ty.name END,c.is_nullable
FROM sys.tables t JOIN sys.columns c ON t.object_id=c.object_id JOIN sys.types ty ON c.user_type_id=ty.user_type_id
WHERE t.is_ms_shipped=0 AND t.name <> '__EFMigrationsHistory';
IF EXISTS(SELECT * FROM @expected EXCEPT SELECT * FROM @actual) OR EXISTS(SELECT * FROM @actual EXCEPT SELECT * FROM @expected)
BEGIN
 SELECT 'Missing/mismatched' AS Issue,* FROM (SELECT * FROM @expected EXCEPT SELECT * FROM @actual) d;
 SELECT 'Unexpected/mismatched' AS Issue,* FROM (SELECT * FROM @actual EXCEPT SELECT * FROM @expected) d;
 THROW 51001,'Schema differs from approved ERD',1;
END;
DECLARE @pk TABLE (TableName sysname, Columns nvarchar(1000));
INSERT INTO @pk VALUES
(N'Notifications',N'Id'),
(N'NotificationPreferences',N'UserId,NotificationKind'),
(N'EmailDeliveries',N'Id'),
(N'EmailDeliveryAttempts',N'Id'),
(N'AuditEntries',N'Id'),
(N'IdempotencyRequests',N'OperationId'),
(N'InboxMessages',N'ConsumerName,EventId');

DECLARE @actualPk TABLE (TableName sysname, Columns nvarchar(1000));
INSERT INTO @actualPk SELECT t.name,STRING_AGG(CONVERT(nvarchar(max),c.name),',') WITHIN GROUP(ORDER BY ic.key_ordinal)
FROM sys.tables t JOIN sys.indexes i ON t.object_id=i.object_id AND i.is_primary_key=1
JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
JOIN sys.columns c ON ic.object_id=c.object_id AND ic.column_id=c.column_id
WHERE t.is_ms_shipped=0 AND t.name <> '__EFMigrationsHistory' GROUP BY t.name;
IF EXISTS(SELECT * FROM @pk EXCEPT SELECT * FROM @actualPk) OR EXISTS(SELECT * FROM @actualPk EXCEPT SELECT * FROM @pk)
 THROW 51002,'Primary keys differ from approved ERD',1;
DECLARE @fk TABLE (ChildTable sysname, ChildColumn sysname, ParentTable sysname, ParentColumn sysname, Ordinal int);
INSERT INTO @fk VALUES
(N'EmailDeliveries',N'NotificationId',N'Notifications',N'Id',1),
(N'EmailDeliveryAttempts',N'EmailDeliveryId',N'EmailDeliveries',N'Id',1);

DECLARE @actualFk TABLE (ChildTable sysname, ChildColumn sysname, ParentTable sysname, ParentColumn sysname, Ordinal int);
INSERT INTO @actualFk SELECT OBJECT_NAME(fkc.parent_object_id),COL_NAME(fkc.parent_object_id,fkc.parent_column_id),
 OBJECT_NAME(fkc.referenced_object_id),COL_NAME(fkc.referenced_object_id,fkc.referenced_column_id),fkc.constraint_column_id
FROM sys.foreign_key_columns fkc;
IF EXISTS(SELECT * FROM @fk EXCEPT SELECT * FROM @actualFk) OR EXISTS(SELECT * FROM @actualFk EXCEPT SELECT * FROM @fk)
 THROW 51003,'Foreign keys differ from approved ERD',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE delete_referential_action<>0 OR is_disabled=1 OR is_not_trusted=1)
 THROW 51004,'Foreign keys must be enabled, trusted and NO ACTION',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 THROW 51005,'Check constraints must be enabled and trusted',1;
IF EXISTS(SELECT 1 FROM sys.indexes WHERE is_disabled=1 OR is_hypothetical=1)
 THROW 51006,'Disabled or hypothetical index',1;
IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory)<>1 THROW 51007,'Expected one applied InitialCreate migration',1;
SELECT DB_NAME() AS DatabaseName, (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) AS TotalTables,
 (SELECT COUNT(*) FROM @expected) AS ApprovedColumns, (SELECT COUNT(*) FROM sys.foreign_keys) AS ForeignKeys,
 (SELECT COUNT(*) FROM sys.check_constraints) AS CheckConstraints;
PRINT 'PASS: approved schema, keys, constraints and migration history';
