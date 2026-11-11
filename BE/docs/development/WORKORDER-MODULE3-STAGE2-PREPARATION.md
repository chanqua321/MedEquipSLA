# MedEquipSLA — Module 3 / Stage 2 Preparation

Ngày kiểm tra: **06/11/2026**, Asia/Bangkok. Repository duy nhất: `D:\PRN232\ASS\MedEquipSLA`.

Phạm vi lượt này: đọc tài liệu/source, đọc metadata SQL Server và **chỉ tạo báo cáo này**. Không sửa Entity/DTO/Proto, không tạo/apply migration, không chạy API có auto-migrate, không chạy tests có ghi database, không commit/push. Đây chưa phải implementation Stage 2 và không phải phê duyệt R1–R4.

Nguồn bắt buộc đã đọc đầy đủ:
- [Stage 0 Audit](WORKORDER-MODULE3-STAGE0-AUDIT.md), đã được chủ dự án chấp thuận.
- [Stage 1 Design — Revision 1](WORKORDER-MODULE3-STAGE1-DESIGN.md).
- [Stage 1 Revision Report](WORKORDER-MODULE3-STAGE1-REVISION-REPORT.md).

## 1. Repository state

| Nội dung | Quan sát tại đầu lượt Preparation |
|---|---|
| Branch | feature/database-code-first |
| HEAD | ddd7507b5afd94c3e8eea5f979c46f911741f421 |
| Tracked files có thay đổi chưa stage | 98 |
| Untracked files, đếm với --untracked-files=all | 86 |
| Staged changes | 0 |
| Working tree | Dirty; có thay đổi từ các lượt trước ở backend, frontend, docs, compose, lock files và tests |
| File fingerprint kiểm tra trước lượt ghi báo cáo | 459 file tracked/untracked hiện hữu, SHA256 in-memory phục vụ đối chiếu cuối lượt |
| AGENTS.md | Không tìm thấy qua rg --files trong D:\PRN232 với các thư mục tools/node_modules được loại trừ |

Không reset, không checkout branch khác, không clone/worktree mới. Không quy kết các thay đổi sẵn có là do ai thực hiện. Số untracked ở trên là **file**, không phải số dòng status dạng gộp thư mục.

Các nhóm thay đổi có sẵn đáng chú ý:
- EquipmentAsset, EquipmentUseRegistration, EquipmentGrpcService, Program, DI, AuditEntry và các workers.
- ScheduleCommands/Endpoints, OccurrencePlanner, ReminderPlanner, ScheduleOutboxDispatcher và tests Module 1/2.
- Incident creation/workflow/evidence và migration AddIncidentOperationPhoto.
- Stage 0/1 documents và các tài liệu database/business/C1.
- Packages lock, compose/.env.example và source frontend cũng đã dirty; Preparation không xử lý chúng.

### So với Stage 0
Không có commit/fingerprint riêng tại thời điểm Stage 0 cho toàn bộ source chưa commit, nên không thể chứng minh diff byte-for-byte từ audit cũ. Không lấy git diff với HEAD làm diff “từ Stage 0”.

Qua đọc lại các implementation liên quan:
- Vẫn chỉ có GetServiceInfo, GetEquipment, RegisterIncidentUse và RegisterScheduleUse; chưa có RPC WO/retirement mới.
- Vẫn chưa có RetirementIntent, RetirementSourceReceipt, source admission gate hay domain Retire transition.
- Danh sách migrations vẫn như Stage 0; chưa có migration Module 3.
- Tài liệu Stage 1 đã đổi sang durable source gate + Sealed proof; source chưa triển khai thiết kế đó.
- Phát hiện cần làm rõ hơn so với báo cáo cũ: IncidentCreationEndpoints giữ SQL transaction trong lúc gọi RegisterIncidentUse; Equipment Audit có cột ActorService nhưng factory chưa gán được ActorService; replay registration hiện trả snapshot thiết bị hiện tại, không phải snapshot đăng ký bền.
Đây là các finding từ source hiện tại, không khẳng định chúng mới được ai thêm sau Stage 0.

## 2. Source inventory thực tế

Đường dẫn dưới đây tương đối với repository ở đầu báo cáo. Tên file trong các sub-stage tương lai được đánh dấu **dự kiến**, không phải file đã tồn tại.

