# Module 3 — Stage 2.1 Report: Equipment Schema & Domain Foundation

Ngày 06/11/2026. Phạm vi chỉ Stage 2.1. Không bắt đầu Stage 2.2.

## 1. Approved R1–R4

Nguồn phê duyệt: yêu cầu người dùng “MODULE 3 / STAGE 2.1 — Equipment Schema & Domain Foundation” đã ghi rõ R1–R4 APPROVED, không additional notes. Đã ghi đúng vào [WORKORDER-MODULE3-APPROVALS.md](WORKORDER-MODULE3-APPROVALS.md).
Các tài liệu Stage 1/Preparation là hồ sơ lịch sử trước approval; báo cáo này không sửa lại lịch sử đó.

## 2. Pre-flight và bảo toàn thay đổi hiện hữu

| Nội dung | Quan sát thực tế trước sửa |
|---|---|
| Repository | D:\PRN232\ASS\MedEquipSLA; dùng nguyên checkout |
| Branch | feature/database-code-first |
| HEAD | ddd7507b5afd94c3e8eea5f979c46f911741f421 |
| Tracked modified, unstaged | 98 |
| Untracked files (không gộp folder) | 87 |
| Staged | 0 |
| So với Preparation | Thêm WORKORDER-MODULE3-STAGE2-PREPARATION.md; không giả định các số lịch sử vẫn đúng |
| Fingerprint | SHA256 460 file hiện hữu để đối chiếu cuối lượt |

Đã đọc toàn bộ các file hiện hữu cần chỉnh: EquipmentAsset, EquipmentUseRegistration, EquipmentStatusHistory, Equipment AuditEntry, DbContext, registration configuration, SchemaAndMigrationTests; đọc TestDatabase và migration/snapshot. Domain methods từ các lượt trước như Transfer/Register/UpdateProfile/ApplyBlockingStatus/ApplyIncidentVersion và Admin Audit constructor được giữ nguyên, chỉ thêm foundation cần thiết.

Không reset/checkout/clean/stash, không xóa untracked file, không format toàn repo, không commit/push. Git diff so với HEAD còn chứa rất nhiều thay đổi **trước Stage 2.1**; danh sách dưới đây dựa trên fingerprint pre-flight, không gộp tất cả dirty files thành thay đổi Stage 2.1.

## 3. Files thêm/sửa trong Stage 2.1

### File hiện hữu được chỉnh

- docs/database/erd-model.json
- docs/database/migrations/Equipment.sql
- docs/database/services/Equipment.md
- docs/database/services/Equipment.mmd
- src/Services/Equipment/Equipment.Domain/EquipmentAsset.cs
- src/Services/Equipment/Equipment.Domain/EquipmentStatusHistory.cs
- src/Services/Equipment/Equipment.Domain/EquipmentUseRegistration.cs
- src/Services/Equipment/Equipment.Infrastructure/Persistence/Configurations/EquipmentUseRegistrationConfiguration.cs
- src/Services/Equipment/Equipment.Infrastructure/Persistence/Entities/AuditEntry.cs
- src/Services/Equipment/Equipment.Infrastructure/Persistence/EquipmentDbContext.cs
- src/Services/Equipment/Equipment.Infrastructure/Persistence/Migrations/EquipmentDbContextModelSnapshot.cs
- tests/MedEquipSLA.Database.IntegrationTests/SchemaAndMigrationTests.cs
- tests/database/Equipment-Schema.sql

Các file đã dirty từ trước như EquipmentAsset, EquipmentUseRegistration, EquipmentStatusHistory, AuditEntry, SchemaAndMigrationTests và erd-model.json được bổ sung có mục tiêu, không thay implementation cũ bằng skeleton.

### File thêm

