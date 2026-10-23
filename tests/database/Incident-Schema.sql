-- Generated from approved erd-model.json. Read-only schema assertions.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @expected TABLE (TableName sysname, ColumnName sysname, SqlType nvarchar(100), IsNullable bit);
INSERT INTO @expected VALUES
(N'SlaPolicies',N'Id',N'uniqueidentifier',0),
(N'SlaPolicies',N'Priority',N'nvarchar(20)',0),
(N'SlaPolicies',N'Name',N'nvarchar(200)',0),
(N'SlaPolicies',N'IsEnabled',N'bit',0),
(N'SlaPolicies',N'CreatedAtUtc',N'datetimeoffset',0),
(N'SlaPolicies',N'UpdatedAtUtc',N'datetimeoffset',0),
(N'SlaPolicies',N'CreatedByUserId',N'uniqueidentifier',1),
(N'SlaPolicies',N'UpdatedByUserId',N'uniqueidentifier',1),
(N'SlaPolicies',N'RowVersion',N'rowversion',0),
(N'SlaPolicyVersions',N'SlaPolicyId',N'uniqueidentifier',0),
(N'SlaPolicyVersions',N'Version',N'int',0),
(N'SlaPolicyVersions',N'ResponseDurationMinutes',N'int',0),
(N'SlaPolicyVersions',N'ResolutionDurationMinutes',N'int',0),
(N'SlaPolicyVersions',N'EffectiveFromUtc',N'datetimeoffset',0),
(N'SlaPolicyVersions',N'EffectiveToUtc',N'datetimeoffset',1),
(N'SlaPolicyVersions',N'CreatedByUserId',N'uniqueidentifier',0),
(N'SlaPolicyVersions',N'CreatedAtUtc',N'datetimeoffset',0),
(N'Incidents',N'Id',N'uniqueidentifier',0),
(N'Incidents',N'Code',N'nvarchar(50)',0),
(N'Incidents',N'EquipmentId',N'uniqueidentifier',0),
(N'Incidents',N'EquipmentCodeSnapshot',N'nvarchar(50)',0),
(N'Incidents',N'EquipmentNameSnapshot',N'nvarchar(200)',0),
(N'Incidents',N'DepartmentIdSnapshot',N'uniqueidentifier',0),
(N'Incidents',N'DepartmentNameSnapshot',N'nvarchar(200)',0),
(N'Incidents',N'Title',N'nvarchar(200)',0),
(N'Incidents',N'Description',N'nvarchar(4000)',0),
(N'Incidents',N'ReportedByUserId',N'uniqueidentifier',0),
(N'Incidents',N'AssignedTechnicianId',N'uniqueidentifier',1),
(N'Incidents',N'RelatedIncidentId',N'uniqueidentifier',1),
(N'Incidents',N'Priority',N'nvarchar(20)',0),
(N'Incidents',N'Status',N'nvarchar(20)',0),
(N'Incidents',N'SlaPolicyId',N'uniqueidentifier',0),
(N'Incidents',N'SlaPolicyVersion',N'int',0),
(N'Incidents',N'SlaPrioritySnapshot',N'nvarchar(20)',0),
(N'Incidents',N'ResponseDurationMinutesSnapshot',N'int',0),
(N'Incidents',N'ResolutionDurationMinutesSnapshot',N'int',0),
(N'Incidents',N'CreatedAtUtc',N'datetimeoffset',0),
(N'Incidents',N'ResponseDeadlineAtUtc',N'datetimeoffset',0),
(N'Incidents',N'ResolutionDeadlineAtUtc',N'datetimeoffset',0),
(N'Incidents',N'FirstRespondedAtUtc',N'datetimeoffset',1),
(N'Incidents',N'ClosedAtUtc',N'datetimeoffset',1),
(N'Incidents',N'ResponseOutcome',N'nvarchar(20)',0),
(N'Incidents',N'ResolutionOutcome',N'nvarchar(20)',0),
(N'Incidents',N'SlaFinalizedAtUtc',N'datetimeoffset',1),
(N'Incidents',N'EquipmentUseRegistrationId',N'uniqueidentifier',0),
(N'Incidents',N'AggregateVersion',N'bigint',0),
(N'Incidents',N'UpdatedAtUtc',N'datetimeoffset',0),
(N'Incidents',N'CreatedByUserId',N'uniqueidentifier',0),
(N'Incidents',N'UpdatedByUserId',N'uniqueidentifier',0),
(N'Incidents',N'RowVersion',N'rowversion',0),
(N'ResolutionAttempts',N'Id',N'uniqueidentifier',0),
(N'ResolutionAttempts',N'IncidentId',N'uniqueidentifier',0),
(N'ResolutionAttempts',N'AttemptNumber',N'int',0),
(N'ResolutionAttempts',N'PerformedByUserId',N'uniqueidentifier',0),
(N'ResolutionAttempts',N'ResolutionNotes',N'nvarchar(4000)',0),
(N'ResolutionAttempts',N'ResultSummary',N'nvarchar(2000)',0),
(N'ResolutionAttempts',N'CompletedAtUtc',N'datetimeoffset',0),
(N'ResolutionAttempts',N'ReviewDecision',N'nvarchar(20)',0),
(N'ResolutionAttempts',N'ReviewedByUserId',N'uniqueidentifier',1),
(N'ResolutionAttempts',N'ReviewedAtUtc',N'datetimeoffset',1),
(N'ResolutionAttempts',N'ReviewReason',N'nvarchar(2000)',1),
(N'ResolutionAttempts',N'RowVersion',N'rowversion',0),
(N'IncidentActivities',N'Id',N'uniqueidentifier',0),
(N'IncidentActivities',N'IncidentId',N'uniqueidentifier',0),
(N'IncidentActivities',N'Action',N'nvarchar(50)',0),
(N'IncidentActivities',N'FromStatus',N'nvarchar(20)',1),
(N'IncidentActivities',N'ToStatus',N'nvarchar(20)',1),
(N'IncidentActivities',N'FromTechnicianId',N'uniqueidentifier',1),
(N'IncidentActivities',N'ToTechnicianId',N'uniqueidentifier',1),
(N'IncidentActivities',N'ActorUserId',N'uniqueidentifier',0),
(N'IncidentActivities',N'Reason',N'nvarchar(2000)',1),
(N'IncidentActivities',N'OccurredAtUtc',N'datetimeoffset',0),
(N'SlaDeadlineAlerts',N'Id',N'uniqueidentifier',0),
(N'SlaDeadlineAlerts',N'IncidentId',N'uniqueidentifier',0),
(N'SlaDeadlineAlerts',N'AlertKind',N'nvarchar(20)',0),
(N'SlaDeadlineAlerts',N'DeadlineAtUtc',N'datetimeoffset',0),
(N'SlaDeadlineAlerts',N'DetectedAtUtc',N'datetimeoffset',0),
(N'SlaDeadlineAlerts',N'EventId',N'uniqueidentifier',0),
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
(N'SlaPolicies',N'Id'),
(N'SlaPolicyVersions',N'SlaPolicyId,Version'),
(N'Incidents',N'Id'),
(N'ResolutionAttempts',N'Id'),
(N'IncidentActivities',N'Id'),
(N'SlaDeadlineAlerts',N'Id'),
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
(N'SlaPolicyVersions',N'SlaPolicyId',N'SlaPolicies',N'Id',1),
(N'Incidents',N'SlaPolicyId',N'SlaPolicyVersions',N'SlaPolicyId',1),
(N'Incidents',N'SlaPolicyVersion',N'SlaPolicyVersions',N'Version',2),
(N'Incidents',N'RelatedIncidentId',N'Incidents',N'Id',1),
(N'ResolutionAttempts',N'IncidentId',N'Incidents',N'Id',1),
(N'IncidentActivities',N'IncidentId',N'Incidents',N'Id',1),
(N'SlaDeadlineAlerts',N'IncidentId',N'Incidents',N'Id',1);

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