| File/path đã đọc hoặc kiểm tra trực tiếp | Chức năng và finding |
|---|---|
| src/Services/Equipment/Equipment.Domain/EquipmentAsset.cs | Register/profile/transfer và ApplyBlockingStatus; có retirement fields, RowVersion/AggregateVersion, priority Retired > Broken > UnderMaintenance > Operational. Không có Retire method hoặc coordination state. |
| src/Services/Equipment/Equipment.Domain/EquipmentUseRegistration.cs | Register Pending; ApplyIncidentVersion chuyển Active/Released theo SourceVersion. Không có payload/hash/fence/snapshot/abort transition WO. |
| src/Services/Equipment/Equipment.Domain/EquipmentStateProjection.cs | Projection Incident, IsBlocking theo trạng thái khác Closed. Không thay authoritative source gate. |
| src/Services/Equipment/Equipment.Domain/EquipmentStatusHistory.cs | Factories Change và Transfer; cột actor/reason có sẵn, nhưng Change chưa gán actor/reason retirement. |
| src/Services/Equipment/Equipment.Domain/ReconciliationCheckpoint.cs | Checkpoint snapshot theo source, không phải gate per Equipment hoặc retirement receipt. |
| src/Services/Equipment/Equipment.Infrastructure/Persistence/EquipmentDbContext.cs | 11 entity persistence nghiệp vụ/kỹ thuật và Records là alias DbSet của EquipmentAsset; không có RetirementIntent/Receipt. ApplyConfigurationsFromAssembly, ownership riêng Equipment DB. |
| src/Services/Equipment/Equipment.Infrastructure/Persistence/Configurations/EquipmentAssetConfiguration.cs | Check retirement coherence, enum/status, SQL rowversion; check dữ liệu không thay domain command/authorization. |
| src/Services/Equipment/Equipment.Infrastructure/Persistence/Configurations/EquipmentUseRegistrationConfiguration.cs | Unique source/type/resource, unique source/operation; index equipment/state; local NoAction FK. WorkOrder/Aborted đã có trong enum nhưng chưa có behavior tương ứng. |
| src/Services/Equipment/Equipment.Infrastructure/Persistence/Entities/AuditEntry.cs và Configurations/AuditEntryConfiguration.cs | ActorService nvarchar(50) nullable có trong schema; constructor chỉ gán ActorUserId và các trường chung, setter private. Cần service factory thực sự. |
| src/Services/Equipment/Equipment.Infrastructure/Persistence/Configurations/EquipmentStatusHistoryConfiguration.cs | ActorUserId/Reason nullable; SourceEventId unique khi không NULL. Không tạo event ID giả chỉ để ghi Retire history. |
| src/Services/Equipment/Equipment.Infrastructure/Persistence/Entities/IdempotencyRequest.cs và configuration | Persistence shape/unique actor-operation-key có; chưa có factory/transition cho Begin/Continue/Cancel retirement command. |
| src/Contracts/MedEquipSLA.Contracts.Grpc/Protos/equipment.proto | 4 RPC hiện hữu, message register chỉ equipment_id/operation_id; chưa có immutable WO payload/hash/fence/epoch/proof. |
| src/Contracts/MedEquipSLA.Contracts.Grpc/MedEquipSLA.Contracts.Grpc.csproj | Protobuf generated Both, dùng lại contract project để thêm RPC additive. |
| src/Services/Equipment/Equipment.Api/Grpc/EquipmentGrpcService.cs | Register chung khóa Equipment UPDLOCK,HOLDLOCK; cần JWT người dùng + service key hiện hữu. Không có worker-only scheme/policy hoặc retirement gate. Replay đọc snapshot từ asset/department hiện tại. |
| src/Services/Equipment/Equipment.Api/Program.cs; EquipmentAccess.cs | Register gRPC, map catalog/department/query; có JWT session validation; không map Retire/Continue/Cancel. |
| src/Services/Equipment/Equipment.Api/AssetCatalogEndpoints.cs | Admin create/profile/transfer/category; kiểm version/reason cho transfer. Chưa có retirement command. |
| src/Services/Equipment/Equipment.Infrastructure/Persistence/AssetCatalogStore.cs | Profile/transfer dùng Equipment row lock và local transaction; Audit và transfer history/Outbox đã có, tái sử dụng pattern. |
| src/Services/Equipment/Equipment.Application/Abstractions/IEquipmentRepository.cs; Features/ScaffoldCommand/ScaffoldCommand.cs; Infrastructure/Persistence/EquipmentRepository.cs | Repository hiện chủ yếu Exists/query; scaffold command ném NotImplemented. Cần aggregate-specific protocol store/command, không nhét retirement vào generic scaffold. |
| src/Services/Equipment/Equipment.Infrastructure/Workers/IncidentLifecycleConsumer.cs | Equipment row lock → registration/projection/Inbox; ACK sau commit; cần tương thích lock order mới, giữ Incident semantics. |
| src/Services/Equipment/Equipment.Infrastructure/Workers/EquipmentOutboxDispatcher.cs | Chỉ publish DepartmentChanged; chưa có WO consumer hoặc retirement worker. Không tạo Retire event không consumer. |
| src/Services/Equipment/Equipment.Infrastructure/DependencyInjection.cs | Chưa có retirement command/store/worker/options/TimeProvider đăng ký riêng Equipment. |
| src/BuildingBlocks/MedEquipSLA.Hosting/ServiceSetup.cs; IdentitySessionValidation.cs | Mặc định JWT bearer + /api/auth/me validation. Service-only RPC cần auth riêng, không thay default scheme toàn hệ thống hoặc giả JWT Admin. |
| .env.example; compose.yaml | Có placeholder IncidentRegistrationKey/MaintenanceRegistrationKey; chưa có credential riêng cho WO/retirement source-scoped. Không đọc/ghi secret thật trong lượt này. |
| docs/architecture/C1-Container.md | Nguồn Maintenance/Incident → Equipment gRPC và Redis → Equipment đã có trong C1. Poll/report retirement theo đúng chiều này. |
| src/Services/Maintenance/Maintenance.Infrastructure/Persistence/ScheduleCommands.cs; Entities/IdempotencyRequest.cs | Pending bền trước gRPC, local final transaction sau gRPC. Pending không lưu EquipmentId/payload bền; create/update/reactivate chưa có gate. |
| src/Services/Incident/Incident.Api/IncidentCreationEndpoints.cs; Incident.Infrastructure/Persistence/Entities/IdempotencyRequest.cs | Pending được lưu trước, nhưng SQL transaction bắt đầu dòng 58 rồi RPC dòng 92 và commit dòng 123; phải tách network khỏi SQL khi tích hợp source gate. Pending chưa có EquipmentId/immutable request body. |
| src/Services/Incident/Incident.Domain/IncidentRecord.cs; Incident.Api/IncidentWorkflowEndpoints.cs | Open tạo blocker; assign/start/submit/reject xử lý hồ sơ chưa Closed; accept mới Closed. Không thấy reopen Closed trong các transition đã đọc. |
| src/Services/Maintenance/Maintenance.Api/ScheduleEndpoints.cs; WorkOrderEndpoints.cs; Infrastructure/Persistence/WorkOrderAssignment.cs; MaintenanceRepository.cs | Có create/update/reactivate Schedule, read/assign WO; chưa có writer generation tạo WO trong production source được tìm thấy. |

### Inventory writers phải đưa vào coverage

