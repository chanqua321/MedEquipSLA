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
INSERT INTO [dbo].[AspNetUsers] ([Id],[NormalizedEmail],[EmailConfirmed],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount],[FullName],[IsActive],[AuthorizationVersion],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'216ee090-0cae-4529-9f93-cd8975b81784',N'db-check@example.invalid',1,1,1,1,1,N'test-b2de0b905ed949928ced3434d1cf01a1',1,1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
BEGIN TRY
INSERT INTO [dbo].[AspNetUsers] ([Id],[NormalizedEmail],[EmailConfirmed],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount],[FullName],[IsActive],[AuthorizationVersion],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'fb80b764-8d27-4888-89a2-da607f9a275b',N'db-check@example.invalid',1,1,1,1,1,N'test-533cd605e7ae49078b3fa10d6e3dd2cd',1,1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
INSERT INTO [dbo].[RefreshTokens] ([Id],[UserId],[TokenHash],[FamilyId],[IssuedAtUtc],[ExpiresAtUtc]) VALUES (N'c56dc084-5e6d-428f-98d7-2ab869c9fcb1',N'216ee090-0cae-4529-9f93-cd8975b81784',0x1111111111111111111111111111111111111111111111111111111111111111,N'9866a137-464e-4472-ad43-2523cf1df169','2026-10-23T00:00:00Z','2026-10-24T00:00:00Z');
BEGIN TRY
INSERT INTO [dbo].[RefreshTokens] ([Id],[UserId],[TokenHash],[FamilyId],[IssuedAtUtc],[ExpiresAtUtc]) VALUES (N'94c13ba0-12b3-49cd-a89d-cab044e0f011',N'216ee090-0cae-4529-9f93-cd8975b81784',0x1111111111111111111111111111111111111111111111111111111111111111,N'c93ad2b4-a2a7-4003-b639-2625ce5b87d0','2026-10-23T00:00:00Z','2026-10-24T00:00:00Z');
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
BEGIN TRY
DELETE FROM AspNetUsers WHERE Id=N'216ee090-0cae-4529-9f93-cd8975b81784';
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (547) THROW;
END CATCH;
ROLLBACK TRANSACTION;
PRINT 'PASS: Identity persistence constraints; fixtures rolled back';
END TRY
BEGIN CATCH
IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
