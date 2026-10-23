-- Generated from approved erd-model.json. Read-only schema assertions.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @expected TABLE (TableName sysname, ColumnName sysname, SqlType nvarchar(100), IsNullable bit);
INSERT INTO @expected VALUES
(N'AspNetUsers',N'Id',N'uniqueidentifier',0),
(N'AspNetUsers',N'UserName',N'nvarchar(256)',1),
(N'AspNetUsers',N'NormalizedUserName',N'nvarchar(256)',1),
(N'AspNetUsers',N'Email',N'nvarchar(256)',1),
(N'AspNetUsers',N'NormalizedEmail',N'nvarchar(256)',1),
(N'AspNetUsers',N'EmailConfirmed',N'bit',0),
(N'AspNetUsers',N'PasswordHash',N'nvarchar(max)',1),
(N'AspNetUsers',N'SecurityStamp',N'nvarchar(max)',1),
(N'AspNetUsers',N'ConcurrencyStamp',N'nvarchar(max)',1),
(N'AspNetUsers',N'PhoneNumber',N'nvarchar(max)',1),
(N'AspNetUsers',N'PhoneNumberConfirmed',N'bit',0),
(N'AspNetUsers',N'TwoFactorEnabled',N'bit',0),
(N'AspNetUsers',N'LockoutEnd',N'datetimeoffset',1),
(N'AspNetUsers',N'LockoutEnabled',N'bit',0),
(N'AspNetUsers',N'AccessFailedCount',N'int',0),
(N'AspNetUsers',N'FullName',N'nvarchar(200)',0),
(N'AspNetUsers',N'DepartmentId',N'uniqueidentifier',1),
(N'AspNetUsers',N'DepartmentNameSnapshot',N'nvarchar(200)',1),
(N'AspNetUsers',N'IsActive',N'bit',0),
(N'AspNetUsers',N'AuthorizationVersion',N'bigint',0),
(N'AspNetUsers',N'CreatedAtUtc',N'datetimeoffset',0),
(N'AspNetUsers',N'UpdatedAtUtc',N'datetimeoffset',0),
(N'AspNetUsers',N'CreatedByUserId',N'uniqueidentifier',1),
(N'AspNetUsers',N'UpdatedByUserId',N'uniqueidentifier',1),
(N'AspNetUsers',N'RowVersion',N'rowversion',0),
(N'AspNetRoles',N'Id',N'uniqueidentifier',0),
(N'AspNetRoles',N'Name',N'nvarchar(256)',1),
(N'AspNetRoles',N'NormalizedName',N'nvarchar(256)',1),
(N'AspNetRoles',N'ConcurrencyStamp',N'nvarchar(max)',1),
(N'AspNetUserRoles',N'UserId',N'uniqueidentifier',0),
(N'AspNetUserRoles',N'RoleId',N'uniqueidentifier',0),
(N'AspNetUserClaims',N'Id',N'int',0),
(N'AspNetUserClaims',N'UserId',N'uniqueidentifier',0),
(N'AspNetUserClaims',N'ClaimType',N'nvarchar(max)',1),
(N'AspNetUserClaims',N'ClaimValue',N'nvarchar(max)',1),
(N'AspNetRoleClaims',N'Id',N'int',0),
(N'AspNetRoleClaims',N'RoleId',N'uniqueidentifier',0),
(N'AspNetRoleClaims',N'ClaimType',N'nvarchar(max)',1),
(N'AspNetRoleClaims',N'ClaimValue',N'nvarchar(max)',1),
(N'AspNetUserLogins',N'LoginProvider',N'nvarchar(128)',0),
(N'AspNetUserLogins',N'ProviderKey',N'nvarchar(128)',0),
(N'AspNetUserLogins',N'ProviderDisplayName',N'nvarchar(max)',1),
(N'AspNetUserLogins',N'UserId',N'uniqueidentifier',0),
(N'AspNetUserTokens',N'UserId',N'uniqueidentifier',0),
(N'AspNetUserTokens',N'LoginProvider',N'nvarchar(128)',0),
(N'AspNetUserTokens',N'Name',N'nvarchar(128)',0),
(N'AspNetUserTokens',N'Value',N'nvarchar(max)',1),
(N'RefreshTokens',N'Id',N'uniqueidentifier',0),
(N'RefreshTokens',N'UserId',N'uniqueidentifier',0),
(N'RefreshTokens',N'TokenHash',N'binary(32)',0),
(N'RefreshTokens',N'FamilyId',N'uniqueidentifier',0),
(N'RefreshTokens',N'IssuedAtUtc',N'datetimeoffset',0),
(N'RefreshTokens',N'ExpiresAtUtc',N'datetimeoffset',0),
(N'RefreshTokens',N'RevokedAtUtc',N'datetimeoffset',1),
(N'RefreshTokens',N'ReplacedByTokenId',N'uniqueidentifier',1),
(N'RefreshTokens',N'RevocationReason',N'nvarchar(200)',1),
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
(N'IdempotencyRequests',N'RowVersion',N'rowversion',0);

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
(N'AspNetUsers',N'Id'),
(N'AspNetRoles',N'Id'),
(N'AspNetUserRoles',N'UserId,RoleId'),
(N'AspNetUserClaims',N'Id'),
(N'AspNetRoleClaims',N'Id'),
(N'AspNetUserLogins',N'LoginProvider,ProviderKey'),
(N'AspNetUserTokens',N'UserId,LoginProvider,Name'),
(N'RefreshTokens',N'Id'),
(N'AuditEntries',N'Id'),
(N'IdempotencyRequests',N'OperationId');

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
(N'AspNetUserRoles',N'UserId',N'AspNetUsers',N'Id',1),
(N'AspNetUserRoles',N'RoleId',N'AspNetRoles',N'Id',1),
(N'AspNetUserClaims',N'UserId',N'AspNetUsers',N'Id',1),
(N'AspNetRoleClaims',N'RoleId',N'AspNetRoles',N'Id',1),
(N'AspNetUserLogins',N'UserId',N'AspNetUsers',N'Id',1),
(N'AspNetUserTokens',N'UserId',N'AspNetUsers',N'Id',1),
(N'RefreshTokens',N'UserId',N'AspNetUsers',N'Id',1),
(N'RefreshTokens',N'ReplacedByTokenId',N'RefreshTokens',N'Id',1);

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