| Writer hiện tại | Tác động | Yêu cầu khi được duyệt R4 |
|---|---|---|
| EquipmentGrpcService.RegisterIncidentUse / RegisterScheduleUse → RegisterUseAsync | Hai entry points, một chỗ insert EquipmentUseRegistration | Dưới Equipment lock, kiểm active intent trước insert mới; replay cũ không tạo reservation mới. Giữ JWT/scope hiện hữu. |
| ScheduleCommands.CreateAsync | Insert source Pending; sau RPC insert Schedule/Committed/Audit/Outbox | Source gate trước insert Pending và trước finalization; thêm equipment linkage/payload; không network trong local TX. |
| ScheduleCommands.UpdateAsync → MaintenanceSchedule.Revise | IsActive có thể false→true, tạo lại blocker Schedule | Gate-first khi mutation tạo blocker, giữ RowVersion và history; Inactive không release registration. |
| IncidentCreationEndpoints.CreateAsync | Insert Pending, RPC registration, insert Incident/Committed/Outbox | Gate-first, tách prepare/network/final transaction; kiểm lại duplicate/SLA/operation trong final TX theo protocol, không làm đổi BR duplicate/SLA. |
| IncidentLifecycleConsumer | Active/Released registration + projection + equipment status/history + Inbox | Giữ Equipment-first lock và terminal priority; không làm mới blocker sau source Sealed sai luật. Đối soát event trễ, không tùy tiện drop event hợp lệ. |
| Incident workflow/evidence endpoints | Update hồ sơ blocker đã có; accept kết thúc hồ sơ | Inventory path trước seal; cho phép terminal resolution hợp lệ, không tự thêm quyền reopen/cancel. |
| WorkOrderAssignment | Thay technician/status version, không tạo registration hoặc WO mới | Không đánh đồng assignment với nguồn tạo blocker mới; phối hợp lock khi cần, regression giữ nguyên hành vi. |
| OccurrencePlanner/ReminderPlanner | Lập kỳ và reminder, không insert D3 Pending/WO | Giữ thuật toán Module 1/2; occurrence không bị xóa khi freeze. Không mặc định mọi worker Maintenance cần dừng. |
| AssetCatalogStore profile/transfer; EquipmentAsset.ApplyBlockingStatus | Snapshot/current asset và trạng thái nội bộ | Giữ Equipment row lock, IsRetired guard; WO snapshot đăng ký mới phải immutable dù transfer xảy ra sau. |
| Tests/SQL smoke trực tiếp insert/update retired/WO | Fixture/admin test writes, không phải production API | Không dùng chúng làm bằng chứng production gate được cover. Kiểm kê import/manual/legacy path và replica cũ trước enable final Retire. |

Tìm bằng rg các insert registration/operation/WO/Schedule/Incident và references RetirementIntent/gate/WorkOrder RPC trong source/test. Chưa thấy importer production tạo các resource này ở phạm vi đã tìm; điều đó không chứng nhận mọi writer ngoài repo hoặc phiên bản deployment đều đã cover. Scaffold Exists không phải writer.

## 3. Database và migration status hiện tại

Đã chạy **read-only sqlcmd** ngày 06/11/2026: SELECT sys.databases và __EFMigrationsHistory của năm catalog. Lần đầu trong sandbox gặp login timeout; chạy lại cùng SELECT với quyền đọc ngoài sandbox thành công, không có DDL/DML/migration command. Cả năm ONLINE, compatibility level 160.

| Owner / database ứng dụng | Migration trong source | Applied đọc trực tiếp trên app DB | Pending so với source |
|---|---|---|---|
| Identity / MedEquip_Identity | 20261023044348_InitialCreate | InitialCreate tương ứng | Không có migration source còn thiếu |
| Equipment / MedEquip_Equipment | 20261023044354_InitialCreate | InitialCreate tương ứng | Không có; Module 3 migration chưa tồn tại |
| Maintenance / MedEquip_Maintenance | 20261023044400_InitialCreate; 20261105101458_AddOccurrenceReminderWindow | Chỉ InitialCreate | AddOccurrenceReminderWindow |
| Incident / MedEquip_Incident | 20261023044406_InitialCreate; 20261104183808_AddIncidentOperationPhoto | Chỉ InitialCreate | AddIncidentOperationPhoto |
| Notification / MedEquip_Notification | 20261023044411_InitialCreate | InitialCreate tương ứng | Không có migration source còn thiếu |

Equipment InitialCreate/snapshot/config có retirement fields và registration shape cũ, không có tables/proof/fence Module 3. Không gọi EF has-pending-model-changes hoặc build trong lượt chỉ chuẩn bị; nhận xét model dựa vào đọc source/config/snapshot, không phải EF runtime validation.

### Phân biệt fixture
- SchemaAndMigrationTests tạo catalog test riêng, apply source migrations và kiểm model/ERD, index/check/rowversion, rollback/reapply.
- TestDatabase chỉ chấp nhận LocalDB/loopback SQL, catalog test sở hữu riêng. Không chạy nó ở Preparation vì nó tạo/drop database.
- Schema test hiện cố định số migrations Equipment=1; Maintenance/Incident=2. Khi thêm migration phải cập nhật kỳ vọng chính xác, không bỏ assertion để test xanh.
- tests/database/Equipment-Behavior.sql chủ yếu test check/FK/rowversion/Inbox/Outbox/idempotency constraint, không phải Retire command/race thật.
- tests/check_backend_workflows.py tạo “retired equipment” bằng UPDATE SQL trực tiếp ở dòng 334–335. Không được lấy case đó làm bằng chứng Retire transaction đã chạy.
- 61/61 của baseline trước là kết quả lịch sử đã báo cáo; Preparation không chạy lại, không suy ra current runtime PASS.
- Không apply hai pending migrations vào app DB trong lượt này. Cần explicit environment/target và phạm vi cho phép khi đến implementation, không khởi chạy API auto-migrate để né kiểm tra.

## 4. Đối chiếu Stage 1 Revision với source

