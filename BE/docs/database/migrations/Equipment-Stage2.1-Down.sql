BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM dbo.EquipmentRetirementIntents)
   OR EXISTS (SELECT 1 FROM dbo.RetirementSourceReceipts)
   OR EXISTS (SELECT 1 FROM dbo.EquipmentUseRegistrations WHERE
       RequestHash IS NOT NULL OR RequestPayloadJson IS NOT NULL OR FencingToken <> 0
       OR CancelIntentId IS NOT NULL OR EquipmentCodeSnapshot IS NOT NULL
       OR EquipmentNameSnapshot IS NOT NULL OR DepartmentIdSnapshot IS NOT NULL
       OR DepartmentNameSnapshot IS NOT NULL OR (ResourceType=N'WorkOrder' AND State=N'Aborted'))
    THROW 51021, 'Stage 2.1 rollback refused: durable Equipment protocol data exists.', 1;

DROP TABLE [RetirementSourceReceipts];

DROP TABLE [EquipmentRetirementIntents];

ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT [CK_EquipmentUseRegistrations_Fence];

ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT [CK_EquipmentUseRegistrations_Payload];

ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT [CK_EquipmentUseRegistrations_ProtocolPair];

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EquipmentUseRegistrations]') AND [c].[name] = N'CancelIntentId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [EquipmentUseRegistrations] DROP COLUMN [CancelIntentId];

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EquipmentUseRegistrations]') AND [c].[name] = N'DepartmentIdSnapshot');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [EquipmentUseRegistrations] DROP COLUMN [DepartmentIdSnapshot];

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EquipmentUseRegistrations]') AND [c].[name] = N'DepartmentNameSnapshot');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [EquipmentUseRegistrations] DROP COLUMN [DepartmentNameSnapshot];

DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EquipmentUseRegistrations]') AND [c].[name] = N'EquipmentCodeSnapshot');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT ' + @var3 + ';');
ALTER TABLE [EquipmentUseRegistrations] DROP COLUMN [EquipmentCodeSnapshot];

DECLARE @var4 nvarchar(max);
SELECT @var4 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EquipmentUseRegistrations]') AND [c].[name] = N'EquipmentNameSnapshot');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT ' + @var4 + ';');
ALTER TABLE [EquipmentUseRegistrations] DROP COLUMN [EquipmentNameSnapshot];

DECLARE @var5 nvarchar(max);
SELECT @var5 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EquipmentUseRegistrations]') AND [c].[name] = N'FencingToken');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT ' + @var5 + ';');
ALTER TABLE [EquipmentUseRegistrations] DROP COLUMN [FencingToken];

DECLARE @var6 nvarchar(max);
SELECT @var6 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EquipmentUseRegistrations]') AND [c].[name] = N'RequestHash');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT ' + @var6 + ';');
ALTER TABLE [EquipmentUseRegistrations] DROP COLUMN [RequestHash];

DECLARE @var7 nvarchar(max);
SELECT @var7 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EquipmentUseRegistrations]') AND [c].[name] = N'RequestPayloadJson');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [EquipmentUseRegistrations] DROP CONSTRAINT ' + @var7 + ';');
ALTER TABLE [EquipmentUseRegistrations] DROP COLUMN [RequestPayloadJson];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261106063402_AddEquipmentRetirementFoundation';

COMMIT;
GO
