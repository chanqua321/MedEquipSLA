# MedEquipSLA — Database hiện hành

Cập nhật 07/11/2026. **Approved** là quyết định nghiệp vụ/schema đã được chủ dự án duyệt; **Implemented** là Entity/configuration/migration đã có; **Verified** phải có kiểm thử thực. Các báo cáo Stage cũ giữ nguyên giá trị lịch sử, không đại diện cho schema mới.

Nguồn: [dictionary ERD](database/erd-model.json), [phê duyệt ERD](database/APPROVAL.md), [D1–D4](business/BUSINESS-RULES-D1-D4-APPROVED.md), [Identity](business/IDENTITY-RBAC-DECISIONS.md), [lifecycle bổ sung](business/BACKEND-LIFECYCLE-DECISIONS.md), EF configurations/migrations và [test SQL](../tests/MedEquipSLA.Database.IntegrationTests/README.md). Business Rules v1.0 gốc vẫn giữ Proposed ở những phần chưa được duyệt; không tự nâng toàn bộ tài liệu thành Approved.

## Database per service

**54 bảng ứng dụng, 649 cột ứng dụng, 31 FK nội bộ, 20 migrations; thêm 5 bảng __EFMigrationsHistory.** Không có FK/JOIN/navigation xuyên database. Database có thể cùng SQL Server instance nhưng credential và quyền ghi tách riêng.