- docs/database/migrations/Equipment-Stage2.1-Down.sql
- docs/development/WORKORDER-MODULE3-APPROVALS.md
- docs/development/WORKORDER-MODULE3-STAGE2.1-REPORT.md
- src/Services/Equipment/Equipment.Domain/EquipmentRetirementIntent.cs
- src/Services/Equipment/Equipment.Domain/Properties/AssemblyInfo.cs
- src/Services/Equipment/Equipment.Domain/RetirementSourceReceipt.cs
- src/Services/Equipment/Equipment.Domain/WorkOrderRegistrationRequest.cs
- src/Services/Equipment/Equipment.Infrastructure/Persistence/Configurations/EquipmentRetirementIntentConfiguration.cs
- src/Services/Equipment/Equipment.Infrastructure/Persistence/Configurations/RetirementSourceReceiptConfiguration.cs
- src/Services/Equipment/Equipment.Infrastructure/Persistence/Migrations/20261106063402_AddEquipmentRetirementFoundation.Designer.cs
- src/Services/Equipment/Equipment.Infrastructure/Persistence/Migrations/20261106063402_AddEquipmentRetirementFoundation.cs
- tests/MedEquipSLA.Database.IntegrationTests/EquipmentRetirementDomainTests.cs
- tests/MedEquipSLA.Database.IntegrationTests/EquipmentRetirementSqlTests.cs

Helper one-off trong .tools/stage21-sync.ps1 và TRX/build artifacts nằm trong thư mục ignored, không phải application code hoặc repo clone mới. Helper dùng đọc generated model để đồng bộ tài liệu; không chạy SQL hoặc app. Không dùng helper đó làm test evidence.

## 4. Domain design đã triển khai

### EquipmentRetirementIntent
- States Coordinating/Ready/Cancelling/Cancelled/Completed; không TTL hoặc timeout transition.
- Giữ EquipmentId/RetirementEpoch, Actor/Reason, RequestKey/hash, required source coverage, UTC timestamps, RowVersion, Continue metadata và cancellation/completion history.
- Internal Begin tính epoch=max retained history+1, từ chối history có active intent, Completed hoặc sai thiết bị. **Caller Application phải load đầy đủ history và gọi dưới Equipment SQL row lock**; foundation không có production allocator/store/handler. SQL unique indexes bảo vệ cả stale/racing view.
- Continue giữ nguyên ID/epoch và phase, kể cả retry coordination Cancelling; không mở admission.
- RequestCancel chỉ ghi Cancelling; ConfirmCancelled cần đủ Maintenance/Incident CancelAck; giữ history.
- Ready chỉ khi hai nguồn có Sealed proof hợp lệ. Missing/Blocked/Frozen không được coi sạch.
- Completed/Cancelled terminal, stale/conflicting reports không overwrite proof.

### RetirementSourceReceipt
- PK IntentId+SourceService; CoverageVersion=1 và epoch/gate version dương.
- Maintenance v1 bao phủ WO+Schedule; Incident v1 bao phủ Incident. Required coverage ghi bền trong intent.
- Composite FK (IntentId,EquipmentId,RetirementEpoch) → intent alternate key cùng tuple, NoAction.
- RecordSource kiểm authenticated-source binding, equipment/intent/epoch/coverage, proof/state/version. Đây là **domain validation trên identity được Application cung cấp**, chưa có gRPC authentication thật ở Stage 2.1.
- Frozen/Blocked/Sealed/CancelAck. Khi CancelAck sau Sealed, giữ ReadyProofId cũ và lưu CancelAcknowledgementId riêng. Exact duplicate là no-op, stale/contradictory report bị từ chối.
- SQL RowVersion và proof-presence checks.

### Final Retire foundation
EquipmentAsset.Retire là internal, không public cho API/generic repository. Chỉ Equipment.Application và test assembly được friend access; Infrastructure/API không được cấp quyền này.
VerifiedRetirementEligibility nội bộ yêu cầu intent Ready + đủ Sealed sources + Application xác nhận coordination/local blockers/legacy đã kiểm tra; zero local registration count một mình không đủ.
Retire recheck binding/state, chuyển intent Completed và Equipment Retired, ghi actor/reason/time/version. Application ở stage sau phải bao transaction/authorization/Audit/history và source proof verification thật. Chưa có API hoặc flow production gọi transition này.
History factory lưu actor/reason/from/to/time; SourceEventId=NULL, không giả integration event.