| Yêu cầu | Điểm phù hợp/tái sử dụng | Gap phải triển khai |
|---|---|---|
| RetirementIntent | Equipment owner/row lock, retirement fields và local SQL transaction có | Chưa entity/config/DbSet/store/state machine/epoch/active-intent unique |
| Source receipts | Source names Incident/Maintenance đã có trong registration | Chưa required-source binding, receipt/version/Sealed/CancelAck, durable discovery/checkpoint |
| Immutable WO registration | Unique resource và operation + SourceVersion/RowVersion | Thiếu payload/hash, separate fence, stable snapshot/proof và WO transitions |
| Abort-before-Register | State enum Aborted và Equipment FK có | Chưa tombstone factory, no late-register guard, source CancelIntent/fence proof |
| Final Retire | SQL retirement coherence + terminal status priority | Chưa domain Retire/command/API; không được thực hiện local-only khi thiếu source proof |
| Continue/Cancel intent | Admin JWT và RowVersion pattern hiện hữu | Chưa command idempotency/factory, Cancelling barrier, all-source CancelAck |
| Audit | ActorService column và entity timeline index có | Factory không gán ActorService; thiếu attempt/command linkage, outcome audit và idempotent history |
| Status history | Nullable actor/reason/event và unique SourceEventId có | Cần Retire history factory gán actor/reason; SourceEventId có thể NULL nếu không có event thực |
| Service authentication | Constant-time existing service-key check có; JWT/session RBAC hoạt động theo source | Hiện RPC register vẫn đòi JWT người dùng; cần policy source principal/scopes, không nới quyền user endpoints |
| CQRS/API–Service–Repository | Application abstractions và Infrastructure ownership có | gRPC register đang thao tác EF trực tiếp; protocol mới cần Application handlers/store cụ thể, tránh tăng business logic trực tiếp trong transport |
| C1 | Đúng hướng source→Equipment, Redis→Equipment | Bổ sung retirement poll/report/consumer trong các cạnh hiện có; không thêm Equipment đọc source DB |
| Migration | EF Code First, SQL Server, NoAction, migration ownership có | Additive migration/guards/legacy validation và schema docs/tests đồng bộ |
| Coverage toàn hệ thống | Tìm thấy writer Schedule/Incident rõ ràng | Chưa gate cho bất kỳ source; R4 và tách Incident network transaction là dependency trước enable final Retire |

### Khả thi mà không breaking change?
Có thể xây nền Equipment **theo hướng additive** sau khi được duyệt:
- Giữ service/package v1 và field numbers của mọi message hiện hữu. Add RPC/message mới; không repurpose message Incident thành payload WO hoặc thay ResourceId semantics cũ.
- Thêm nullable/default protocol columns riêng WO, không buộc Incident/Schedule legacy có hash/snapshot ngay.
- Giữ user JWT + source-key requirement của RPC cũ. Scheme service-only mới chỉ áp dụng RPC WO/retirement được cấp scope.
- Trong khi chưa có active intent và chưa bật rollout, client cũ phải tiếp tục hoạt động. Active intent sẽ thêm lỗi gate-closed cho cả register cũ; đây là **thay đổi hành vi có chủ đích theo R1, cần phê duyệt**, không phải hoàn toàn không breaking về nghiệp vụ.
- Không bật Start/Final Retire trước khi source writers/replicas/legacy coverage đã sẵn sàng. Nếu chỉ có Equipment foundation, thiếu source proof phải luôn block.
- Không cần sửa Identity/RBAC implementation để cấp worker JWT giả. Admin tiếp tục dùng JWT/session validation thật; source principal riêng xác thực trực tiếp tại Equipment.

## 5. Thiết kế kỹ thuật cần khóa rõ trước implementation

Không tự sửa Stage 1 trong lượt Preparation. Các chi tiết sau cần hoàn thiện trong sub-stage tương ứng, trong giới hạn invariant Revision 1:

1. Phân biệt version: Begin intent nhận Equipment RowVersion; Continue/Cancel nhận Intent RowVersion; receipt SourceGateVersion, RetirementEpoch, op FencingToken và SourceVersion không dùng thay nhau.
2. Epoch cấp dưới Equipment lock, lấy high-watermark từ lịch sử bền không bị xóa; unique equipment+epoch và active intent. Không cấp epoch bằng timestamp.
3. Required coverage gồm Maintenance(WO+Schedule) và Incident, bất biến theo intent. Thiếu source/coverage/gate proof không được Ready. Không lấy registration hiện có để quyết định source nào cần báo.
4. Service proof là assertion được tin cậy của source đã persist gate. Equipment không thể tự chứng minh “zero source Pending” chỉ bằng DTO số đếm. Test harness báo giả proof chỉ là component test, không chứng nhận source coverage.
5. Poll List có keyset/sequence và wrap/rescan; source giữ các intent đang theo dõi bền và Get terminal theo ID. Không dùng hết trang làm proof không blocker.
6. Register replay đúng identity/hash trả snapshot lần đầu; mutate/insert mới bị gate. Confirm/Abort của operation cũ được giải quyết, không tự tạo reservation mới.
7. Confirm RPC và WorkOrderCreated consumer dùng cùng domain transition và proof binding; Inbox riêng theo envelope; Stage 2 component test chưa thay test Redis/producer integration ở Stage 6.
8. WO operation CancelIntent phải bền ở source và fence mọi final commit trước Abort RPC; Equipment-only fence không chặn được late source commit. Abort gặp missing Equipment không được tạo success giả bỏ FK.
9. Equipment StatusHistory Retire có actor/reason/from/to/time, không dùng IntentId làm SourceEventId của một event không tồn tại. Không thêm Retire Outbox vô chủ.
10. Idempotency của Admin commands cần request hash/key/actor/intent và result/link bền; replay cùng key không tạo epoch mới/reset budget/gửi audit nghiệp vụ trùng. Pending response không được diễn đạt Cancelled/Retired thành công.
11. Model mới thêm bảng/cột phải đồng bộ erd-model.json, Equipment dictionary/SQL assertions và owner-specific technical metadata. Không bỏ schema ownership tests, không mở rộng nhầm tất cả IdempotencyRequests của năm DB.
12. Source Pending legacy chỉ có hash, không có equipment/payload: không thể đảo SHA-256 để backfill. Nếu không có bằng chứng khác thì giữ unknown/block readiness, không coi ResourceId NULL là không có blocker.