| Owner | Catalog | Bảng ứng dụng | Migrations | Entity/bảng |
|---|---|---|---|---|
| Identity | MedEquip_Identity | 11 | 3 | `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `RefreshTokens`, `AuditEntries`, `IdempotencyRequests`, `OutboxMessages` |
| Equipment | MedEquip_Equipment | 13 | 3 | `Departments`, `EquipmentCategories`, `EquipmentAssets`, `EquipmentStateProjections`, `EquipmentUseRegistrations`, `EquipmentStatusHistories`, `ReconciliationCheckpoints`, `EquipmentRetirementIntents`, `RetirementSourceReceipts`, `AuditEntries`, `IdempotencyRequests`, `OutboxMessages`, `InboxMessages` |
| Maintenance | MedEquip_Maintenance | 12 | 7 | `MaintenanceSchedules`, `ScheduleOccurrences`, `WorkOrders`, `WorkOrderAttempts`, `CalibrationMeasurements`, `OccurrenceReminders`, `WorkOrderActivities`, `EquipmentRetirementGates`, `AuditEntries`, `IdempotencyRequests`, `OutboxMessages`, `InboxMessages` |
| Incident | MedEquip_Incident | 11 | 5 | `SlaPolicies`, `SlaPolicyVersions`, `Incidents`, `ResolutionAttempts`, `IncidentActivities`, `SlaDeadlineAlerts`, `EquipmentRetirementGates`, `AuditEntries`, `IdempotencyRequests`, `OutboxMessages`, `InboxMessages` |
| Notification | MedEquip_Notification | 7 | 2 | `Notifications`, `NotificationPreferences`, `EmailDeliveries`, `EmailDeliveryAttempts`, `AuditEntries`, `IdempotencyRequests`, `InboxMessages` |

Các bảng kỹ thuật cùng tên là bảng riêng của từng owner. Identity có Outbox bảo mật/quyền; Maintenance/Incident có Inbox cho thu hồi quyền. `IdempotencyRequests` của hai source có metadata D3 riêng, không dùng cấu hình chung để mất các cột này. `AuditEntries` ghi actor thật (user hoặc service), CorrelationId và thay đổi; LastError không thay Audit.

## Cách đọc ERD

Mở [MedEquipSLA_ERD.drawio](../output/handoff/MedEquipSLA_ERD.drawio) bằng diagrams.net. Có 1 trang hướng dẫn và 5 tab database; phần A chứa bảng đầy đủ cột, phần B tách từng FK thành panel khóa riêng. Panel lặp cột để đọc quan hệ, không tạo bảng mới. Connector neo trực tiếp dòng PK/FK, có lane riêng nên không chồng chéo hoặc nối vào connector khác. REF là tham chiếu logic, không phải FK bị thiếu. Tạo lại bằng `python scripts/Export-Erd.py`; tùy chọn `--preview` cần Pillow, kiểm tra 59 bảng gồm EF history và 31 đường FK.

GUID dùng uniqueidentifier; thời gian UTC dùng datetimeoffset; ngày mua/bảo hành dùng date. Chuỗi có giới hạn/Unicode theo dictionary. RowVersion là SQL Server rowversion 8 byte, không phải datetime; client gửi base64 đúng phiên bản khi sửa. AggregateVersion tăng theo thay đổi nghiệp vụ để consumer bỏ qua event cũ; FencingToken dành cho quyền sở hữu attempt D3, RetirementEpoch dành cho intent thanh lý — ba giá trị không thay thế nhau.

## Quan hệ và ràng buộc

| Quan hệ | Bảo đảm |
|---|---|
| Departments/Categories → EquipmentAssets | FK bắt buộc; logical archive giữ thiết bị/lịch sử cũ, không nhận liên kết mới tới danh mục Inactive |
| EquipmentAssets → Registrations/Projections/History/RetirementIntents | Mọi registration, trạng thái và retirement đều thuộc cùng Equipment; final Retire serialize trên hàng Equipment |
| RetirementIntent → SourceReceipts | FK ghép Id/EquipmentId/Epoch chống receipt của intent/thiết bị khác; source/coverage/proof phải hợp lệ |
| Schedule → Occurrences → WorkOrder | Unique ScheduleId + ScheduledOccurrenceAtUtc; WorkOrders.OccurrenceId filtered unique (mỗi kỳ tối đa một WO) |
| WorkOrder → Attempts → Measurements | Giữ mọi attempt; chỉ một Accepted; Failed Calibration không được accept/Closed |
| Occurrence → Reminders | Unique occurrence + reminder kind; Outbox commit cùng reminder |
| Source IdempotencyRequests → Occurrence | FK nội bộ ở Maintenance cho operation sinh WorkOrder; unique operation theo kỳ; metadata legacy giữ NULL khi không biết |
| SlaPolicy → Versions → Incidents | FK ghép policy/version; mỗi hồ sơ snapshot duration và deadline, không tính lại sau sửa policy |
| Incident → Attempts/Activities/Alerts | Unique attempt number/accepted attempt và alert kind; self RelatedIncident cùng Equipment là application invariant |
| Notification → EmailDelivery → Attempts | Mỗi notification tối đa một email delivery, retry có lịch sử riêng; preference là snapshot lúc nhận event |
| Identity user/role/session | ASP.NET Core Identity; một Admin qua bootstrap có khóa SQL; email/username duy nhất; refresh hash duy nhất |

Tất cả FK **NoAction**; không cascade xóa lịch sử. Unique/filtered indexes, check constraints và nullability được so sánh tự động với SQL thật, không chỉ xem migration. Danh sách chi tiết nằm trong dictionary/configurations và `tests/database/*-Schema.sql`.

## D3 và trạng thái kỹ thuật

Source persist Pending trước RPC. Equipment nhận đăng ký dưới Equipment row lock; source commit aggregate + operation Committed + Audit + Outbox trong local transaction, không có distributed transaction. Timeout giữ Pending/unknown. CancelRequested chỉ là ý định, chưa phải Aborted; cần Equipment tombstone cùng binding/fence rồi mới ghi Aborted tại source. Không xóa operation thất bại hoặc giải phóng theo tuổi dữ liệu.

RetirementIntent đóng admission ở Equipment và source gate Maintenance/Incident. Source chỉ Sealed sau khi đã hết Pending và hồ sơ/blocker liên quan dưới khóa gate; Sealed giữ đóng đến khi có quyết định Equipment bền. Inactive schedule vẫn giữ registration. Ended schedule không thể reactivate, release chỉ khi hết Pending và WO mở. [Protocol và concurrency](development/WORKORDER-MODULE3-STAGE1-DESIGN.md).

Đối soát [snapshot/checkpoint](development/D3-SNAPSHOT-RECONCILIATION.md) là inventory nhất quán và positive proof có version. Không coi record vắng mặt, checkpoint Completed hoặc timeout là quyền release/Retire. Pending/legacy unknown tiếp tục chặn đến khi giải quyết bằng nghiệp vụ được duyệt.

## Code First và migration

### Identity

- `20261023044348_InitialCreate`
- `20261106180429_AddIdentitySecurityOutbox`
- `20261106193753_AddInvitationSuspension`
### Equipment

- `20261023044354_InitialCreate`
- `20261106063402_AddEquipmentRetirementFoundation`
- `20261106164929_AddResourceAbortProtocol`
### Maintenance

- `20261023044400_InitialCreate`
- `20261105101458_AddOccurrenceReminderWindow`
- `20261106073414_AddSourceRetirementGate`
- `20261106154420_AddWorkOrderD3Operations`
- `20261106162215_AddWorkOrderOperationPhoto`
- `20261106170013_AddScheduleTermination`
- `20261106185052_AddUserAccessInbox`
### Incident

- `20261023044406_InitialCreate`
- `20261104183808_AddIncidentOperationPhoto`
- `20261106073418_AddSourceRetirementGate`
- `20261106164929_AddIncidentCancellationProtocol`
- `20261106185055_AddUserAccessInbox`
### Notification

- `20261023044411_InitialCreate`
- `20261106172832_AddNotificationChannels`

InitialCreate không bị sửa. Migrations mới có guard Down khi cột/bảng đang giữ proof/tombstone/history protocol. Không reset DB hoặc dùng EnsureCreated. Review SQL xuất từ EF ở [migrations](database/migrations/Identity.sql), chạy Up/Down/reapply trên database test trước khi triển khai. Các guard được kiểm bằng dữ liệu protocol thật, bao gồm Identity suspension/security Outbox, source access Inbox và D3.

**Tách rõ môi trường:** ứng dụng hiện hữu vẫn InitialCreate; phiên này không apply migration ứng dụng. Fixture đã apply toàn bộ 20 migrations trên SQL Server 2022 test và LocalDB. Chỉ catalog `MedEquip_<Owner>_Test_<GUID>` do harness tạo mới được ghi/xóa. Runtime login mỗi owner có datareader/datawriter, không có DDL hoặc quyền database owner khác. Năm test SQL login isolation kiểm cả trường hợp bị từ chối.

Theo [runbook](development/BACKEND-RUNBOOK.md) để operator nâng schema, chạy job migration một lần và bootstrap Admin. Mặc định production/Compose không cho replicas tự Migrate lúc startup. Development startup migration chỉ opt-in; không Start binary mới vào application schema cũ khi chưa rollout migration.

[Evidence và giới hạn](development/BACKEND-COMPLETION-TRACKER.md), [Technical tables](database/TECHNICAL-TABLES.md), [PRN232 matrix](assignment-compliance/PRN232-CHECKLIST.md).