### EquipmentUseRegistration foundation
Bổ sung đúng mapping tên yêu cầu:
RequestHash, RequestPayloadJson, FencingToken, CancelIntentId, EquipmentCodeSnapshot, EquipmentNameSnapshot, DepartmentIdSnapshot, DepartmentNameSnapshot.
- RegisterWorkOrder là **domain factory**, không RPC: dùng typed WorkOrderRegistrationRequest với version=1, UUID/UTC serialization cố định và SHA256 tự tính 32 bytes.
- Stable resource=OccurrenceId, operation riêng; snapshot chụp lần đầu. MatchesWorkOrder so payload/hash/binding, không refresh snapshot từ asset hiện tại.
- Hash trả defensive copy; caller không sửa được hash lưu bằng cách mutate array getter.
- SourceVersion khác FencingToken. Không thêm Confirm/Abort workflow; Aborted vẫn không được coi là đã triển khai protocol chỉ vì có enum/cột.
- Legacy Incident/Schedule factory và ApplyIncidentVersion giữ nguyên.
- Factory không thay OperationalStatus; Broken không thành UnderMaintenance và Retired bị từ chối đăng ký WO mới.

### Audit
ForService tạo ActorService thuộc allowlist Equipment/Maintenance/Incident với ActorUserId=NULL; Admin constructor cũ giữ ActorUserId và ActorService=NULL.
Không có secret/token input trong service identity; tests chỉ truyền domain change data, không credential. Caller ở stage sau vẫn phải bảo đảm ChangesJson không chứa credential. Chưa xây attempt workflow.

## 5. Database schema thực tế đã sinh

Migration **20261106063402_AddEquipmentRetirementFoundation**:
- Thêm 2 bảng EquipmentRetirementIntents và RetirementSourceReceipts.
- Thêm 8 cột registration: 7 nullable; FencingToken bigint NOT NULL default 0.
- Tổng 40 cột mới (intent 21, receipt 11, registration 8).
- Giữ unique registration source/type/resource và source/operation.
- Unique EquipmentId+RetirementEpoch; filtered unique active EquipmentId với State <> Cancelled AND State <> Completed.
- Request key unique theo actor; receipt PK một source/intent, nghiêm hơn duplicate theo source+intent+epoch+coverage khi coverage/epoch đã bị bind.
- FK local NoAction; không FK/navigation xuyên database.
- State/epoch/count/required coverage/proof/JSON/UTC/retirement timestamp coherence checks và rowversion.
- Registration RequestHash binary(32); width lưu 32 bytes nhưng SQL binary có thể pad literal ngắn. Vì vậy domain tự tính SHA256; không coi column width là chứng minh dữ liệu đầu vào là hash đúng.
- Legacy columns không bị UPDATE/backfill suy đoán. Old registration shape NULL/default0 tiếp tục hợp lệ.

Generated SQL:
- [Equipment.sql](../database/migrations/Equipment.sql): idempotent Up từ migration assembly hiện tại.
- [Equipment-Stage2.1-Down.sql](../database/migrations/Equipment-Stage2.1-Down.sql): review Down riêng migration mới, guard đứng trước mọi DROP. Chỉ sinh script, không execute lên app DB.

ERD/dictionary đồng bộ từ generated model và được exact model/schema tests kiểm lại. Các schema JSON ngoài Equipment được giữ nguyên về nội dung. Equipment.mmd/diagram trong dictionary có composite binding và optional FKs đúng; PNG v0.1 được ghi rõ là historical, không giả là export schema mới.

## 6. Migration safety

Không sửa InitialCreate, InitialCreate.Designer hoặc migration lịch sử của bất kỳ service nào. Equipment snapshot hiện tại được cập nhật đúng migration mới.

Down dùng THROW 51021 trước DDL nếu có:
- Bất kỳ RetirementIntent hoặc receipt, gồm Cancelled/Completed, Sealed/CancelAck.
- Registration protocol payload/hash/fence/cancel intent/snapshot.
- WorkOrder Aborted tombstone, kể cả terminal state.

Tests chứng minh:
1. Apply legacy InitialCreate trên test catalog.
2. Seed legacy records bằng fixture old-schema, lưu snapshot gồm RowVersion.
3. Upgrade, kiểm nullable/default mới và dữ liệu cũ không đổi.
4. Down về InitialCreate khi chưa có protocol data, dữ liệu legacy giữ nguyên.
5. Reapply, migration history chính xác 2.
6. Seed từng loại protocol data, Down bị từ chối đúng mã 51021.
7. Migration history, row counts và toàn bộ protocol-row JSON trước/sau failure giữ nguyên.

