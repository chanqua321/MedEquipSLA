# MedEquipSLA — Database Documentation

## 1. Trạng thái và nguồn thiết kế

Ngày audit 23/10/2026. Có **5 SQL Server database, 47 bảng ứng dụng và 5 bảng `__EFMigrationsHistory`** đã tạo thực tế bằng EF Core Code First trên `(localdb)\MSSQLLocalDB`, SQL Server 2025 17.0.4025.3, compatibility 160. [Verification](database/VERIFICATION.md) ghi các lần apply và constraint tests. Đây không chỉ là số lượng từ ERD. Môi trường chuẩn nhóm vẫn là SQL Server 2022 CU27 trong Compose; Engine container chưa chạy được trong phiên xác minh.

| Mức trạng thái | Ý nghĩa trong dự án |
|---|---|
| Proposed Schema | Bản thiết kế trước phê duyệt; không chứng minh có bảng. BR gốc vẫn Proposed |
| Approved Schema | [ERD v0.1/D1–D4](database/APPROVAL.md) được người dùng duyệt; [erd-model.json](database/erd-model.json) là nguồn thiết kế |
| Implemented Schema | Entity/Fluent API, migration/snapshot, SQL export và schema SQL Server đã đối chiếu với ERD |
| Implemented behavior | Chưa có đầy đủ application workflow; schema không thay CRUD/authorization/worker/protocol |

Không đổi BR gốc thành Approved chỉ vì database đã tạo. Schema tương lai cần sửa ERD, review và migration tên mới; không chỉnh lại baseline đã apply vào môi trường có dữ liệu.

[Finalization audit](database/FINALIZATION-AUDIT.md) và [BR D1–D4](business/BUSINESS-RULES-D1-D4-APPROVED.md) bổ sung EF model/migration rollback/SQL tests; không sinh trùng InitialCreate hoặc đổi ERD.

## 2. Database ownership matrix

| Owner / DbContext | Database | Nghiệp vụ/Identity | Kỹ thuật cục bộ | Tổng ứng dụng | Cả history |
|---|---|---:|---|---:|---:|
| IdentityDbContext | MedEquip_Identity | 8 | AuditEntries, IdempotencyRequests | 10 | 11 |
| EquipmentDbContext | MedEquip_Equipment | 7 | AuditEntries, IdempotencyRequests, OutboxMessages, InboxMessages | 11 | 12 |
| MaintenanceDbContext | MedEquip_Maintenance | 7 | AuditEntries, IdempotencyRequests, OutboxMessages | 10 | 11 |
| IncidentDbContext | MedEquip_Incident | 6 | AuditEntries, IdempotencyRequests, OutboxMessages | 9 | 10 |
| NotificationDbContext | MedEquip_Notification | 4 | AuditEntries, IdempotencyRequests, InboxMessages | 7 | 8 |
| **Tổng** | **5 database** | **32** | **15 bản sao owner-local** | **47** | **52** |

Gateway/Web không sở hữu DB nghiệp vụ. Có thể dùng một SQL Server instance cho dev nhưng vẫn là năm DB/users/connection riêng; không biến thành shared schema/database. Identity/Notification chưa có business producer theo C1 nên không thêm Outbox không dùng.

## 3. Entity, ERD và data dictionary

Mỗi dictionary liệt kê **từng cột, SQL type, nullability, PK/FK/UK/REF, quan hệ và rule/index**. Không nhân bản 515 cột ở tài liệu tổng quan này.

| Owner | Entity → table nghiệp vụ/Identity | ERD + dictionary |
|---|---|---|
| Identity | ApplicationUser → AspNetUsers; `IdentityRole<Guid>` → AspNetRoles; `IdentityUserRole<Guid>` → AspNetUserRoles; `IdentityUserClaim<Guid>` → AspNetUserClaims; `IdentityRoleClaim<Guid>` → AspNetRoleClaims; `IdentityUserLogin<Guid>` → AspNetUserLogins; `IdentityUserToken<Guid>` → AspNetUserTokens; RefreshToken → RefreshTokens | [Identity](database/services/Identity.md), [SVG](database/images/Identity-ERD.svg) |
| Equipment | Department → Departments; EquipmentCategory → EquipmentCategories; EquipmentAsset → EquipmentAssets; EquipmentStateProjection → EquipmentStateProjections; EquipmentUseRegistration → EquipmentUseRegistrations; EquipmentStatusHistory → EquipmentStatusHistories; ReconciliationCheckpoint → ReconciliationCheckpoints | [Equipment](database/services/Equipment.md), [SVG](database/images/Equipment-ERD.svg) |
| Maintenance | MaintenanceSchedule → MaintenanceSchedules; ScheduleOccurrence → ScheduleOccurrences; WorkOrder → WorkOrders; WorkOrderAttempt → WorkOrderAttempts; CalibrationMeasurement → CalibrationMeasurements; OccurrenceReminder → OccurrenceReminders; WorkOrderActivity → WorkOrderActivities | [Maintenance](database/services/Maintenance.md), [SVG](database/images/Maintenance-ERD.svg) |
| Incident | SlaPolicy → SlaPolicies; SlaPolicyVersion → SlaPolicyVersions; IncidentRecord → Incidents; ResolutionAttempt → ResolutionAttempts; IncidentActivity → IncidentActivities; SlaDeadlineAlert → SlaDeadlineAlerts | [Incident](database/services/Incident.md), [SVG](database/images/Incident-ERD.svg) |
| Notification | NotificationRecord → Notifications; NotificationPreference → NotificationPreferences; EmailDelivery → EmailDeliveries; EmailDeliveryAttempt → EmailDeliveryAttempts | [Notification](database/services/Notification.md), [SVG](database/images/Notification-ERD.svg) |

