-- Integration assertions. All fixtures are rolled back, including on failure.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT OFF;
BEGIN TRANSACTION;
BEGIN TRY
INSERT INTO [dbo].[Departments] ([Id],[Code],[Name],[IsActive],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'59f4a11b-e241-427f-85d8-fc248839288e',N'test-d8121bd720604631b1420e4e7352c61d',N'test-a96b862b2c964fa986ff59fbedf0e919',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
INSERT INTO [dbo].[EquipmentCategories] ([Id],[Code],[Name],[IsActive],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'86c05c1d-98ce-4165-8e35-f3cbe0011780',N'test-b54aa940b23e4572bac915fc230fe785',N'test-0a2314b3b9a143aa9765e5fd8ccf7e0d',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
INSERT INTO [dbo].[EquipmentAssets] ([Id],[AssetCode],[Name],[CategoryId],[DepartmentId],[IsRetired],[OperationalStatus],[AggregateVersion],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'f78c717b-cf29-4b94-8a65-84bbadb3cd63',N'test-0365e7ef4dfd48e1967d07202fa71367',N'test-1f6793bcbdbf44568d5a37bf57572e19',N'86c05c1d-98ce-4165-8e35-f3cbe0011780',N'59f4a11b-e241-427f-85d8-fc248839288e',0,N'Operational',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
BEGIN TRY
INSERT INTO [dbo].[EquipmentAssets] ([Id],[AssetCode],[Name],[CategoryId],[DepartmentId],[IsRetired],[OperationalStatus],[AggregateVersion],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'137ac112-07a4-4d63-9357-ba3a41bd1651',N'test-7f8c03949a95459089b69be41411e75b',N'test-055a029c5f8648d8be35c5c7f47fd83a',N'86c05c1d-98ce-4165-8e35-f3cbe0011780',N'0e0fc618-9356-484a-88fe-eca0a399adaa',0,N'Operational',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (547) THROW;
END CATCH;
BEGIN TRY
UPDATE EquipmentAssets SET IsRetired=1 WHERE Id=N'f78c717b-cf29-4b94-8a65-84bbadb3cd63';
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (547) THROW;
END CATCH;
DECLARE @oldVersion binary(8)=(SELECT RowVersion FROM EquipmentAssets WHERE Id=N'f78c717b-cf29-4b94-8a65-84bbadb3cd63');
UPDATE EquipmentAssets SET Name=N'concurrency check' WHERE Id=N'f78c717b-cf29-4b94-8a65-84bbadb3cd63' AND RowVersion=@oldVersion;
IF @@ROWCOUNT<>1 THROW 51010,'First optimistic write should succeed',1;
UPDATE EquipmentAssets SET Name=N'stale write' WHERE Id=N'f78c717b-cf29-4b94-8a65-84bbadb3cd63' AND RowVersion=@oldVersion;
IF @@ROWCOUNT<>0 THROW 51011,'Stale optimistic write was accepted',1;
INSERT INTO [dbo].[OutboxMessages] ([EventId],[EventType],[SchemaVersion],[AggregateType],[AggregateId],[AggregateVersion],[OccurredAtUtc],[CorrelationId],[PayloadJson],[Status],[AttemptCount]) VALUES (N'bb7dd232-36d2-4947-b177-6c2f36fb07b4',N'test-43426423e7d34ab7b5dfa3f112ce33d7',1,N'test-3f566dee3fed44fcb16b176b39bf9443',N'e27e47c8-fc11-4c54-9a8d-c7c57f12c1c1',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),N'test-c6077b71016f4620b3317f90d680d2a5',N'{}',N'Pending',0);
BEGIN TRY
INSERT INTO [dbo].[OutboxMessages] ([EventId],[EventType],[SchemaVersion],[AggregateType],[AggregateId],[AggregateVersion],[OccurredAtUtc],[CorrelationId],[PayloadJson],[Status],[AttemptCount]) VALUES (N'afd29ff9-6535-4552-b0c7-0ee7f001c9c9',N'test-0c155b66e68a42cea49460e439201ad8',1,N'test-1c07e96f77a545668c78777a93fba340',N'2258d8cc-a64b-4910-8536-f4149a5b3348',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),N'test-0a6cfa05579749aeb75e9429af0103d9',N'invalid-json',N'Pending',0);
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (547) THROW;
END CATCH;
INSERT INTO [dbo].[InboxMessages] ([ConsumerName],[EventId],[EventType],[SchemaVersion],[ProcessedAtUtc]) VALUES (N'database-check',N'bb7dd232-36d2-4947-b177-6c2f36fb07b4',N'test-85e1f593edad4aa789288bd99b8da7d1',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
BEGIN TRY
INSERT INTO [dbo].[InboxMessages] ([ConsumerName],[EventId],[EventType],[SchemaVersion],[ProcessedAtUtc]) VALUES (N'database-check',N'bb7dd232-36d2-4947-b177-6c2f36fb07b4',N'test-a5c445cdb9c549ba89b45e873c81b3c8',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
INSERT INTO [dbo].[IdempotencyRequests] ([OperationId],[ActorScope],[OperationName],[IdempotencyKey],[RequestHash],[State],[CreatedAtUtc],[ExpiresAtUtc]) VALUES (N'357a8ed1-995b-4e40-abff-b2b3a77cdd67',N'database-check',N'database-check',N'4c515ed6-4b62-4534-9e72-6532b1020b3b',0x1111111111111111111111111111111111111111111111111111111111111111,N'Pending',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),'2026-10-24T00:00:00Z');
BEGIN TRY
INSERT INTO [dbo].[IdempotencyRequests] ([OperationId],[ActorScope],[OperationName],[IdempotencyKey],[RequestHash],[State],[CreatedAtUtc],[ExpiresAtUtc]) VALUES (N'ebed5277-d8cd-4c6b-80f5-250cfb04384a',N'database-check',N'database-check',N'4c515ed6-4b62-4534-9e72-6532b1020b3b',0x1111111111111111111111111111111111111111111111111111111111111111,N'Pending',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),'2026-10-24T00:00:00Z');
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
ROLLBACK TRANSACTION;
IF EXISTS(SELECT 1 FROM EquipmentAssets WHERE Id=N'f78c717b-cf29-4b94-8a65-84bbadb3cd63') OR EXISTS(SELECT 1 FROM OutboxMessages WHERE EventId=N'bb7dd232-36d2-4947-b177-6c2f36fb07b4') THROW 51012,'Transaction rollback left data behind',1;
PRINT 'PASS: Equipment persistence constraints; fixtures rolled back';
END TRY
BEGIN CATCH
IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
