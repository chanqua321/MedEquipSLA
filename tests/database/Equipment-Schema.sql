-- Generated from approved erd-model.json. Read-only schema assertions.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @expected TABLE (TableName sysname, ColumnName sysname, SqlType nvarchar(100), IsNullable bit);
INSERT INTO @expected VALUES
(N'Departments',N'Id',N'uniqueidentifier',0),
(N'Departments',N'Code',N'nvarchar(50)',0),
(N'Departments',N'Name',N'nvarchar(200)',0),
(N'Departments',N'IsActive',N'bit',0),
(N'Departments',N'CreatedAtUtc',N'datetimeoffset',0),
(N'Departments',N'UpdatedAtUtc',N'datetimeoffset',0),
(N'Departments',N'CreatedByUserId',N'uniqueidentifier',1),
(N'Departments',N'UpdatedByUserId',N'uniqueidentifier',1),
(N'Departments',N'RowVersion',N'rowversion',0),
(N'EquipmentCategories',N'Id',N'uniqueidentifier',0),
(N'EquipmentCategories',N'Code',N'nvarchar(50)',0),
(N'EquipmentCategories',N'Name',N'nvarchar(200)',0),
(N'EquipmentCategories',N'Description',N'nvarchar(2000)',1),
(N'EquipmentCategories',N'IsActive',N'bit',0),
(N'EquipmentCategories',N'CreatedAtUtc',N'datetimeoffset',0),
(N'EquipmentCategories',N'UpdatedAtUtc',N'datetimeoffset',0),
(N'EquipmentCategories',N'CreatedByUserId',N'uniqueidentifier',1),
(N'EquipmentCategories',N'UpdatedByUserId',N'uniqueidentifier',1),
(N'EquipmentCategories',N'RowVersion',N'rowversion',0),
(N'EquipmentAssets',N'Id',N'uniqueidentifier',0),
(N'EquipmentAssets',N'AssetCode',N'nvarchar(50)',0),
(N'EquipmentAssets',N'SerialNumber',N'nvarchar(100)',1),
(N'EquipmentAssets',N'Name',N'nvarchar(200)',0),
(N'EquipmentAssets',N'CategoryId',N'uniqueidentifier',0),
(N'EquipmentAssets',N'DepartmentId',N'uniqueidentifier',0),
(N'EquipmentAssets',N'Manufacturer',N'nvarchar(200)',1),
(N'EquipmentAssets',N'Model',N'nvarchar(100)',1),
(N'EquipmentAssets',N'PurchasedOn',N'date',1),
(N'EquipmentAssets',N'WarrantyUntil',N'date',1),
(N'EquipmentAssets',N'IsRetired',N'bit',0),
(N'EquipmentAssets',N'OperationalStatus',N'nvarchar(30)',0),
(N'EquipmentAssets',N'RetiredAtUtc',N'datetimeoffset',1),
(N'EquipmentAssets',N'RetiredByUserId',N'uniqueidentifier',1),
(N'EquipmentAssets',N'RetirementReason',N'nvarchar(2000)',1),
(N'EquipmentAssets',N'AggregateVersion',N'bigint',0),
(N'EquipmentAssets',N'CreatedAtUtc',N'datetimeoffset',0),
(N'EquipmentAssets',N'UpdatedAtUtc',N'datetimeoffset',0),
(N'EquipmentAssets',N'CreatedByUserId',N'uniqueidentifier',1),
(N'EquipmentAssets',N'UpdatedByUserId',N'uniqueidentifier',1),
(N'EquipmentAssets',N'RowVersion',N'rowversion',0),
(N'EquipmentStateProjections',N'SourceService',N'nvarchar(30)',0),
(N'EquipmentStateProjections',N'SourceAggregateId',N'uniqueidentifier',0),
(N'EquipmentStateProjections',N'EquipmentId',N'uniqueidentifier',0),
(N'EquipmentStateProjections',N'SourceStatus',N'nvarchar(30)',0),
(N'EquipmentStateProjections',N'IsBlocking',N'bit',0),
(N'EquipmentStateProjections',N'SourceVersion',N'bigint',0),
(N'EquipmentStateProjections',N'LastEventId',N'uniqueidentifier',0),
(N'EquipmentStateProjections',N'LastOccurredAtUtc',N'datetimeoffset',0),
(N'EquipmentStateProjections',N'AppliedAtUtc',N'datetimeoffset',0),
(N'EquipmentStateProjections',N'RowVersion',N'rowversion',0),
(N'EquipmentUseRegistrations',N'Id',N'uniqueidentifier',0),
(N'EquipmentUseRegistrations',N'EquipmentId',N'uniqueidentifier',0),
(N'EquipmentUseRegistrations',N'SourceService',N'nvarchar(30)',0),
(N'EquipmentUseRegistrations',N'ResourceType',N'nvarchar(30)',0),
(N'EquipmentUseRegistrations',N'ResourceId',N'uniqueidentifier',0),
(N'EquipmentUseRegistrations',N'SourceOperationId',N'uniqueidentifier',0),
(N'EquipmentUseRegistrations',N'State',N'nvarchar(20)',0),
(N'EquipmentUseRegistrations',N'SourceVersion',N'bigint',0),
(N'EquipmentUseRegistrations',N'RegisteredAtUtc',N'datetimeoffset',0),
(N'EquipmentUseRegistrations',N'ConfirmedAtUtc',N'datetimeoffset',1),
(N'EquipmentUseRegistrations',N'ReleasedAtUtc',N'datetimeoffset',1),
(N'EquipmentUseRegistrations',N'LastVerifiedAtUtc',N'datetimeoffset',1),
(N'EquipmentUseRegistrations',N'RowVersion',N'rowversion',0),
(N'EquipmentStatusHistories',N'Id',N'uniqueidentifier',0),
(N'EquipmentStatusHistories',N'EquipmentId',N'uniqueidentifier',0),
(N'EquipmentStatusHistories',N'ChangeKind',N'nvarchar(30)',0),
(N'EquipmentStatusHistories',N'FromStatus',N'nvarchar(30)',1),
(N'EquipmentStatusHistories',N'ToStatus',N'nvarchar(30)',1),
(N'EquipmentStatusHistories',N'FromDepartmentId',N'uniqueidentifier',1),
(N'EquipmentStatusHistories',N'ToDepartmentId',N'uniqueidentifier',1),
(N'EquipmentStatusHistories',N'FromDepartmentNameSnapshot',N'nvarchar(200)',1),
(N'EquipmentStatusHistories',N'ToDepartmentNameSnapshot',N'nvarchar(200)',1),
(N'EquipmentStatusHistories',N'Reason',N'nvarchar(2000)',1),
(N'EquipmentStatusHistories',N'ActorUserId',N'uniqueidentifier',1),
(N'EquipmentStatusHistories',N'SourceEventId',N'uniqueidentifier',1),
(N'EquipmentStatusHistories',N'ChangedAtUtc',N'datetimeoffset',0),
(N'ReconciliationCheckpoints',N'SourceService',N'nvarchar(30)',0),
(N'ReconciliationCheckpoints',N'SnapshotId',N'uniqueidentifier',1),
(N'ReconciliationCheckpoints',N'SnapshotWatermark',N'nvarchar(256)',1),
(N'ReconciliationCheckpoints',N'Cursor',N'nvarchar(512)',1),
(N'ReconciliationCheckpoints',N'State',N'nvarchar(20)',0),
(N'ReconciliationCheckpoints',N'StartedAtUtc',N'datetimeoffset',1),
(N'ReconciliationCheckpoints',N'LastCompletedAtUtc',N'datetimeoffset',1),
(N'ReconciliationCheckpoints',N'LastError',N'nvarchar(2000)',1),
(N'ReconciliationCheckpoints',N'RowVersion',N'rowversion',0),
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
(N'OutboxMessages',N'EventId',N'uniqueidentifier',0),
(N'OutboxMessages',N'EventType',N'nvarchar(150)',0),
(N'OutboxMessages',N'SchemaVersion',N'int',0),
(N'OutboxMessages',N'AggregateType',N'nvarchar(100)',0),
(N'OutboxMessages',N'AggregateId',N'uniqueidentifier',0),
(N'OutboxMessages',N'AggregateVersion',N'bigint',0),
(N'OutboxMessages',N'OccurredAtUtc',N'datetimeoffset',0),
(N'OutboxMessages',N'CorrelationId',N'nvarchar(100)',0),
(N'OutboxMessages',N'PayloadJson',N'nvarchar(max)',0),
(N'OutboxMessages',N'Status',N'nvarchar(20)',0),
(N'OutboxMessages',N'AttemptCount',N'int',0),
(N'OutboxMessages',N'NextAttemptAtUtc',N'datetimeoffset',1),
(N'OutboxMessages',N'LeaseOwner',N'nvarchar(100)',1),
(N'OutboxMessages',N'LeaseUntilUtc',N'datetimeoffset',1),
(N'OutboxMessages',N'PublishedAtUtc',N'datetimeoffset',1),
(N'OutboxMessages',N'LastError',N'nvarchar(2000)',1),
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
(N'Departments',N'Id'),
(N'EquipmentCategories',N'Id'),
(N'EquipmentAssets',N'Id'),
(N'EquipmentStateProjections',N'SourceService,SourceAggregateId'),
(N'EquipmentUseRegistrations',N'Id'),
(N'EquipmentStatusHistories',N'Id'),
(N'ReconciliationCheckpoints',N'SourceService'),
(N'AuditEntries',N'Id'),
(N'IdempotencyRequests',N'OperationId'),
(N'OutboxMessages',N'EventId'),
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
(N'EquipmentAssets',N'DepartmentId',N'Departments',N'Id',1),
(N'EquipmentAssets',N'CategoryId',N'EquipmentCategories',N'Id',1),
(N'EquipmentStateProjections',N'EquipmentId',N'EquipmentAssets',N'Id',1),
(N'EquipmentUseRegistrations',N'EquipmentId',N'EquipmentAssets',N'Id',1),
(N'EquipmentStatusHistories',N'EquipmentId',N'EquipmentAssets',N'Id',1),
(N'EquipmentStatusHistories',N'FromDepartmentId',N'Departments',N'Id',1),
(N'EquipmentStatusHistories',N'ToDepartmentId',N'Departments',N'Id',1);

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