Các loại kỹ thuật AuditEntry/IdempotencyRequest/OutboxMessage/InboxMessage nằm ở Infrastructure.Persistence.Entities của owner, không chia sẻ CLR entity giữa service. Xem [technical dictionary](database/TECHNICAL-TABLES.md), [Technical Tables ERD](database/images/TECHNICAL-TABLES-ERD.svg), [ERD tổng hợp](database/images/ALL-SERVICES-ERD.svg), [database index](database/README.md).

## 4. Primary keys, foreign keys và relationships

Phần lớn entity dùng Guid PK. Ngoại lệ: Identity claims dùng int identity; Identity junction/login/token dùng composite PK; EquipmentStateProjections `(SourceService,SourceAggregateId)`; ReconciliationCheckpoints `SourceService`; SlaPolicyVersions `(SlaPolicyId,Version)`; NotificationPreferences `(UserId,NotificationKind)`; InboxMessages `(ConsumerName,EventId)`; OutboxMessages EventId, IdempotencyRequests OperationId là PK Guid.

| Quan hệ trong cùng database | Cardinality / FK |
|---|---|
| Identity Users ↔ Roles | N:N qua UserRoles; claims/logins/tokens là children; RefreshTokens.UserId → Users.Id |
| RefreshToken → replacement | Self optional 1:1 qua ReplacedByTokenId filtered unique; cùng user/family cần application check |
| Departments/Categories → EquipmentAssets | 1:N, Equipment.DepartmentId/CategoryId FK bắt buộc |
| EquipmentAssets → projection/registration/history | 1:N; history có From/ToDepartment FK tùy chọn |
| Schedule → Occurrences | 1:N, ScheduleId FK |
| Occurrence → WorkOrder | Một occurrence có 0..1 WO; WO có thể không thuộc occurrence (manual) |
| WorkOrder → Attempts → Measurements | 1:N → 1:N; activities/reminders tham chiếu owner-local |
| SlaPolicy → Versions → Incidents | 1:N → 1:N; Incident FK ghép `(SlaPolicyId,SlaPolicyVersion)` → `(SlaPolicyId,Version)` |
| Incident → RelatedIncident | Self optional N:1; cùng EquipmentId là application invariant |
| Incident → Attempts/Activities/Alerts | 1:N, lịch sử không bị ghi đè |
| Notification → EmailDelivery → Attempts | 1:0..1 → 1:N |

Tất cả 29 FK hiện **NO ACTION**, enabled/trusted. Không cascade xóa audit/lịch sử và không multiple cascade paths. Việc xóa/archive phải qua use case kiểm tra, không phát sinh delete tự động từ ORM. Không có AcceptedAttempt FK vòng ngược từ aggregate; filtered unique chọn tối đa một attempt Accepted, invariant “Closed cần accepted attempt” vẫn cần Application/Domain.

### Logical references giữa service

| Cột/reference | Nguồn sự thật | Quy tắc |
|---|---|---|
| UserId/TechnicianId/ActorUserId | Identity | gRPC/quyền đích; không FK tới DB Identity |
| DepartmentId trong Identity | Equipment | Reference + DepartmentNameSnapshot; không copy quyền ghi Department |
| EquipmentId trong Maintenance/Incident | Equipment | Lookup/registration gRPC; giữ code/name/khoa snapshot |
| EquipmentUseRegistrationId tại source | Equipment | ID phối hợp, không SQL navigation/FK xuyên DB |
| SourceAggregateId/ResourceId ở projection/notification | Owner nguồn tương ứng | Event/resource resolver, không JOIN DB nguồn |

