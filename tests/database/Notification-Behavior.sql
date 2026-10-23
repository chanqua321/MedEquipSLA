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
INSERT INTO [dbo].[Notifications] ([Id],[RecipientUserId],[SourceEventId],[Kind],[Title],[Body],[ResourceType],[ResourceId],[CreatedAtUtc]) VALUES (N'8c64b18f-a92c-4568-a02f-5bf896e4419c',N'216ee090-0cae-4529-9f93-cd8975b81784',N'e40dab4f-bc4b-4753-adab-99328c6eb172',N'IncidentOpened',N'test-68ce0aa94e65472cb6048039a1a95a77',N'test-ce68ee2918b84d4ea6e88cf288230bfc',N'test-cc665215aeaf4f1890d9ff820',N'9c0f519b-3009-45e2-bb16-5692c05fb23e',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
BEGIN TRY
INSERT INTO [dbo].[Notifications] ([Id],[RecipientUserId],[SourceEventId],[Kind],[Title],[Body],[ResourceType],[ResourceId],[CreatedAtUtc]) VALUES (N'58ee95c2-42f1-4f2b-97a4-d6af1d3b2bab',N'216ee090-0cae-4529-9f93-cd8975b81784',N'e40dab4f-bc4b-4753-adab-99328c6eb172',N'IncidentOpened',N'test-31a44788aba94edabc73e1f298ba95ac',N'test-b9fdb51945874134b35382e0133bf7d4',N'test-3dfef5d18d65428f8b7947993',N'df7aa443-c97f-46e4-8c58-20bb6bd91c42',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
INSERT INTO [dbo].[EmailDeliveries] ([Id],[NotificationId],[ToAddressSnapshot],[Subject],[Body],[MessageId],[Status],[AttemptCount],[CreatedAtUtc]) VALUES (N'6971e396-f519-41b8-b750-80a8032eb233',N'8c64b18f-a92c-4568-a02f-5bf896e4419c',N'test-6b4a2289f6f545328f128e54dafa9d58',N'test-58e388f167744c9db3c8ba67c1d7587e',N'test-f0e57d22039e4fa98a172b462599f7d5',N'test-d1c5d7aa8d514ade93a94448b4644151',N'Pending',0,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
BEGIN TRY
INSERT INTO [dbo].[EmailDeliveries] ([Id],[NotificationId],[ToAddressSnapshot],[Subject],[Body],[MessageId],[Status],[AttemptCount],[CreatedAtUtc]) VALUES (N'03fa96bf-231a-4ed2-ba46-5e65f79efbd4',N'8c64b18f-a92c-4568-a02f-5bf896e4419c',N'test-001e2b98fdf5404d914b3bd28c962a50',N'test-a93e1fd57d654bbeb122f62aa42e6928',N'test-0920158e936d4ea3aca99f25002657dc',N'test-8b24863b8b264fd0b6a46f32128022ae',N'Pending',0,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
ROLLBACK TRANSACTION;
PRINT 'PASS: Notification persistence constraints; fixtures rolled back';
END TRY
BEGIN CATCH
IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