## 6. Kế hoạch sub-stage 2.1–2.7

Tất cả dưới đây là **kế hoạch chưa chạy**, chỉ bắt đầu sau xác nhận R1–R4 và cho phép Stage 2.1.

Điều chỉnh thứ tự so với ví dụ: auth WO được đưa vào trước lúc expose RPC WO; migration/schema tests chạy ngay ở 2.1, không chờ 2.7. Stage 2.7 là tổng nghiệm thu/migration safety, không phải lần kiểm thử đầu tiên. Chỉ chuyển sub-stage khi có bằng chứng thực tế của gate trước.

### 2.1 — Schema additive và domain foundation Equipment

**File hiện hữu dự kiến sửa**
- Equipment.Domain/EquipmentUseRegistration.cs, EquipmentAsset.cs, EquipmentStatusHistory.cs.
- Equipment.Infrastructure/Persistence/EquipmentDbContext.cs.
- Equipment.Infrastructure/Persistence/Configurations/EquipmentUseRegistrationConfiguration.cs.
- Equipment.Infrastructure/Persistence/Entities/AuditEntry.cs và configurations liên quan.
Các prefix trên nằm trong src/Services/Equipment/.

**File dự kiến thêm**
- Equipment.Domain/EquipmentRetirementIntent.cs và RetirementSourceReceipt.cs.
- Equipment.Infrastructure/Persistence/Configurations/EquipmentRetirementIntentConfiguration.cs; RetirementSourceReceiptConfiguration.cs.
- EF-generated migration mới và Designer/snapshot trong Equipment.Infrastructure/Persistence/Migrations/; tên/ID chưa sinh.
- tests/MedEquipSLA.Database.IntegrationTests/EquipmentProtocolSchemaTests.cs và EquipmentProtocolMigrationTests.cs.
- Đồng bộ docs/database/erd-model.json, services/Equipment.md, migrations/Equipment.sql, tests/database/Equipment-Schema.sql và SchemaAndMigrationTests.cs khi schema thực sự thay đổi.

**Entity/contract**: registration hash/payload/snapshot/fence/tombstone; intent Coordinating/Ready/Cancelling/Cancelled/Completed; receipt source/coverage/proof/version; Audit service factory; Retire/history domain method không tự gọi từ public API.

**TX/invariant**: schema migration riêng trên catalog fixture; domain mutations không network. NoAction FK local, SQL rowversion, one active intent/equipment, epoch unique, immutable payload, terminal tombstone retention. Domain final Retire chỉ được gọi qua Application kiểm source receipts/local blockers.

**Tests/gate chuyển tiếp**: Release build; model ownership/schema checks; upgrade legacy InitialCreate→new migration, Down mới khi chưa có protocol, reapply; seeded protocol rows làm Down guard reject, dữ liệu vẫn còn. Guard kiểm cả terminal intent/receipt/tombstone. Không sửa InitialCreate. Chưa expose retire endpoints hoặc bật workers.
**PASS gate** chỉ khi các kiểm tra này thực sự chạy đạt trên SQL Server, không skipped.

### 2.2 — RegisterWorkOrder/Query và service-only authorization

**File dự kiến**
- Sửa Contracts.Grpc/Protos/equipment.proto, Equipment.Api/Grpc/EquipmentGrpcService.cs, Equipment.Api/Program.cs, Infrastructure/DependencyInjection.cs.
- Thêm Equipment.Application/Features/WorkOrderRegistration/ contracts, command/query handlers và store abstraction.
- Thêm Equipment.Infrastructure/Persistence/WorkOrderRegistrationStore.cs.
- Thêm Equipment.Api/Authentication/EquipmentServiceAuthentication.cs hoặc tương đương, scopes/policies chỉ tại Equipment.
- .env.example/compose.yaml chỉ thêm placeholder; không giá trị secret thật.
- Tests: EquipmentWorkOrderGrpcTests.cs và EquipmentGrpcTestHost.cs.

**DTO**: RegisterWorkOrder payload versioned/canonical hash/fence; Query theo stable operation; source-scoped principal; response bound snapshot/hash/registration state. Giữ proto fields/RPC cũ.

**TX/invariant**: RPC transport → handler → store; local TX khóa Equipment rồi registration, validate retirement gate adapter trước insert. Network của caller ở ngoài SQL. Same op+payload replay; different hash/resource conflict; Broken accepted, Retired reject, snapshot bền lần đầu.

**Tests/gate**: real Kestrel HTTP/2 Equipment gRPC + SQL; operational/broken/retired/missing; duplicate sequential/concurrent, hash/resource mismatch, snapshot sau transfer; missing/wrong service key và Admin JWT-only bị từ chối; RPC cũ vẫn require JWT/scope đúng.
**PASS gate** khi SQL + real gRPC/auth tests chạy đạt và compile mọi contract caller; không dùng fake caller để nói auth PASS.

### 2.3 — Confirm, Abort tombstone và fencing

**File dự kiến**
- Mở rộng proto/handlers/store/domain registration từ 2.2; thêm ConfirmWorkOrderCommitted/VerifyAndAbort handlers.
- Thêm proof verification/value types dùng chung cho transition RPC và event sau này.
- Equipment Audit service factory từ 2.1 dùng cho attempt/result/stale audit.
- Tests: EquipmentWorkOrderFencingTests.cs, EquipmentWorkOrderAbortTests.cs.

**Contract**: exact equipment/operation/occurrence/WO/registration/hash binding; monotonic op fence; durable source CancelIntent assertion. SourceVersion độc lập fence; không thêm Release RPC.

**TX/invariant**: Equipment→registration locks; absent→Aborted tombstone được lưu bền dưới lock, kể cả intent đóng; không reservation mới. Confirm Pending→Active sau source commit proof; terminal guards; replay/old response không ghi đè new result. Không gọi Maintenance để verify bên trong TX.