`REF` trong dictionary là logical reference, không phải FK còn thiếu. Không SQL JOIN/View/FK/distributed transaction xuyên DB, kể cả dashboard; dùng API hoặc read projection/event với trạng thái đồng bộ rõ ràng.

## 5. SQL types, nullability và concurrency

| Ý nghĩa | SQL Server / C# |
|---|---|
| ID | uniqueidentifier / Guid |
| Thời điểm nghiệp vụ | datetimeoffset / DateTimeOffset, chuẩn hóa UTC; UI Asia/Ho_Chi_Minh |
| Ngày mua/bảo hành | date / DateOnly? |
| Code/name/status/notes | nvarchar độ dài hữu hạn theo dictionary; JSON/large text nvarchar(max) |
| Tiền / measurements | decimal(18,2) / decimal(18,6), không float cho tiền |
| Token/request hash | binary(32), không lưu refresh token thô |
| Concurrency | rowversion / byte[], SQL sinh, `IsRowVersion()` |
| Thứ tự event | bigint AggregateVersion / long; application tăng, không đồng nhất rowversion |

Nullable đánh dấu bằng cột NULL trong dictionary và nullable C# type. Optional OccurrenceId không có nghĩa được chọn một occurrence thuộc source khác. RowVersion không phải timestamp/clock: client dự kiến gửi version gốc, EF phát hiện stale update; API mapping 409 chưa implement. SQL test đã xác minh stale predicate không ghi đè.

## 6. Unique indexes và check constraints

Các index/check nằm trong `Persistence/Configurations` từng Infrastructure, được migration tạo thực. `UK` trong dictionary có thể là một phần khóa ghép, không có nghĩa mọi cột đứng riêng unique.

| Nhóm ràng buộc quan trọng | Implementation |
|---|---|
| Identity normalized username/email | Unique filtered IS NOT NULL; RefreshToken.TokenHash unique, replacement filtered unique |
| Equipment asset/catalog | AssetCode, Department/Category Code unique; SerialNumber filtered unique khi khác NULL |
| Registration | Unique `(SourceService,ResourceType,ResourceId)` và `(SourceService,SourceOperationId)` |
| Occurrence/WO | Unique `(ScheduleId,ScheduledOccurrenceAtUtc)`; WO.OccurrenceId filtered unique, cho nhiều NULL manual WO |
| Attempts | Unique `(OwnerId,AttemptNumber)` và filtered unique OwnerId khi ReviewDecision=Accepted |
| SLA/reminder | Priority unique; policy effectiveFrom key; `(IncidentId,AlertKind)` và `(OccurrenceId,ReminderKind)` unique |
| Notification/email | `(SourceEventId,RecipientUserId,Kind)` unique; EmailDelivery.NotificationId/MessageId unique; `(DeliveryId,AttemptNumber)` unique |
| Idempotency/Inbox | `(ActorScope,OperationName,IdempotencyKey)` unique; composite Inbox PK |

87 check constraints gồm enum trạng thái/nguồn, version/duration dương, deadline bằng snapshot, retired flag/status/time consistency, accepted/rejected review actor/time, lý do reject, threshold calibration và Passed, JSON hợp lệ, expiration và email state/timestamps. Metadata/behavior checks đã thử trường hợp sai thật trên SQL Server; xem [tests](../tests/database/).

SQL **chưa bảo đảm toàn bộ**: policy intervals không overlap; Closed có Accepted attempt; related Incident cùng Equipment; registration protocol/fencing; role/assignment/current department; calibration blocking xuyên aggregate. Các invariant này cần Domain/Application transaction/protocol tests, không thể chỉ suy từ FK/check.

## 7. Audit, Outbox/Inbox, idempotency

AuditEntries có actor/service/action/entity/time/correlation/ChangesJson, append-only theo use case; application sẽ ghi cùng local transaction. Schema không có trigger tự ghi audit, chưa có command auditing hoàn chỉnh.

Outbox ở Equipment/Maintenance/Incident chứa event/schema/aggregate version, payload JSON, status, attempt/next retry/lease/error và published time. Inbox ở Equipment/Notification dedupe consumer/event. **Bảng đã có; atomic write/dispatcher/consumer/ACK/retry/DLQ còn Planned.** Không đánh dấu nghiệp vụ event PASS vì có schema.

IdempotencyRequests giữ operation ID, actor scope/key, request hash, ResourceId, Pending/Committed/Aborted, response và expiration. Không xóa tombstone/Pending còn tham gia D3 chỉ vì TTL. Client retry khác request hash cùng key cần policy conflict tại Application (chưa code).

## 8. Equipment registrations và reconciliation