Không xóa dữ liệu để vượt guard. Không rollback app DB hoặc reset database.

## 7. Tests và kết quả thực tế

Bằng chứng cuối: tests/MedEquipSLA.Database.IntegrationTests/TestResults/stage2.1-acceptance.trx
SHA256: B779C30B52F8D4287A18935C550495327BD17C147E760820CCF8D556C32DBBDF.

| Nhóm | Số test cuối | Kết quả |
|---|---:|---|
| EquipmentRetirementDomainTests | 6 | PASS |
| EquipmentRetirementSqlTests | 11 | PASS — SQL Server LocalDB |
| SchemaAndMigrationTests, 5 model + 5 SQL fixture owners | 10 | PASS |
| EquipmentOutboxTests | 1 | PASS — existing retry/EventId regression, publisher fault stub |
| EfPersistenceTests | 3 | PASS — Equipment concurrency/local rollback regression |
| Tổng | **31** | **31 PASS, 0 FAIL, 0 skipped**, 18 giây |

Coverage gồm valid/invalid/terminal intent transitions, active/history/epoch guard, continue identity, wrong source/equipment/epoch/coverage/proof, missing receipts, snapshot immutable sau rename/transfer, legacy Incident/Schedule semantics, Admin/service audit actors; SQL unique active/epoch/receipt, NoAction FK, rowversion changes, hash column metadata, invalid proof/JSON/fence và migration safety.

Test hai allocator dùng cùng Equipment UPDLOCK,HOLDLOCK trên SQL thật chứng minh pattern serialization, không dùng InMemory hoặc Task.Delay. Production allocator chưa được implement.

SchemaAndMigrationTests vẫn assert **Equipment migrations=2**, Incident/Maintenance=2, các owner khác=1. Không bỏ count assertion. Bổ sung kiểm tra riêng alternate unique keys: SQL unique constraints không phải EF indexes; cả hai nhóm đều được kiểm exact count/names-or-column binding thay vì bỏ chúng khỏi verification.

Lượt rộng đầu có 2 FAIL vì metadata generator ghi SourceService PK của receipt nullable=true. Đã sửa **tài liệu và SQL expected nullable**, không sửa SQL schema thành nullable hoặc nới assertion. Lượt cuối 31/31 PASS.
Build test ban đầu gặp analyzer xUnit2031 và được sửa bằng Assert.Single overload đúng trước các lượt acceptance.

Lệnh cuối tại repository:
```powershell
& ./.tools/dotnet/dotnet.exe build MedEquipSLA.sln --no-restore -c Release -m:4 --nologo -v:q
$env:SQLSERVER_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
& ./.tools/dotnet/dotnet.exe test tests/MedEquipSLA.Database.IntegrationTests --no-build -c Release --nologo --filter 'FullyQualifiedName~EquipmentRetirement|FullyQualifiedName~SchemaAndMigrationTests|FullyQualifiedName~EquipmentOutboxTests|FullyQualifiedName~EfPersistenceTests' --logger 'trx;LogFileName=stage2.1-acceptance.trx'
```

## 8. Build results

Release build toàn solution sau thay đổi cuối code/tests: **PASS, exit 0, 0 warnings, 0 errors**, 4.98 giây. Vì build toàn solution, các service/project khác vẫn compile với proto/contract hiện tại; không đổi package, Identity/RBAC hoặc frontend.
EF tool restore dùng version đã pin 10.0.13; migration add và Up/Down script generation exit 0, không database update.

Không chạy API/gRPC host, Docker, Redis WO consumer, full Gateway smoke hoặc source reconciliation. Những phần đó không thuộc Stage 2.1 và không được ghi PASS từ tests foundation.

## 9. Database safety confirmation

Đã đọc toàn bộ TestDatabase trước chạy: base connection chỉ master/omitted catalog; allowlist LocalDB/loopback14333; tạo tên MedEquip_<Owner>_Test_<GUID> bằng CREATE; chỉ đánh dấu ownership sau CREATE thành công; cleanup kiểm exact owned name/regex và chỉ DROP catalog do fixture tạo.