**Tests/gate**: Abort-before-Register; delayed Register; repeated Abort; stale/lower fence; duplicate Confirm; mismatched proof; Active/Committed conflict không bị Cancel hồi tố; response mất sau remote commit + query/restart same op. SQL save failure không lưu Audit/proof nửa chừng.
**PASS gate** chỉ cho Equipment behavior đã test. Chưa kết luận source late-commit fencing hoặc Redis event consumer end-to-end đã đạt; chúng còn cần source implementation và Stage 6.

### 2.4 — RetirementIntent/receipts persistence và admission gate

**File dự kiến**
- Equipment.Application/Features/Retirement/ model contracts, IRetirementCoordinationStore và commands nội bộ.
- Equipment.Infrastructure/Persistence/RetirementCoordinationStore.cs.
- Reuse intent/receipt domain/config từ 2.1; sửa shared gate check được gọi bởi cả RegisterUseAsync hiện tại và WO store.
- Tests: EquipmentRetirementIntentTests.cs, EquipmentRetirementAdmissionTests.cs.

**DTO/model**: stable IntentId/epoch/request hash, required-source coverage, progress/error/lease/attempt metadata, proof/version binding và CancelAck receipt. Chưa public Admin routes ở sub-stage này.

**TX/invariant**: EquipmentAsset→Intent→Receipts theo source→Registration/projection. Một active intent, epoch không reuse. Không có receipt ≠ source clear. Gate kiểm trong cùng TX trước insert mới cho cả WO/Schedule/Incident; replay hợp lệ không tạo reservation mới.

**Tests/gate**: Begin intent concurrent; mọi active phase chặn insert mới; existing registration replay; required Maintenance+Incident, wrong/missing coverage; proof cũ/duplicate không overwrite; SQL rollback giữ tính atomic; gate không đổi Broken/OperationalStatus.
**PASS gate** khi SQL admission matrix đạt cho tất cả ba loại, gồm real RPC cũ. Không nói source gate đã cover vì mới có Equipment gate.

### 2.5 — Retirement gRPC/auth/poll/report protocol

**File dự kiến**
- equipment.proto + Equipment.Api/Grpc/EquipmentGrpcService.cs hoặc endpoint adapter mới có cùng service.
- Retirement Application contracts/handlers, RetirementCoordinationStore, Equipment-only auth scopes từ 2.2.
- Tests: EquipmentRetirementGrpcTests.cs, EquipmentRetirementReceiptTests.cs.
- Tài liệu API/gRPC/README Equipment khi contract được implement.

**RPC**: ListRetirementIntents, GetRetirementIntent, ReportRetirementSourceState, AcknowledgeRetirementCancellation. Query terminal; bounded keyset + rescan, exact intent/equipment/epoch/source binding, stale report handling.

**TX/invariant**: List/Get đọc E local; Report/CancelAck ghi E local TX, không gọi nguồn. Credential chỉ báo coverage của chính source, không cho worker create/Continue/Cancel/Retire. Receipt bền trước reply; CancelAck chỉ cho Cancelling/Cancelled đúng epoch.

**Tests/gate**: malformed/unauthorized/other-source report; missing receipt không Ready; stale epoch/version; duplicate proof/Ack; report response timeout/restart; discovery nhiều trang và terminal query; CancelAck không tự mở admission khi thiếu nguồn.
**PASS gate** khi real gRPC + SQL tests đạt. Report proof từ harness chứng minh contract handling tại E, không chứng minh source worker thật đã freeze/seal.

### 2.6 — Admin Begin/Continue/Cancel và final Retire transaction

**File dự kiến**
- Equipment.Api/RetirementEndpoints.cs, Program.cs.
- Equipment.Application/Features/Retirement/ Begin/Continue/Cancel/Get handlers và validation.
- RetirementCoordinationStore, Equipment IdempotencyRequest factory/commands, Audit/history factory.
- Equipment.Infrastructure/Workers/RetirementCoordinator.cs hoặc equivalent, options/TimeProvider/DI.
- Tests: EquipmentRetirementCommandTests.cs; EquipmentRetireConcurrencyTests.cs; mở rộng tests/check_backend_workflows.py khi được phép triển khai.

**DTO**: reason + appropriate RowVersion + idempotency key; intent status/progress/link; original equipment version ở Begin và intent version ở Continue/Cancel, không lẫn nhau.

**TX/invariant**:
- E1 create intent/Audit và gate → trả 202.
- E4 Cancelling quyết định bền tranh Equipment lock với E3 final.
- E5 Cancelled/mở admission chỉ sau all-source CancelAck.
- E3 final cần current intent+epoch, durable required proofs, no R Pending/Active mọi loại, no blocking/unknown legacy; Retire + Completed + Audit/history một TX.
- Continue chỉ retry cùng intent, không bỏ proof, không tự Abort/Release. No network/Redis trong SQL TX.
- Chưa triển khai source coverage thì final Retire phải bị chặn. Không “bật tạm” bằng fake proof hoặc omit Incident để demo.

**Tests/gate**: Admin vs Staff/Technician/service identity; reason/version/key validation và replay; Begin không báo Retired; Cancel-vs-final hai commit orders; stale Cancel/Prepare epoch; all-Ack barrier; uncertain response→GET; no automatic TTL/open; history actor/reason giữ đủ. Retire/Register race cần cả Equipment lock orders với command thật.
**PASS gate** ở mức Equipment component chỉ khi các test tương ứng đạt. Gate enable toàn hệ thống cần dependency source thật ở §7; thiếu nó không được nghiệm thu final Retire end-to-end.

### 2.7 — Migration safety, regression và báo cáo nghiệm thu

**File dự kiến**
- Các test mới từ 2.1–2.6; SchemaAndMigrationTests, EfPersistenceTests, SQL Equipment assertions theo schema mới.
- tests/check_backend_workflows.py và real gRPC host harness; không chuyển sang InMemory.
- docs/DATABASE-DOCUMENTATION.md, docs/API-DOCUMENTATION.md, Equipment README, migration SQL, PRN232 checklist cập nhật đúng evidence.
- Stage 2 sub-stage reports theo phạm vi thực tế, không đổi planned thành implemented chỉ vì tạo class.

