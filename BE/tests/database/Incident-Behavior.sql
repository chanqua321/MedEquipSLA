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
INSERT INTO [dbo].[SlaPolicies] ([Id],[Priority],[Name],[IsEnabled],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'97bfe4fa-95bd-4581-9975-d3593ec48372',N'Critical',N'test-3c516d007fd94393a18fe10233fbef33',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
INSERT INTO [dbo].[SlaPolicyVersions] ([SlaPolicyId],[Version],[ResponseDurationMinutes],[ResolutionDurationMinutes],[EffectiveFromUtc],[CreatedByUserId],[CreatedAtUtc]) VALUES (N'97bfe4fa-95bd-4581-9975-d3593ec48372',1,30,60,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),N'd054f952-6570-497d-9d83-fc3d8fdb0630',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
INSERT INTO [dbo].[Incidents] ([Id],[Code],[EquipmentId],[EquipmentCodeSnapshot],[EquipmentNameSnapshot],[DepartmentIdSnapshot],[DepartmentNameSnapshot],[Title],[Description],[ReportedByUserId],[Priority],[Status],[SlaPolicyId],[SlaPolicyVersion],[SlaPrioritySnapshot],[ResponseDurationMinutesSnapshot],[ResolutionDurationMinutesSnapshot],[CreatedAtUtc],[ResponseDeadlineAtUtc],[ResolutionDeadlineAtUtc],[ResponseOutcome],[ResolutionOutcome],[EquipmentUseRegistrationId],[AggregateVersion],[UpdatedAtUtc],[CreatedByUserId],[UpdatedByUserId]) VALUES (N'7e01fb58-fec4-41ef-8faa-6bafe6ee1368',N'test-5ceecccb52714a82b01a00a564f5ea98',N'f4511ce5-1a2a-4982-8f19-a21dba20a9f2',N'test-44d23f1f24cb49f990fadb13e513a11a',N'test-87d884a2628f4af6b05eb598cd33c87d',N'deaa2ef4-4edc-460b-87fd-d819e70fdc98',N'test-54b0ae838d4c4c92be058080e9164c03',N'test-cf6c821391d84d3ea370ade052d0bed9',N'test-d5c96a11d6834e7094a1f8f2cb2dede4',N'b04b2eb1-5095-41a2-b539-d9820971608c',N'Critical',N'Open',N'97bfe4fa-95bd-4581-9975-d3593ec48372',1,N'Critical',30,60,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),'2026-10-23T00:30:00Z','2026-10-23T01:00:00Z',N'Pending',N'Pending',N'f84b375e-b1a5-43bf-bc0e-8b7f1e6bae55',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),N'2e3a1409-68c7-48e6-976c-534a2c961473',N'ca92b64c-b19e-4687-a235-b49095a02570');
BEGIN TRY
INSERT INTO [dbo].[Incidents] ([Id],[Code],[EquipmentId],[EquipmentCodeSnapshot],[EquipmentNameSnapshot],[DepartmentIdSnapshot],[DepartmentNameSnapshot],[Title],[Description],[ReportedByUserId],[Priority],[Status],[SlaPolicyId],[SlaPolicyVersion],[SlaPrioritySnapshot],[ResponseDurationMinutesSnapshot],[ResolutionDurationMinutesSnapshot],[CreatedAtUtc],[ResponseDeadlineAtUtc],[ResolutionDeadlineAtUtc],[ResponseOutcome],[ResolutionOutcome],[EquipmentUseRegistrationId],[AggregateVersion],[UpdatedAtUtc],[CreatedByUserId],[UpdatedByUserId]) VALUES (N'653d4971-ef61-4fae-be80-604955f71526',N'test-3d69f18c72d4451992581925d7812255',N'91c16555-da58-4f7f-a3a0-bb906af34b7a',N'test-5ff60027b3c54e9da1bcdf8764fbe672',N'test-d8b0afd4d3d24e78b2a128adfd17df63',N'2e8d6992-2ea6-4ee0-b7b9-1ecaf4877247',N'test-8e7efc9e82c142b18f9a881869124d1b',N'test-1f65a2584ac942f7a4e8a43f179b5f7f',N'test-768779b6628c429dbfd6920e371f81d6',N'fbbe6cbd-da2b-4686-bb52-0fcd2c2df190',N'Critical',N'Open',N'97bfe4fa-95bd-4581-9975-d3593ec48372',2,N'Critical',30,60,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),'2026-10-23T00:30:00Z','2026-10-23T01:00:00Z',N'Pending',N'Pending',N'9422e9a9-2470-43e7-9dd6-18363911da84',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),N'3160d30f-f6cf-4211-a0b4-188543acf2ec',N'75bd886c-f85f-4de9-bce8-e9851d157924');
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (547) THROW;
END CATCH;
BEGIN TRY
INSERT INTO [dbo].[Incidents] ([Id],[Code],[EquipmentId],[EquipmentCodeSnapshot],[EquipmentNameSnapshot],[DepartmentIdSnapshot],[DepartmentNameSnapshot],[Title],[Description],[ReportedByUserId],[Priority],[Status],[SlaPolicyId],[SlaPolicyVersion],[SlaPrioritySnapshot],[ResponseDurationMinutesSnapshot],[ResolutionDurationMinutesSnapshot],[CreatedAtUtc],[ResponseDeadlineAtUtc],[ResolutionDeadlineAtUtc],[ResponseOutcome],[ResolutionOutcome],[EquipmentUseRegistrationId],[AggregateVersion],[UpdatedAtUtc],[CreatedByUserId],[UpdatedByUserId]) VALUES (N'9e856348-723a-4feb-a86c-674a6103647b',N'test-861a67b7ea9e4c598202061f9b391e3c',N'a577abf3-fbaf-410d-960c-2725ff4134ea',N'test-1b0c9377577e472ca022657e6209382b',N'test-58a6d07a759547d89b3a3f044ecbdaa6',N'32913545-6eec-41b8-95da-bbdf3100984f',N'test-a7ab11befb534ddc9ae1e6e68c62dc5a',N'test-15e3a1c385004e3ea273ecdd3f0f4758',N'test-07eaa2d7cc9a4b5db65ff9c1a18420a8',N'804223ee-f289-471a-add2-6159c6007781',N'Critical',N'Open',N'97bfe4fa-95bd-4581-9975-d3593ec48372',1,N'Critical',30,60,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),'2026-10-23T00:30:00Z','2026-10-24T01:00:00Z',N'Pending',N'Pending',N'abab1fa1-917f-4c5c-aa50-97fc8f295ac8',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),N'a9d8fde8-8492-428c-ba17-3274548604ee',N'b2466a9f-ac77-4447-a3cb-f0a39234f822');
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (547) THROW;
END CATCH;
INSERT INTO [dbo].[ResolutionAttempts] ([Id],[IncidentId],[AttemptNumber],[PerformedByUserId],[ResolutionNotes],[ResultSummary],[CompletedAtUtc],[ReviewDecision],[ReviewedByUserId],[ReviewedAtUtc]) VALUES (N'9e29e096-1623-450f-8dc0-1bfe724a46d3',N'7e01fb58-fec4-41ef-8faa-6bafe6ee1368',1,N'a350eb42-a95b-4e1d-9af2-96b40cf91098',N'test-cd0e27170b02407aba1ba171689f98e1',N'test-32c391c244a341e7a54f3fade29d44ca',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),N'Accepted',N'fda0c06e-8e5f-47b1-bbf2-daf4b8699079','2026-10-23T02:00:00Z');
BEGIN TRY
INSERT INTO [dbo].[ResolutionAttempts] ([Id],[IncidentId],[AttemptNumber],[PerformedByUserId],[ResolutionNotes],[ResultSummary],[CompletedAtUtc],[ReviewDecision],[ReviewedByUserId],[ReviewedAtUtc]) VALUES (N'b1aa87b6-7cf2-49bd-b921-1483640afec8',N'7e01fb58-fec4-41ef-8faa-6bafe6ee1368',2,N'93d8f3a1-3393-4914-9b9b-657fab6ef266',N'test-6b591999b8c8485eb72c4c5aa3eb19a6',N'test-cf3239c4ed7d4846afeed718e9cb7071',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),N'Accepted',N'fda0c06e-8e5f-47b1-bbf2-daf4b8699079','2026-10-23T02:00:00Z');
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
ROLLBACK TRANSACTION;
PRINT 'PASS: Incident persistence constraints; fixtures rolled back';
END TRY
BEGIN CATCH
IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