Connection khi chạy tests được set explicit master Integrated Security loopback LocalDB. Không gọi Update-Databases, database update hoặc start API auto-migrate. Migration design-time/script dùng tên MedEquip_Equipment_Test_DesignOnly; chúng không mở kết nối để apply.
SchemaAndMigrationTests dùng 5 catalog **test** theo owner, không phải 5 application catalogs.

Read-only metadata trước/sau cho kết quả giống hệt:
- MedEquip_Identity: chỉ 20261023044348_InitialCreate.
- MedEquip_Equipment: chỉ 20261023044354_InitialCreate.
- MedEquip_Maintenance: chỉ 20261023044400_InitialCreate.
- MedEquip_Incident: chỉ 20261023044406_InitialCreate.
- MedEquip_Notification: chỉ 20261023044411_InitialCreate.
- sys.columns tổng theo từng app DB không đổi; không coi số cột này là số cột nghiệp vụ.

Không có command ghi dữ liệu/schema trên năm app DB. Đây là xác nhận dựa trên target/harness/command audit và metadata before/after, không phải full content backup/diff của dữ liệu ứng dụng. Fixture đã kiểm schema mới không đồng nghĩa application DB đã apply migration.

## 10. Git diff, preservation và secrets

So fingerprint pre-flight trước khi thêm report: 13 existing files thay đổi đúng scope và 12 file thêm; report này là file thêm thứ 13. Không có source file của API/gRPC/worker/Identity/Maintenance/Incident/Notification/frontend hoặc lock/package file nào thay đổi bởi Stage 2.1. Branch/HEAD/staged state giữ nguyên.

Kiểm tra scoped git diff --check đạt exit 0; các generated SQL EOF đã chuẩn hóa. Các thay đổi dirty có trước ngoài scope được giữ. Historical InitialCreate/designer hashes không đổi.
Không thêm secret/token/key thật; SQL test connection dùng Windows Integrated Security. Chuỗi “secret-token” trong negative domain test là dữ liệu giả để kiểm allowlist service actor, không credential.

## 11. Known limitations và phần chưa triển khai

- No production handler/store cấp epoch dưới lock, Begin/Continue/Cancel command idempotency hoặc retirement eligibility verifier; nền domain/internal contract và SQL constraints đã có.
- No Register/Confirm/Abort RPC, fencing lease/reconciliation workflow hoặc real abort behavior. Raw Aborted seed trong migration guard test chỉ chứng minh không mất tombstone data.
- No actual source authentication; authenticatedSource trong domain là assertion Application phải cung cấp từ principal đã xác minh ở Stage 2.5.
- No Equipment retirement admission logic trong RPC legacy, no retirement endpoints/coordinator, no source gates cho Maintenance/Incident; không có đường public mới cho phép Retire bỏ qua chúng.
- No WorkOrder generation/Redis WO consumer/final Retire API. RPC/event ordering end-to-end NOT TESTED.
- Missing legacy source Pending equipment linkage và Incident RPC-inside-SQL-TX ở source cũ là dependency đã ghi ở Preparation; không sửa vượt scope Stage 2.1.
- Không claim toàn bộ D3/Retire/Module 3/backend hoàn tất. Schema mới chỉ ở fixture; app Equipment vẫn InitialCreate. Binary Equipment mới đã map thêm bảng/cột nên chưa được chạy với app schema cũ để kiểm nghiệp vụ; cần kế hoạch migration được phép trước khi dùng binary đó trên application DB. Không start API auto-migrate để tự nâng schema trong Stage 2.1.
- PNG/draw.io export cũ không tự cập nhật; ERD JSON/Mermaid/dictionary mới là nguồn schema hiện hành.

## 12. Kết luận

Tất cả acceptance criteria **Stage 2.1 foundation** đã được kiểm bằng build/domain/SQL/schema/migration evidence trong phạm vi cho phép; các phụ thuộc runtime stage sau được ghi rõ, không mở API hoặc chạy coordinator để demo.

**PASS — STAGE 2.1 COMPLETE.**

Dừng sau báo cáo, chờ chủ dự án review. Không tự bắt đầu Stage 2.2; không commit/push.