**TX/invariant**: Không thêm protocol mới; xác minh upgrade/rollback/reapply trên fixture legacy đã có dữ liệu, guard khi protocol đã dùng; không reset ứng dụng, không downgrade InitialCreate hoặc xóa tombstone để qua guard.

**Tests/gate**: Release build, toàn DB integration suite; Module 1/2/Notification Inbox/Identity recipient/Gateway/Equipment regression; real Redis khi test consumer/outbox liên quan đã có implementation; contract compatibility; secrets/config checks và complete diff. Test không chạy được phải NOT TESTED/BLOCKED, không PASS. App DB migration chỉ apply khi đã được phép đúng catalog, báo riêng fixture.

**PASS gate**: chỉ các evidence đã chạy và đủ scope. Equipment có thể hoàn tất phần của mình nhưng **không tuyên bố toàn D3/Retire an toàn** nếu source gates hoặc WO generation/consumer còn chưa implement. Nếu có blocker thì báo đúng, không tự chuyển sang full Maintenance generation/Frontend.

## 7. Dependency R4 trước khi bật Retire toàn hệ thống

Đây là phạm vi cần chủ dự án duyệt, không được Preparation tự triển khai:
- Maintenance: thêm EquipmentRetirementGates/model/migration + source worker poll/report; ScheduleCommands tạo Pending và reactivation gate-first; giữ Planner/Reminder algorithm. WO generation về sau bắt buộc cùng gate.
- Incident: thêm EquipmentRetirementGates/equipment-linked Pending metadata + migration/source worker; IncidentCreationEndpoints tách TX/network/TX. sp_getapplock hiện tại không thay source retirement gate, vì Pending đã được lưu trước nó và freeze chưa dùng cùng lock.
- Source side cancellation phải ghi terminal epoch tombstone trước CancelAck, bao gồm intent bị hủy trước khi source từng Frozen. Old worker/response không được mở gate mới.
- Deployment inventory: mọi replica/writer cùng version hỗ trợ gate, legacy metadata đã xác minh; không chỉ một worker advertise coverage. Không dựa một query zero Pending hoặc timestamps.
- Source proof giao thức phải được test với SQL thực của nguồn, không dùng Equipment-only receipt seed làm nghiệm thu an toàn.

Sau khi được duyệt, các thay đổi source này có thể được chia gói phụ thuộc riêng và phối hợp với 2.4–2.6, nhưng không tự đổi quyền/lifecycle Incident hoặc Schedule để làm thanh lý chạy được.

## 8. Risk register và cách chứng minh

| Risk | Tình trạng source / hướng xử lý trong kế hoạch | Evidence bắt buộc trước kết luận |
|---|---|---|
| Register vs Retire | Có Equipment lock cho Register, chưa có Retire/intents | Hai lock/commit orders thực, final proofs + zero local blockers |
| Source Pending chưa tới Equipment | Pending hiện không gate/link đủ | A/B source gate races; R4 coverage đầy đủ; Equipment absent không đồng nghĩa resolved |
| Timeout sau remote commit | RPC hiện có có thể để remote Pending và source Pending | Fault boundary real gRPC, query stable op, no duplicate snapshots/rows |
| Abort-before-Register | Chỉ enum Aborted, không tombstone behavior | Persist tombstone trước late Register; duplicate Abort và missing Equipment |
| Stale FencingToken | Chưa có cột/behavior riêng | Reclaim/late write tests, no SourceVersion misuse; source final commit cũng fenced |
| Duplicate RPC/resource | Unique indexes có nhưng payload/hash chưa có | Concurrent duplicate same/different hash/resource + no duplicate Audit business result |
| Cancel vs final Retire | Chưa có command/state machine | E4/E3 race cả hai thứ tự, all-source cancel barrier |
| Delayed Prepare/Cancel epoch cũ | Chưa có source terminal epoch/gate | Source old message tests, không unfreeze/refreeze intent mới |
| Thiếu source receipt hoặc giả source proof | Chưa có receipt/required-source/auth scopes | Missing source, coverage mismatch, other-source credential, empty-count assertion bị từ chối |
| Old replica bỏ qua gate | Không thể giải quyết chỉ bằng thêm E entity | Rollout inventory + gated writer tests; final Retire disabled cho tới coverage verified |
| Snapshot thay đổi khi replay | Register hiện đọc asset/department hiện thời | WO snapshot bền lần đầu, transfer/rename rồi retry giữ nguyên |
| Audit factory/LastError | Có cột service nhưng thiếu factory/history attempt | ActorService đúng và per-attempt Started/Unknown/Result, reason/CommandId đủ |
| Migration Down khi protocol data có | InitialCreate Down không phải guard protocol mới | Guard additive migration kiểm cả terminal data, rejects trước drop, old data còn |
| Unknown legacy Pending | Không có equipment/body đủ | Không đảo hash/backfill đoán; source không Seal khi unknown |
| Network trong Incident SQL TX | Finding hiện tại, không phù hợp yêu cầu Stage 2/R4 | Refactor có phạm vi duyệt + test không giữ TX khi RPC, bảo toàn duplicate/SLA |
| Enable “Equipment only” | Có thể build component nhưng không đủ proof source | Không có Ready/Retired thành công khi nguồn chưa hỗ trợ, end-to-end mới là gate enable |

Không risk nào ở bảng được đánh dấu runtime PASS trong Preparation.

## 9. Test matrix cho implementation

Tất cả ca dưới đây hiện **NOT TESTED trong lượt này**; tên ID chỉ là kế hoạch, không phải test methods đã tạo.