D3 dùng registration Pending/Active/Released/Aborted, owner/resource/operation/version để phối hợp source với Equipment. Pending/Active chặn Retire; Register/Retire serialize trên cùng row tại Equipment. Source giữ operation tombstone trước/sau commit; không có transaction phân tán.

Checkpoint lưu source/snapshot/watermark/cursor/state/start/completed/error, bảo đảm có chỗ theo dõi đối soát. Snapshot APIs, fencing/abort và xử lý event đồng thời chưa được chốt bằng contract/test. Không coi bảng checkpoint là worker đã hoạt động; không xóa blocking do một page rỗng hoặc source unavailable.

## 9. EF Core, Fluent API và migration strategy

Đường dẫn thống nhất: `src/Services/<Service>/<Service>.Infrastructure/Persistence`. DbContext giữ DbSet và ApplyConfigurationsFromAssembly; Identity gọi base IdentityDbContext để map framework tables, sau đó override Fluent configuration. Entity nghiệp vụ ở Domain; ApplicationUser framework type ở Infrastructure. Alias `Records` của các non-Identity context trỏ cùng entity, **không tạo bảng thứ hai**.

Runtime DI dùng SQL Server compatibility 160. Design-time factory đọc `ConnectionStrings__Database`, kiểm tra catalog đúng service hoặc namespace Test của service; không chạy workers để sinh migration. Mỗi owner có InitialCreate, Designer và ModelSnapshot riêng. Không tự Migrate từ app startup, không dùng EnsureCreated và không có shared migration project.

```sh
dotnet tool restore
dotnet ef migrations add DescribeYourChange --project src/Services/Equipment/Equipment.Infrastructure --startup-project src/Services/Equipment/Equipment.Api --context EquipmentDbContext --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/Services/Equipment/Equipment.Infrastructure --startup-project src/Services/Equipment/Equipment.Api --context EquipmentDbContext
dotnet ef migrations script --idempotent --project src/Services/Equipment/Equipment.Infrastructure --startup-project src/Services/Equipment/Equipment.Api --context EquipmentDbContext --output docs/database/migrations/Equipment.sql
```

`InitialCreate` đã tồn tại, không tạo lại tên đó. SQL exports trong [migrations](database/migrations/) để review/deploy, C# migrations/model là nguồn thay đổi. Review DropColumn/AlterColumn/truncation, backup môi trường có dữ liệu và có kế hoạch rollback/forward fix trước deploy. Không drop DB để vượt lỗi baseline. DB có bảng cũ mà chưa history cần baseline riêng sau đối chiếu, không giả định tương đương.

Apply qua `pwsh ./scripts/Update-Databases.ps1` với năm `MEDEQUIP_<SERVICE>_MIGRATION_CONNECTION`; script có thể chọn một service. Runtime user chỉ datareader/datawriter; deployment identity có DDL tại DB được phép. Bootstrap tạo database/logins, không tạo business tables. [SETUP](database/SETUP.md) và [Environment Setup](development/ENVIRONMENT-SETUP.md) có bước cấu hình. Windows dev có thể dùng `-LocalDb`; không phải yêu cầu cho cả team.

## 10. Seed và verification

Chỉ seed ba role ổn định Admin/Technician/DepartmentStaff bằng HasData với Guid/concurrency stamp cố định. Không seed user/password/key, SLA giả hoặc dữ liệu demo như tính năng đã chạy. Demo seed tương lai phải idempotent qua use cases với audit, tránh bypass invariant.

Đã áp dụng 5 migration từ DB rỗng, chạy lại SQL idempotent/wrapper, kiểm tra no pending model changes. Mười SQL suites đối chiếu 515 cột/type/nullability/PK/FK và thử unique/check/rowversion; fixtures rollback. Chạy `pwsh ./scripts/Test-Databases.ps1` cho LocalDB; SQL authentication theo SETUP dùng SQLCMDPASSWORD, không `-P` trên command line.

Docker SQL Server 2022/credentials isolation và full-stack E2E còn chưa xác minh do Engine unavailable. LocalDB compatibility 160 không chứng minh engine 2022 đã chạy. [Evidence DB](database/VERIFICATION.md), [audit tài liệu](assignment-compliance/DOCUMENTATION-VERIFICATION.md), [PRN232 matrix](assignment-compliance/PRN232-CHECKLIST.md).

EF integration tests hiện có 5 model + 8 SQL runtime cases; xem [test README](../tests/MedEquipSLA.Database.IntegrationTests/README.md). Các case migration rollback chỉ dùng database mới do fixture tạo, không rollback năm application DB. Kết quả runtime cụ thể ở finalization audit.
