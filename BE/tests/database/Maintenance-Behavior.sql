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
INSERT INTO [dbo].[MaintenanceSchedules] ([Id],[Code],[EquipmentId],[EquipmentCodeSnapshot],[EquipmentNameSnapshot],[DepartmentIdSnapshot],[DepartmentNameSnapshot],[WorkType],[AnchorAtUtc],[IntervalDays],[NextOccurrenceAtUtc],[ReminderLeadMinutes],[CompletionWindowMinutes],[IsActive],[EquipmentUseRegistrationId],[AggregateVersion],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'7f0e8a20-c172-4bb1-bcfa-3bf258d00fef',N'test-068fcca0939a49dfb3f1c238c9aacf1c',N'21481eb6-8ccf-4c00-b077-3078be152028',N'test-e30c296c4d1246aabf4803918ec79e76',N'test-9c7303c7757d40c5ad1c4ec0ecf21359',N'1b22b3a3-365d-455d-bc35-9a5a12ce2fc5',N'test-b4bdafdd6a79498491975d2b1bc54225',N'Calibration',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),1,1,1,N'971bc71a-07ae-460d-98b7-90ed782a9261',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
INSERT INTO [dbo].[ScheduleOccurrences] ([Id],[ScheduleId],[ScheduledOccurrenceAtUtc],[DueAtUtc],[State],[CreatedAtUtc]) VALUES (N'c5c11c9f-0675-4d49-af89-33b71133ddc1',N'7f0e8a20-c172-4bb1-bcfa-3bf258d00fef','2026-10-23T00:00:00Z','2026-10-23T00:00:00Z',N'Planned',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
BEGIN TRY
INSERT INTO [dbo].[ScheduleOccurrences] ([Id],[ScheduleId],[ScheduledOccurrenceAtUtc],[DueAtUtc],[State],[CreatedAtUtc]) VALUES (N'3f6c9f41-877b-487c-b462-03ab7396aa59',N'7f0e8a20-c172-4bb1-bcfa-3bf258d00fef','2026-10-23T00:00:00Z','2026-10-23T00:00:00Z',N'Planned',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
INSERT INTO [dbo].[WorkOrders] ([Id],[Code],[OccurrenceId],[EquipmentId],[EquipmentCodeSnapshot],[EquipmentNameSnapshot],[DepartmentIdSnapshot],[DepartmentNameSnapshot],[WorkType],[Status],[ScheduledForUtc],[DueAtUtc],[IsBlocking],[EquipmentUseRegistrationId],[AggregateVersion],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'73e11ddd-8b20-4f5c-becf-64fd8fd2d310',N'test-e16314c1d87d401785dcf3f0385a73a8',N'c5c11c9f-0675-4d49-af89-33b71133ddc1',N'c333360f-393b-401c-a961-47736cab9b64',N'test-638d9b513d144698a19d7dc32b1ec8fa',N'test-ab25885611f74a7b9eba80dcd11e0abc',N'251b5e4d-e213-4341-b3ea-88271c9c670c',N'test-29ce8c050c314834b84b1b284381023c',N'Calibration',N'Scheduled',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),1,N'29466ba9-74f1-4976-b8c3-79bfd2d8c38e',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
BEGIN TRY
INSERT INTO [dbo].[WorkOrders] ([Id],[Code],[OccurrenceId],[EquipmentId],[EquipmentCodeSnapshot],[EquipmentNameSnapshot],[DepartmentIdSnapshot],[DepartmentNameSnapshot],[WorkType],[Status],[ScheduledForUtc],[DueAtUtc],[IsBlocking],[EquipmentUseRegistrationId],[AggregateVersion],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'a3bef603-2153-4fe0-a067-3bbd5ab7e7e9',N'test-d46feeee6a60438889c42c56ba9b6669',N'c5c11c9f-0675-4d49-af89-33b71133ddc1',N'35dce1dd-1304-4bab-8398-4c66b1570148',N'test-df366fd22ac144ad9486b2feced50787',N'test-3bc445f12034451da9f62443da3b1517',N'152cef43-4f1c-4dd4-8594-058750d7ccd5',N'test-6775361269e6403197474a95d3dea5ae',N'Calibration',N'Scheduled',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),1,N'1a5f5ae1-cd5c-46dd-879f-d7b3e4a29f33',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
INSERT INTO [dbo].[WorkOrders] ([Id],[Code],[EquipmentId],[EquipmentCodeSnapshot],[EquipmentNameSnapshot],[DepartmentIdSnapshot],[DepartmentNameSnapshot],[WorkType],[Status],[ScheduledForUtc],[DueAtUtc],[IsBlocking],[EquipmentUseRegistrationId],[AggregateVersion],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'1ff8c935-8c7f-45be-ba6c-d1a680a0c1a1',N'test-8565bde508234a8287232aac31875bf5',N'95b29625-9dde-4dc7-8183-b1d0b0ef8c90',N'test-38bac2fa0a644f4f96281ef8cc111334',N'test-11e45ce119b9452aad065ec5e07940af',N'1314d94a-2541-4367-9438-e64fa5fc7720',N'test-07e4f7907ec848d79c3215ae5630717e',N'Maintenance',N'Scheduled',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),1,N'25b602c5-4ccc-4604-84dc-59f209f35117',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
INSERT INTO [dbo].[WorkOrders] ([Id],[Code],[EquipmentId],[EquipmentCodeSnapshot],[EquipmentNameSnapshot],[DepartmentIdSnapshot],[DepartmentNameSnapshot],[WorkType],[Status],[ScheduledForUtc],[DueAtUtc],[IsBlocking],[EquipmentUseRegistrationId],[AggregateVersion],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (N'a1992a8d-c3ca-4af0-a254-769f22f72de4',N'test-d3e12c8cf9354323aeca7e24b8795ff8',N'e2a0c4d6-f3d5-44bd-8f63-1d6584163efc',N'test-414569d7f2844a3282efe3067ee0fea8',N'test-7448c25e66354fb3ae62d55a0ba91d8c',N'7708b227-e581-44f2-8d52-6fee856b2a5e',N'test-72b36c38413e42e7a3c14181f731d30d',N'Maintenance',N'Scheduled',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),1,N'48b19575-1909-415c-8262-518e59d7864d',1,CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset));
INSERT INTO [dbo].[WorkOrderAttempts] ([Id],[WorkOrderId],[AttemptNumber],[PerformedByUserId],[CompletedAtUtc],[ResultStatus],[Notes],[ReviewDecision],[ReviewedByUserId],[ReviewedAtUtc]) VALUES (N'792434ca-b16b-4406-a56b-95e4c134c298',N'73e11ddd-8b20-4f5c-becf-64fd8fd2d310',1,N'7f62c88a-dac0-4fd2-8ccd-f6800d52dd3a',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),N'Passed',N'test-745640c6eaf145abbe63a07e8fa786a2',N'Accepted',N'a799b8de-bd9d-4744-bae6-74be52954cab','2026-10-23T01:00:00Z');
BEGIN TRY
INSERT INTO [dbo].[WorkOrderAttempts] ([Id],[WorkOrderId],[AttemptNumber],[PerformedByUserId],[CompletedAtUtc],[ResultStatus],[Notes],[ReviewDecision],[ReviewedByUserId],[ReviewedAtUtc]) VALUES (N'ea3f9cd8-217b-4339-9e2c-93f95c123a8d',N'73e11ddd-8b20-4f5c-becf-64fd8fd2d310',2,N'e63f2506-2b28-4b56-bb7a-18411a7e6249',CAST('2026-10-23T00:00:00+00:00' AS datetimeoffset),N'Passed',N'test-006a8e985f0f4ffe85820f1dea4ba4bc',N'Accepted',N'a799b8de-bd9d-4744-bae6-74be52954cab','2026-10-23T01:00:00Z');
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
INSERT INTO [dbo].[CalibrationMeasurements] ([Id],[WorkOrderAttemptId],[ParameterCode],[ParameterName],[Unit],[MeasuredValue],[MinimumAllowed],[MaximumAllowed],[Passed]) VALUES (N'7ce6352b-4137-4fdd-928a-d052847f8f8b',N'792434ca-b16b-4406-a56b-95e4c134c298',N'test-16757cba28c140bba165cbf79a31bf65',N'test-0bc0d6b00ed843c793b545bdfd60dcd8',N'test-82db3198ae9542d0863a31257',5,0,10,1);
BEGIN TRY
INSERT INTO [dbo].[CalibrationMeasurements] ([Id],[WorkOrderAttemptId],[ParameterCode],[ParameterName],[Unit],[MeasuredValue],[MinimumAllowed],[MaximumAllowed],[Passed]) VALUES (N'4896df13-890e-4683-ab69-86b3ae3c2fd3',N'792434ca-b16b-4406-a56b-95e4c134c298',N'test-d04045d71484439b950366888736f485',N'test-b8057336257c4730b9ecc5ffb0ddd2f7',N'test-d5f5f26790bb43158360ceab2',50,0,10,1);
THROW 51000, 'Expected database rejection did not occur', 1;
END TRY
BEGIN CATCH
IF ERROR_NUMBER() NOT IN (547) THROW;
END CATCH;
ROLLBACK TRANSACTION;
PRINT 'PASS: Maintenance persistence constraints; fixtures rolled back';
END TRY
BEGIN CATCH
IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
