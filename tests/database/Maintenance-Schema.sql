-- Generated from approved erd-model.json. Read-only schema assertions.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @expected TABLE (TableName sysname, ColumnName sysname, SqlType nvarchar(100), IsNullable bit);
INSERT INTO @expected VALUES
(N'MaintenanceSchedules',N'Id',N'uniqueidentifier',0),
(N'MaintenanceSchedules',N'Code',N'nvarchar(50)',0),
(N'MaintenanceSchedules',N'EquipmentId',N'uniqueidentifier',0),
(N'MaintenanceSchedules',N'EquipmentCodeSnapshot',N'nvarchar(50)',0),
(N'MaintenanceSchedules',N'EquipmentNameSnapshot',N'nvarchar(200)',0),
(N'MaintenanceSchedules',N'DepartmentIdSnapshot',N'uniqueidentifier',0),
(N'MaintenanceSchedules',N'DepartmentNameSnapshot',N'nvarchar(200)',0),
(N'MaintenanceSchedules',N'WorkType',N'nvarchar(20)',0),
(N'MaintenanceSchedules',N'AnchorAtUtc',N'datetimeoffset',0),
(N'MaintenanceSchedules',N'IntervalDays',N'int',0),
(N'MaintenanceSchedules',N'NextOccurrenceAtUtc',N'datetimeoffset',0),
(N'MaintenanceSchedules',N'ReminderLeadMinutes',N'int',0),
(N'MaintenanceSchedules',N'CompletionWindowMinutes',N'int',0),
(N'MaintenanceSchedules',N'IsActive',N'bit',0),
(N'MaintenanceSchedules',N'EquipmentUseRegistrationId',N'uniqueidentifier',0),
(N'MaintenanceSchedules',N'AggregateVersion',N'bigint',0),
(N'MaintenanceSchedules',N'CreatedAtUtc',N'datetimeoffset',0),
(N'MaintenanceSchedules',N'UpdatedAtUtc',N'datetimeoffset',0),
(N'MaintenanceSchedules',N'CreatedByUserId',N'uniqueidentifier',1),
(N'MaintenanceSchedules',N'UpdatedByUserId',N'uniqueidentifier',1),
(N'MaintenanceSchedules',N'RowVersion',N'rowversion',0),
(N'ScheduleOccurrences',N'Id',N'uniqueidentifier',0),
(N'ScheduleOccurrences',N'ScheduleId',N'uniqueidentifier',0),
(N'ScheduleOccurrences',N'ScheduledOccurrenceAtUtc',N'datetimeoffset',0),
(N'ScheduleOccurrences',N'DueAtUtc',N'datetimeoffset',0),
(N'ScheduleOccurrences',N'State',N'nvarchar(20)',0),
(N'ScheduleOccurrences',N'CreatedAtUtc',N'datetimeoffset',0),
(N'ScheduleOccurrences',N'CancelledAtUtc',N'datetimeoffset',1),
(N'ScheduleOccurrences',N'RowVersion',N'rowversion',0),
(N'WorkOrders',N'Id',N'uniqueidentifier',0),
(N'WorkOrders',N'Code',N'nvarchar(50)',0),
(N'WorkOrders',N'OccurrenceId',N'uniqueidentifier',1),
(N'WorkOrders',N'EquipmentId',N'uniqueidentifier',0),
(N'WorkOrders',N'EquipmentCodeSnapshot',N'nvarchar(50)',0),
(N'WorkOrders',N'EquipmentNameSnapshot',N'nvarchar(200)',0),
(N'WorkOrders',N'DepartmentIdSnapshot',N'uniqueidentifier',0),
(N'WorkOrders',N'DepartmentNameSnapshot',N'nvarchar(200)',0),
(N'WorkOrders',N'WorkType',N'nvarchar(20)',0),
(N'WorkOrders',N'Status',N'nvarchar(20)',0),
(N'WorkOrders',N'AssignedTechnicianId',N'uniqueidentifier',1),
(N'WorkOrders',N'ScheduledForUtc',N'datetimeoffset',0),
(N'WorkOrders',N'DueAtUtc',N'datetimeoffset',0),
(N'WorkOrders',N'StartedAtUtc',N'datetimeoffset',1),
(N'WorkOrders',N'ClosedAtUtc',N'datetimeoffset',1),
(N'WorkOrders',N'CancelledAtUtc',N'datetimeoffset',1),
(N'WorkOrders',N'CancellationReason',N'nvarchar(2000)',1),
(N'WorkOrders',N'IsBlocking',N'bit',0),
(N'WorkOrders',N'EquipmentUseRegistrationId',N'uniqueidentifier',0),
(N'WorkOrders',N'AggregateVersion',N'bigint',0),
(N'WorkOrders',N'CreatedAtUtc',N'datetimeoffset',0),
(N'WorkOrders',N'UpdatedAtUtc',N'datetimeoffset',0),
(N'WorkOrders',N'CreatedByUserId',N'uniqueidentifier',1),
(N'WorkOrders',N'UpdatedByUserId',N'uniqueidentifier',1),
(N'WorkOrders',N'RowVersion',N'rowversion',0),
(N'WorkOrderAttempts',N'Id',N'uniqueidentifier',0),
(N'WorkOrderAttempts',N'WorkOrderId',N'uniqueidentifier',0),
(N'WorkOrderAttempts',N'AttemptNumber',N'int',0),
(N'WorkOrderAttempts',N'PerformedByUserId',N'uniqueidentifier',0),
(N'WorkOrderAttempts',N'CompletedAtUtc',N'datetimeoffset',0),
(N'WorkOrderAttempts',N'ResultStatus',N'nvarchar(20)',0),
(N'WorkOrderAttempts',N'Notes',N'nvarchar(4000)',0),
(N'WorkOrderAttempts',N'Cost',N'decimal(18,2)',1),
(N'WorkOrderAttempts',N'CurrencyCode',N'char(3)',1),
(N'WorkOrderAttempts',N'ReviewDecision',N'nvarchar(20)',0),
(N'WorkOrderAttempts',N'ReviewedByUserId',N'uniqueidentifier',1),
(N'WorkOrderAttempts',N'ReviewedAtUtc',N'datetimeoffset',1),
(N'WorkOrderAttempts',N'ReviewReason',N'nvarchar(2000)',1),
(N'WorkOrderAttempts',N'RowVersion',N'rowversion',0),
(N'CalibrationMeasurements',N'Id',N'uniqueidentifier',0),
(N'CalibrationMeasurements',N'WorkOrderAttemptId',N'uniqueidentifier',0),
(N'CalibrationMeasurements',N'ParameterCode',N'nvarchar(50)',0),
(N'CalibrationMeasurements',N'ParameterName',N'nvarchar(200)',0),
(N'CalibrationMeasurements',N'Unit',N'nvarchar(30)',0),
(N'CalibrationMeasurements',N'MeasuredValue',N'decimal(18,6)',0),
(N'CalibrationMeasurements',N'MinimumAllowed',N'decimal(18,6)',1),
(N'CalibrationMeasurements',N'MaximumAllowed',N'decimal(18,6)',1),
(N'CalibrationMeasurements',N'Passed',N'bit',0),
(N'OccurrenceReminders',N'Id',N'uniqueidentifier',0),
(N'OccurrenceReminders',N'OccurrenceId',N'uniqueidentifier',0),
(N'OccurrenceReminders',N'ReminderKind',N'nvarchar(20)',0),
(N'OccurrenceReminders',N'EventId',N'uniqueidentifier',0),
(N'OccurrenceReminders',N'TriggeredAtUtc',N'datetimeoffset',0),
(N'WorkOrderActivities',N'Id',N'uniqueidentifier',0),
(N'WorkOrderActivities',N'WorkOrderId',N'uniqueidentifier',0),
(N'WorkOrderActivities',N'Action',N'nvarchar(50)',0),
(N'WorkOrderActivities',N'FromStatus',N'nvarchar(20)',1),
(N'WorkOrderActivities',N'ToStatus',N'nvarchar(20)',1),
(N'WorkOrderActivities',N'FromTechnicianId',N'uniqueidentifier',1),
(N'WorkOrderActivities',N'ToTechnicianId',N'uniqueidentifier',1),
(N'WorkOrderActivities',N'ActorUserId',N'uniqueidentifier',0),
(N'WorkOrderActivities',N'Reason',N'nvarchar(2000)',1),
(N'WorkOrderActivities',N'OccurredAtUtc',N'datetimeoffset',0),
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
(N'OutboxMessages',N'LastError',N'nvarchar(2000)',1);

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
(N'MaintenanceSchedules',N'Id'),
(N'ScheduleOccurrences',N'Id'),
(N'WorkOrders',N'Id'),
(N'WorkOrderAttempts',N'Id'),
(N'CalibrationMeasurements',N'Id'),
(N'OccurrenceReminders',N'Id'),
(N'WorkOrderActivities',N'Id'),
(N'AuditEntries',N'Id'),
(N'IdempotencyRequests',N'OperationId'),
(N'OutboxMessages',N'EventId');

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
(N'ScheduleOccurrences',N'ScheduleId',N'MaintenanceSchedules',N'Id',1),
(N'WorkOrders',N'OccurrenceId',N'ScheduleOccurrences',N'Id',1),
(N'WorkOrderAttempts',N'WorkOrderId',N'WorkOrders',N'Id',1),
(N'CalibrationMeasurements',N'WorkOrderAttemptId',N'WorkOrderAttempts',N'Id',1),
(N'OccurrenceReminders',N'OccurrenceId',N'ScheduleOccurrences',N'Id',1),
(N'WorkOrderActivities',N'WorkOrderId',N'WorkOrders',N'Id',1);

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