| Group | Case/assertion | Sub-stage |
|---|---|---|
| DB-1 | Equipment model/ERD/index/check/rowversion/NoAction ownership | 2.1 |
| MIG-1..4 | Legacy Up→Down mới→reapply; guard active và terminal intent/receipt/tombstone/proof; giữ dữ liệu sau rollback bị chặn | 2.1, 2.7 |
| WO-1..6 | Operational/Broken/Retired/missing; duplicate op/resource/hash; snapshot immutable | 2.2 |
| AUTH-1..5 | Missing/wrong key, Admin JWT-only, wrong source, scope escalation tới Retire/Cancel | 2.2, 2.5, 2.6 |
| FENCE-1..5 | Higher/lower/equal fence, claim reclaim/late response; SourceVersion không bị reset | 2.3 + source dependencies |
| ABORT-1..5 | Abort-before-Register, duplicate, tombstone, missing equipment, late Created/confirm conflict | 2.3 |
| INT-1..5 | One active intent/epoch unique, required-source set, gate cả 3 resource, replay vs insert mới, trạng thái vận hành giữ nguyên | 2.4 |
| REC-1..6 | Sealed/Frozen/Blocked, missing source/coverage, duplicate/stale reports, paging/Get terminal, response loss/restart | 2.5 |
| RET-1..6 | Begin 202, no false success; Continue unchanged intent; Cancel all-Acks; final local blockers/unknown; actor/reason/history | 2.6 |
| RACE-A/B | Pending source trước intent; intent trước Pending, freeze thắng/thua source lock | Source dependency + 2.7 |
| RACE-C/D/E | Register/intents both lock orders; deadline sau remote commit; local rollback/commit | 2.2–2.6, source integration |
| RACE-F/G/H | CancelRequested chưa M Aborted; restart durable barriers; List/Get/report/Ack timeout | 2.3–2.6, source integration |
| EPOCH-1..4 | Cancel-vs-final hai thứ tự, old Prepare sau CancelAck, old Cancel sau epoch mới, source mở gate nhưng Ack mất | 2.5–2.7 + source |
| AUD-1..4 | Started/Result/Unknown, stale attempt append, Admin retry/cancel reason, duplicate command không nhân Audit nghiệp vụ | 2.3–2.7 |
| EVT-1..4 | Confirm RPC trước/sau Created consumer, duplicate/stale event, R+Inbox atomic, ACK sau commit | Contract/domain ở Stage 2; real producer/consumer ở Stage 6 |
| REG-ALL | Module 1/2, Schedule Gateway, Identity recipient, Notification Inbox, Equipment API, SQL suite; Redis thật khi integration có | 2.7 và Stage 6/7 |
| ROLLOUT | Legacy rows + old writer/replica inventory; cannot enable final Retire khi coverage thiếu | Trước enable end-to-end |

Không dùng SQL UPDATE IsRetired đơn thuần làm test Retire command. Không dùng test double/in-memory để chứng minh SQL lock hoặc gRPC auth. Equipment component test có source proof harness được phép để kiểm parsing/state machine, nhưng phải ghi đúng giới hạn, không thay source gate tests thật.

## 10. Kiểm tra đã làm và chưa làm

Đã thực hiện:
- Đọc đầy đủ ba tài liệu bắt buộc; đọc source/config/migration/tests liên quan và tìm writers qua rg.
- git branch/status/HEAD read-only, fingerprint file hiện hữu để kiểm phạm vi thay đổi.
- Read-only SQL metadata xác nhận history hiện tại cả năm app DB, không chỉ lặp lại báo cáo cũ.
- Chuẩn bị kế hoạch với file/contract, TX, invariant, test và gate chuyển tiếp cho từng sub-stage.

Chưa chạy: dotnet restore/build/test, EF model runtime check, migration add/script/apply/down/reapply, real gRPC host/Redis tests, Docker Compose hoặc deployment. Tất cả runtime verification mới là NOT TESTED; không sử dụng 61/61 lịch sử làm PASS Preparation/Stage 2.

Đã kiểm cấu trúc, đủ 7 sub-stage, local links và whitespace của báo cáo. Đối chiếu SHA256: 459 file hiện hữu không đổi, chỉ thêm đúng file Preparation; branch/HEAD giữ nguyên. Các kiểm tra tài liệu không phải kiểm thử protocol.

## 11. Những quyết định cần chủ dự án duyệt

Theo yêu cầu Stage 2 Preparation, **không tự chọn hoặc coi việc gửi task này là xác nhận R1–R4**:

| Quyết định | Nội dung cần xác nhận |
|---|---|
| R1 | Intent Coordinating/Ready/Cancelling chặn registration mới của WO/Schedule/Incident; source Frozen chặn Pending/mutation tạo blocker gồm reactivation; no TTL; giữ OperationalStatus |
| R2 | Chỉ Admin Continue/Cancel, reason+RowVersion+idempotency; Cancelling bền trước; Cancelled sau all-source CancelAck; không tự Abort/Release resource |
| R3 | Pending cũ chưa Register vẫn blocker, không tạo reservation xuyên intent; có thể hủy intent để tiếp tục; Option A WO không tự mở quyền Cancel Incident/Schedule |
| R4 | Gate phải bao phủ đầy đủ WO, Schedule và Incident, kể cả source prepare/finalize/reactivation và old replica rollout; đồng ý phạm vi thay đổi nguồn phụ thuộc trước enable Retire |

Cần chủ dự án review báo cáo, xác nhận từng R1–R4 và cho phép bắt đầu **Stage 2.1**. Chưa cần cung cấp thêm secret, reset DB hoặc quyền push để hoàn thành Preparation.

Các source/design gaps ở §4–§8 là công việc/phụ thuộc phải giải quyết theo kế hoạch sau approval. Chúng không được che bằng local-only Retire hoặc receipt giả. Nếu R4 không được duyệt thì không có phương án enable final Retire an toàn chỉ với Equipment hiện hữu.

## 12. Kết luận

Preparation đã hoàn tất ở mức audit/kế hoạch. Nền SQL/EF/row lock/unique/rowversion hiện tại có thể tái sử dụng để triển khai Equipment theo các bước nhỏ, nhưng **chưa có quyền bắt đầu code** và chưa có runtime evidence cho protocol mới.

Dừng tại đây, chờ chủ dự án review, xác nhận R1–R4 và cho phép Stage 2.1. Không sửa code/migration/database hoặc commit/push trong lượt này.

**BLOCKED — NEED BUSINESS APPROVAL**
