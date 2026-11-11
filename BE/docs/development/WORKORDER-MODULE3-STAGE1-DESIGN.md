# Module 3 — Stage 1: Contract và database design — Revision 1

Ngày 05/11/2026. **Chỉ là thiết kế đề xuất, chưa triển khai hoặc kiểm thử runtime.** Bản này thay thế thiết kế Retire một transaction trước đây. Stage 0 đã được chấp thuận và được giữ nguyên như hồ sơ lịch sử; nhận xét “không phát hiện mâu thuẫn” trong Stage 0 không chứng minh invariant Retire đã được bảo đảm.

Nguồn: yêu cầu Module 3 và REVIEW REVISION — MODULE 3 STAGE 1. Xem [báo cáo revision](WORKORDER-MODULE3-STAGE1-REVISION-REPORT.md). Chưa được chuyển Stage 2 khi những thay đổi nghiệp vụ/phạm vi ở §15 chưa được chủ dự án duyệt.

## 1. Vấn đề, invariant và phạm vi bảo đảm

Khóa hàng Equipment chỉ serialize các thao tác đã tới Equipment. Nó không nhìn thấy Pending đã commit ở Maintenance nhưng chưa gửi/nhận Register. Query Maintenance một lần rồi Retire cũng có khoảng trống giữa query và final commit. Thêm RetirementIntent nhưng không chặn việc persist Pending tại source vẫn không đủ.

Các invariant phải giữ:
1. Retire chỉ Completed khi không có D3 Pending chưa xác định tại mọi source liên quan và không còn registration Pending/Active, Incident chưa Closed, WO chưa Closed/Cancelled hoặc Schedule còn hiệu lực.
2. Pending + CancelRequested, pending không registration, max-retry/intervention và response không rõ đều là blocker.
3. Registration Schedule/Incident được xét đầy đủ. Inactive không chứng minh Released. Chỉ bằng chứng terminal đã được xác nhận đúng protocol mới có giá trị.
4. Không tự Cancel WO, Abort/Release registration hoặc xóa lịch sử để thanh lý.
5. Broken giữ nguyên khi mở/hủy intent; intent là trạng thái phối hợp riêng, không thêm trạng thái vận hành và không đổi thành Operational.
6. Admin mới được yêu cầu/tiếp tục/hủy Retire. Service credential chỉ báo proof, không có quyền tự mở hoặc hủy intent.

**Cơ chế đề xuất: Equipment RetirementIntent + durable admission gate ở từng source + proof Sealed bền.** Equipment đóng admission trước, từng source đóng admission của mình trước khi xác nhận không còn blocker. Gate không hết hạn theo TTL.

Bảo đảm được lập luận cho các race của ứng dụng nếu mọi writer đi qua source gate, mọi Register đi qua Equipment gate, mọi source bắt buộc đã tham gia, và proof/gate/tombstone tồn tại bền. Nếu vẫn có writer cũ bỏ qua gate, legacy không đối soát được, nguồn không hỗ trợ protocol, hoặc proof thiếu: **không cho hoàn tất Retire**. Không tuyên bố source WorkOrder-only hoặc kiểm tra local Equipment-only đã giải quyết toàn hệ thống.

Lập luận theo điểm serialize: Pending transaction và Freeze transaction tranh cùng source gate. Pending thắng thì nằm trong tập blocker phải giải quyết trước Seal; Freeze thắng thì writer không được tạo Pending. Seal chỉ có khi tập blocker rỗng và gate tiếp tục đóng. Equipment final transaction yêu cầu mọi nguồn đã Seal cùng intent/epoch, nên không cần giả định một snapshot đồng thời xuyên database. Ngoại lệ mở lại gate chỉ bắt đầu sau quyết định Cancelling bền ở Equipment; quyết định đó tranh cùng Equipment lock với final Retire và vô hiệu final Retire trước khi bất kỳ nguồn nào mở gate. Đây là điều kiện chống race, không phải bằng chứng code hiện tại đã thực thi đúng.

## 2. Kiến trúc, identity và payload WO

API → Application command/query abstraction → Infrastructure/EF; Domain độc lập EF/gRPC. Mỗi service giữ database riêng, không đọc chéo DB/FK xuyên service, không dùng distributed transaction.

Business key WO: Maintenance / WorkOrder / OccurrenceId. Scope Maintenance.WorkOrderGeneration, OperationName RegisterWorkOrder, key UUID OccurrenceId. OperationId sinh một lần khi insert operation; WorkOrderId có thể bằng OperationId nhưng ResourceId của Equipment registration là OccurrenceId.

Immutable payload v1: EquipmentId, ScheduleId, OccurrenceId, WorkOrderId, WorkType, ScheduledForUtc, DueAtUtc. Chuẩn UUID lowercase D, UTC serialization cố định và thứ tự field cố định; SHA-256 UTF-8 được kiểm lại tại Equipment. Lease/fence không thuộc business hash. Không lưu JWT/secret trong payload hoặc Audit.

Snapshot Equipment/Department lưu bền tại lần tạo registration đầu tiên; replay/query trả lại snapshot đó. WO copy thời gian đã lưu trong operation/occurrence, không tính lại Due từ now. Broken được đăng ký Scheduled, IsBlocking=false, không thay operational status.

## 3. Hai lớp gate và state machine Retire

### 3.1. Equipment RetirementIntent
Một Equipment tối đa một intent chưa terminal. Epoch tăng đơn điệu trên từng Equipment, không tái sử dụng intent/epoch sau Cancelled. Các trạng thái:
- Coordinating: intent đã commit, chặn mọi registration mới; đang chờ source freeze/proof hoặc còn blocker.
- Ready: đã có Sealed proof của mọi source bắt buộc; vẫn chưa Retired, còn phải final-check local registrations/projection/legacy coverage trong transaction.
- Cancelling: Admin đã chốt hủy intent bền tại Equipment; chưa mở admission, đang chờ mọi source ghi cancel tombstone và mở gate.
- Cancelled: đã nhận cancel acknowledgment của mọi source; mở Equipment admission nếu thiết bị chưa Retired. Giữ intent/receipt/Audit.
- Completed: final transaction đã Retire Equipment. Terminal; không hủy hoặc quay lại Open.

NeedsIntervention/BlockedReason là metadata khi retry hết hạn, source unknown hoặc blocker tồn tại, không tự biến intent thành Cancelled/Completed. Ready không đồng nghĩa API Retire thành công.

Bắt đầu intent chỉ cập nhật coordination epoch/intent, không chuyển Equipment.OperationalStatus. Register mới của cả WorkOrder, Schedule và Incident bị chặn khi có Coordinating/Ready/Cancelling. Replay một registration đã tồn tại với đúng identity/hash được trả lại, Confirm/verified Abort của operation cũ được xử lý; không được mở reservation mới dưới dạng replay.

### 3.2. Gate tại Maintenance và Incident
Mỗi source có durable gate theo EquipmentId. Source Maintenance bao phủ WO và Schedule; Incident bao phủ Incident. Phase:
- Open: được persist Pending mới qua gate.
- Frozen(intent, epoch): không tạo Pending mới hoặc mutation tạo lại blocker mới; giữ operation đã có.
- Sealed(intent, epoch, proof): đã kiểm tra chính xác không còn blocker tại source trong transaction và cam kết không cho xuất hiện blocker mới khi intent còn hiệu lực.
- Open với terminal cancel tombstone: đã xử lý hủy đúng intent; cho phép công việc mới nhưng vẫn từ chối Prepare cũ.
- Retired: nhận terminal Equipment Completed; vĩnh viễn không mở admission.

Một gate chưa có row phải được tạo/khóa an toàn trước writer, không coi row thiếu là vượt qua khóa. Writer dùng cùng khóa gate EquipmentId trước persist Pending, kể cả API tạo Schedule/Incident, worker WO, retry tạo lại dữ liệu và import/legacy path. Mutation như reactivation Schedule cũng phải đi qua gate nếu làm xuất hiện blocker mới. Planner lập Occurrence và Reminder không tạo D3 Pending nên không cần viết lại thuật toán; Occurrence vẫn được giữ.

Frozen không tự Abort Pending hiện có. Operation cũ có registration đã xác nhận được tiếp tục giải quyết theo nghiệp vụ hiện hành; chuyển Pending sang Committed + WO vẫn là blocker, không được lấy riêng số Pending=0 làm proof. Lifecycle hợp lệ kết thúc hồ sơ/registration tiếp tục hoạt động, không tự triển khai workflow mới trong tài liệu này.

Pending chưa có registration không được tạo registration mới xuyên Equipment gate. Nó giữ Pending và chặn Retire cho tới khi Admin hủy RetirementIntent để tiếp tục công việc, hoặc xử lý operation theo quyền Cancel đã được duyệt. Option A của WO không mặc nhiên cấp quyền Cancel cho Schedule/Incident.

### 3.3. Proof Sealed
Trong source transaction: khóa gate, xác minh Frozen đúng intent/epoch, kiểm tra indexed blockers, tạo ReadyProofId và SourceGateVersion, chuyển Sealed và ghi Audit. Chỉ trả proof sau commit.
Proof ràng buộc EquipmentId + IntentId + RetirementEpoch + SourceService + CoverageVersion + SourceGateVersion + ReadyProofId.
Coverage Maintenance phải bao phủ cả WO/Schedule; Coverage Incident bao phủ Incident. Danh sách source/coverage bắt buộc lưu bất biến trong intent, không suy ra từ các registration hiện đang có.

Blockers source gồm:
- Pending mọi loại liên quan, kể cả CancelRequested, Unknown, no-registration và max attempts.
- Committed operation thiếu/mâu thuẫn aggregate; legacy Pending không xác định được EquipmentId; chưa xác minh dữ liệu.
- Incident chưa Closed, WO chưa Closed/Cancelled, Schedule còn hiệu lực và unresolved coordination theo nguồn.
- Aborted chưa có bằng chứng bền tương ứng hoặc terminal state không nhất quán.
Schedule Inactive có thể không nằm trong count “active Schedule” nhưng Equipment registration Pending/Active vẫn chặn final Retire; không được dùng source count để bỏ qua nó.

Proof không phải snapshot không có ràng buộc: gate Sealed phải tiếp tục cấm tạo blocker mới. Không tự hết hạn hoặc unseal theo timeout. Chỉ quyết định Cancel bền tại Equipment hoặc terminal Completed mới đổi gate. LastVerifiedAt/TTL không thay proof.
Nếu legacy Pending không gán được Equipment, chặn readiness cho phạm vi thiết bị có thể bị ảnh hưởng; khi không thể xác định phạm vi thì nguồn không được phát bất kỳ Sealed proof nào cho đến khi đối soát, không đoán thiết bị.

## 4. Giao thức phối hợp giữ chiều kết nối C1

Đề xuất worker nguồn Maintenance/Incident **poll Equipment gRPC** theo chiều đã có trong C1; không thêm Equipment→Maintenance network edge. Các source tự đóng gate và báo proof. Không có HTTP query một lần “không tìm thấy Pending” để mở Retire. Nếu chuyển sang Equipment gọi source trong tương lai phải review C1/auth riêng; bản này không dùng cách đó.

### Luồng chuẩn
1. Admin POST retire: Equipment TX E1 khóa Equipment, kiểm RowVersion/quyền/reason và idempotency, tạo intent Coordinating + epoch + required coverage + Audit. Commit, trả 202 với IntentId/status URL, chưa đổi IsRetired.
2. Source worker lấy intent qua List/Get gRPC bên ngoài source SQL transaction. Source TX S1 khóa gate; ghi Frozen đúng intent/epoch + Audit. Nếu đã có terminal tombstone cũ thì không áp lại freeze.
3. Nguồn kiểm tra/giải quyết blocker trong nhiều tick. Kết quả Blocked/Unknown chỉ là tiến độ, không phải readiness. Retry/Abort/lifecycle vẫn tuân thủ quyền và luật đã duyệt.
4. Source TX S2: khi blockers thực sự bằng 0, seal bền; gọi ReportRetirementSourceState ngoài transaction với proof. Equipment TX E2 lưu receipt đã xác thực đúng epoch/coverage/version. Duplicate proof replay, response cũ không ghi đè proof mới.
5. Chỉ khi đủ source Sealed, Equipment có thể chuyển Ready. E3 final transaction khóa Equipment, intent, receipts, registrations/projections; kiểm lại epoch/state, đủ proof, không Pending/Active của mọi loại, không blocking/unknown legacy, chưa Retired. Retire domain transition + intent Completed + Audit + status history cùng transaction. Sau commit mới trả trạng thái Retired thành công.
6. Nguồn query intent theo ID đang theo dõi để nhận Completed, chuyển local gate Retired. Nếu thông báo này timeout, gate Sealed vẫn đóng bền; không có khoảng trống để tạo Pending. Không cần chờ ACK đổi nhãn Retired để coi final E3 hợp lệ, vì mọi readiness đã được xác nhận bền trước E3. Việc này khác thiếu Sealed proof hoặc query không rõ kết quả.

List dùng stable sequence/keyset, batch và vòng quét lại; nguồn giữ checkpoint cùng danh sách gate/intent đang theo dõi bền. Query Get theo intent đó dù nó không còn trong trang active. Không lấy hết một trang làm bằng chứng “không còn operation”. Receipt chỉ được tạo từ transaction gate của nguồn đã commit, không từ memory cache.

### Admin Continue/Cancel intent — đề xuất cần duyệt
GET intent cho Admin trả trạng thái intent, blocker counts/reason đã lọc, từng source readiness/last contact, phiên bản, không secret. Continue giữ nguyên intent/epoch, cho coordinator thử lại; không tạo proof, không Cancel operation và không bỏ qua gate.
Cancel yêu cầu RowVersion + reason:
- E4 khóa Equipment/intent; nếu chưa Completed, ghi Cancelling + Admin/reason/Audit bền trước khi thông báo nguồn. Từ đây stale E3 không thể Retire.
- Source nhận trạng thái Cancelling qua Get/List, TX S3 khóa gate, ghi terminal Cancelled tombstone (intent/epoch) và mở gate; commit rồi gửi CancelAck bằng service identity.
- Equipment chỉ chuyển Cancelled/mở admission sau đủ CancelAck của mọi source bắt buộc. Cancel trước khi source từng Frozen vẫn yêu cầu source ghi tombstone, để chặn delayed Prepare cũ.
- Nếu nguồn đã mở gate nhưng Ack timeout, nó có thể persist Pending mới; Equipment vẫn đóng nên Register mới tạm bị chặn. Source retry Ack, cuối cùng Equipment mở admission. Không được bắt đầu intent epoch mới trước khi intent cũ hoàn tất Cancelled.
- Cancelled intent không bao giờ dùng lại receipt Sealed cũ. Late report/Prepare/Cancel với epoch cũ không được ảnh hưởng intent mới.
- Completed thắng transaction E4: Cancel trả 409; E4 thắng E3: E3 không được Completed. Hủy intent không hồi sinh thiết bị đã Retired.

Không tự cancel intent sau downtime, timeout hoặc max attempts. Điều này ưu tiên tính nhất quán nhưng có thể tạm ngừng nhận công việc mới; cần chủ dự án duyệt tại §15.

## 5. Service authentication và RPC/contract

Principal Maintenance chỉ scope WO RPC và retirement coverage Maintenance. Principal Incident chỉ scope registration hiện hữu và retirement coverage Incident. Không một source nào được báo thay nguồn kia, tự tạo intent, Retire hoặc Cancel intent. Credential lấy từ environment/secret management, constant-time verification, TLS theo deployment; không JWT Admin giả. User JWT Admin không thay thế service proof. Schedule/Incident user auth hiện hữu giữ nguyên; gate là điều kiện nghiệp vụ bổ sung, không giảm RBAC.

### RPC WO
| RPC | Request/response chính | Idempotency và fence |
|---|---|---|
| RegisterWorkOrder | Immutable payload/hash, operation/fence/correlation → registration + snapshot + state | Equipment lock; retirement gate trước insert mới; same identity/hash replay; retired/aborted/stale fence từ chối |
| GetWorkOrderRegistrationByOperation | equipment/occurrence/operation/hash → Found + bound registration/hash/snapshot/state/fence | NotFound không chứng minh source operation đã Aborted; không cấp quyền tạo vượt gate |
| ConfirmWorkOrderCommitted | operation/WO/occurrence/registration/hash/fence/sourceVersion → durable state | Chỉ từ source Committed bền; cùng transition với event consumer; terminal guards |
| VerifyAndAbortWorkOrderRegistration | immutable identity/hash, CancelIntentId/fence, durable source cancel assertion → tombstone | Source đã ngăn local commit; absence→Aborted được ghi bền, kể cả gate đang đóng; không tự release |

### RPC retirement bổ sung trên Equipment gRPC
| RPC đề xuất | Contract | Kết quả/authorization |
|---|---|---|
| ListRetirementIntents | Source identity, coverage version, cursor/page limit | IntentId, EquipmentId, epoch, phase, next cursor; nguồn chỉ xem coverage cần tham gia; không trả Admin secret |
| GetRetirementIntent | IntentId/EquipmentId/epoch | Trạng thái bền và required coverage; hỗ trợ phục hồi sau timeout/restart, terminal vẫn truy vấn được |
| ReportRetirementSourceState | Intent binding, source/coverage, local gate version, phase Frozen/Blocked/Sealed, proofId nếu Sealed, blocker summary/correlation | Equipment receipt bền; exact replay; lower version/epoch không thay proof; Frozen/Blocked không được tính Ready |
| AcknowledgeRetirementCancellation | Intent binding, source gate version, durable Cancelled tombstone identity | CancelAck bền cho đúng source; chỉ chấp nhận intent Cancelling/Cancelled; duplicate trả cùng kết quả |

ReadyProof là authenticated assertion sau local seal; worker không được đưa count=0 không có gate proof vào RPC. IntentId/epoch/proof là dữ liệu phối hợp, không phải secret và không thay authorization. RequiredSource set chỉ Equipment định nghĩa theo coverage đã triển khai, không nhận tùy ý từ client.

Errors: Unauthenticated/PermissionDenied sai credential/scope; InvalidArgument sai identity/hash; AlreadyExists resource hoặc idempotency key khác payload; FailedPrecondition retirement-in-progress/Retired/terminal conflict; Aborted stale fence/version. DeadlineExceeded/Unavailable là Unknown, không tự tạo success/zero proof. Phân biệt retryable gate-closed với retired terminal, và permanent payload mismatch với transport timeout.

REST retire thay đổi: POST retire trả 202 khi mới tạo intent; chỉ trạng thái Completed mới trả representation Retired với IsRetired=true. GET status phân biệt Coordinating/Blocked/Ready/Cancelling/Cancelled/Completed. Continue/Cancel cần Admin, RowVersion và reason. Key trùng/payload khác 409; key trùng/payload giống trả intent hiện có, không start epoch mới. Không ghi event Retire vô chủ; Audit/history bắt buộc.

## 6. D3 operation và registration state machine

Maintenance IdempotencyRequest.State giữ Pending/Committed/Aborted:
- Generate Pending: persist payload và OperationId một lần, có lease/retry.
- CancelRequested là intent bền bổ sung, State vẫn Pending; ngăn mọi commit WO cũ. Chỉ Abort reconciliation được chạy.
- InterventionRequired giữ Pending, không tự Abort; LastError không phải lịch sử.
- Committed: WO/occurrence/Audit/Outbox đã commit cùng local TX; Retire vẫn bị chặn bởi WO còn mở và registration.
- Aborted: terminal sau remote tombstone được xác nhận, không trở lại Pending hoặc tạo operation khác cho cùng kỳ.

Equipment registration: absent→Pending khi Register hợp lệ và gate mở; absent→Aborted trên verified Abort; Pending→Active khi source commit proof; Pending→Aborted trên durable cancel proof. Active/Committed proof không được bị Admin Cancel WO hồi tố; remote Active nhưng source Pending là bất nhất cần intervention, không đoán.
Released chỉ có từ workflow terminal đã duyệt ở module sau, không thêm Release RPC ở Module 3. SourceVersion cho aggregate progression, FencingToken cho ownership; không đánh đồng RetirementEpoch với hai trường này.

CancelRequested source commit trước network, nâng op fence và vô hiệu lease; verified Abort dùng đúng identity/hash/intent. Equipment giữ tombstone ngay cả khi Register chưa tới, Register đến muộn bị chặn. Source chỉ ghi Aborted sau xác nhận/query tombstone đúng binding. Nếu response mất, giữ Pending + CancelRequested; Retire chờ source seal, không lấy tombstone Equipment thay xác nhận local Aborted.

## 7. Mapping trước/sau từng transaction, RPC và event

Ký hiệu M=Maintenance DB, E=Equipment DB; R=EquipmentUseRegistration; O=WorkOrderCreated Outbox; I=Equipment Inbox cho event WO. WorkOrder.EquipmentUseRegistrationId là field bắt buộc trên WO, **không tồn tại field này khi chưa có WO row**, không dùng Guid.Empty giả.

| Điểm | M operation | WO và registration field | E registration | O | I / Retire |
|---|---|---|---|---|---|
| Trước T1 | Chưa có | Chưa có WO | Thường absent, phải query nếu recovery | Không có | Không có; không suy ra an toàn từ E absent |
| T1 commit Pending qua source gate | Pending + payload/hash/op bền | Chưa có WO, field chưa tồn tại | Có thể absent | Không có | Retire phải thấy blocker khi source freeze/scan |
| T2 claim commit | Pending + owner/expiry/fence/attempt | Không đổi | Không đổi | Không có | Gate/proof không lấy claim làm terminal |
| Register RPC chưa rõ kết quả | Pending | Chưa có WO | absent hoặc Pending, không đoán | Không có | Blocker ngay cả khi Equipment chưa có R |
| Register E transaction commit | Pending | Chưa có WO | Pending, stable RegistrationId/snapshot/hash | Không có | R Pending chặn Retire |
| M T3 rollback/fail sau Register | Vẫn Pending bền | Không có WO, không lưu field giả | Pending còn nguyên | Không có event giả | Recovery query cùng op, giữ Retire blocked |
| M T3 commit thành công | Committed | Scheduled + registrationId đúng R; occurrence WorkOrderCreated | Pending có thể chưa confirm | Pending, EventId ổn định | Source không Sealed vì WO mở; E R vẫn chặn |
| Confirm RPC E commit trước event | Committed | Giữ nguyên | Active + bound committed proof/sourceVersion | Pending/Processing/Published tùy dispatcher | Chưa có I; event vẫn phải xử lý Inbox |
| Event E transaction trước Confirm RPC | Committed | Giữ nguyên | Active cùng committed binding | Đã publish sau source commit | R + I atomically commit trước ACK |
| RPC rồi event / event rồi RPC / duplicate | Committed | Không đổi | Active, no-op cho cùng proof/version | Không sinh thêm O | Event thêm I một lần; RPC không tạo I giả |
| Cancel request M commit | Pending + CancelIntentId + fence mới | Chưa có WO | absent/Pending; chưa coi Aborted | Không có WO-created event | Retire vẫn blocked |
| Abort E commit, M chưa nhận | Pending + CancelRequested | Chưa có WO | Aborted tombstone bền | Không có | Source chưa được seal; không lấy E Aborted làm source terminal |
| M Abort-result transaction commit | Aborted | Chưa có WO | Aborted đúng binding | Không có WO-created event | Operation này hết blocker; blocker khác vẫn xét |
| WO terminal ở module sau | Committed | Lịch sử WO còn, cùng R id | Chỉ Released sau terminal proof hợp lệ | Lịch sử O còn | I/Audit còn; không triển khai release tại đây |

Confirm RPC và event consumer gọi cùng một domain transition/verification: đúng EquipmentId, operation, occurrence, WorkOrderId, registration, request hash và committed sourceVersion. Đồng thời cùng khóa Equipment→Registration; transition chỉ một lần.
- Cùng committed proof/version đã áp dụng thì trả idempotent/no-op, không giảm SourceVersion/FencingToken.
- Event cùng EventId có I rồi thì ACK; khác EventId nhưng cùng proof chỉ thêm I cho envelope đó, không lặp transition hay nghiệp vụ.
- RPC trước event không thay ACK: consumer vẫn lưu I dù R đã Active; event trước RPC làm RPC no-op.
- Older fence không được dùng để advance Pending; replay một committed result đã được nhận diện chính xác không hạ fence hoặc phục hồi state.
- Released không được quay lại Active: nếu nhận diện được chính xác committed proof đã áp dụng cho cùng WO thì historical confirm/event chỉ no-op và vẫn có thể lưu Inbox cho duplicate envelope. Nếu không chứng minh được binding thì giữ lỗi để đối soát. Aborted nhận một Created/Committed proof là bất nhất, không revive; identity mismatch hoặc cùng version khác proof cũng là lỗi. Ghi protocol violation, giữ message pending hoặc durable DLQ trước ACK; không ghi normal Inbox-success để che xung đột.
- Registration và Inbox cùng E transaction; RPC confirm không trực tiếp ACK Redis.
Dispatcher chỉ publish sau T3 commit, retry giữ EventId. Không tạo notification trực tiếp trong T3.

## 8. Transaction boundaries và lock order mới

Source lock order chung:
**SourceEquipmentRetirementGate(EquipmentId) → Schedule → Occurrence → Operation**, chỉ lấy các row có liên quan. Incident thay Schedule/Occurrence bằng aggregate Incident liên quan. Mọi writer có thể tạo blocker mới phải lấy gate trước. Thứ tự cũ Schedule→Occurrence→Operation của bản trước chưa đủ cho retirement barrier.
- Prepare Pending: khóa gate Open rồi Schedule/Occurrence/operation-range, kiểm due/Active/unique, insert Pending/hash/payload + Audit; commit.
- Claim lease: chỉ khóa operation, tăng fence/attempt + Audit; kết thúc TX, không lấy gate khi đang giữ operation. Bước sau mở TX mới theo gate-first.
- RPC Register/Query/Confirm/Abort: bên ngoài mọi local SQL TX.
- Final generation: gate→Schedule→Occurrence→Operation, kiểm lease/fence/expiry/RowVersion, không CancelRequested, due/Active/Planned, remote binding; nếu gate Frozen chỉ được giải quyết operation có trước freeze và registration đã tồn tại, không tạo operation/reservation mới. WO+occurrence+Committed+Audit+Outbox cùng TX. Sau Sealed không thể có Pending để final generation hợp lệ.
- Freeze/seal: khóa gate, kiểm blocker dưới cùng admission barrier. Các path giảm blocker cũng đi gate-first; không dựa count ở snapshot cũ. Query/range locks và indexes phải ngăn phantom từ writer tuân thủ gate; chỉ dùng READ COMMITTED snapshot riêng lẻ không đủ.
- Cancel WO/final Abort và mutation tạo blocker như Schedule reactivate: gate-first, rồi row liên quan. Cancel/terminal resolution được phép khi Frozen, không được biến thành generation mới.
- Không khóa nhiều Equipment tùy ý; thao tác bulk nhiều thiết bị lấy gate theo EquipmentId tăng dần, giữ batch nhỏ.

Equipment lock order:
**EquipmentAsset → RetirementIntent hiện tại → SourceReceipts (thứ tự SourceService) → Registration/projection/history liên quan**.
Mọi Register/Confirm/Abort/event consumer/Retire dùng Equipment lock trước; không có đường registration-first rồi lấy Equipment. Register kiểm active intent dưới cùng Equipment lock trước insert.
Final Retire và Cancel intent dùng cùng Equipment/intent locks, nên chỉ một phía thắng. Epoch cấp dưới cùng Equipment lock, unique EquipmentId+Epoch; không sinh từ đồng hồ.
Mọi RPC/publish nằm ngoài TX của service gọi. Receipt arrival RPC chỉ ghi local E transaction. Source không gọi Equipment trong S1/S2/S3. ACK Redis sau E commit.

## 9. Concurrency matrix A–H và phục hồi

| Race | Trạng thái DB sau bước cạnh tranh | Điều kiện hoàn tất và phục hồi |
|---|---|---|
| A. Pending commit trước intent | M Pending bền; E Coordinating, R có thể absent; M sau Freeze là Frozen với blocker Pending | E không có Sealed receipt nên không Completed. Query không registration không giải phóng. Nếu R absent, Admin xử lý WO theo Option A hoặc hủy intent để tiếp tục; không tự Abort. |
| B. Intent commit trước Pending | E Coordinating/gate đóng. Trước source Freeze, Pending vẫn có thể commit và trở thành trường hợp A. Nếu Freeze thắng source gate lock, writer không được persist Pending mới. | Source scan dưới gate bao gồm mọi Pending commit trước Freeze; sau Seal không thể phát sinh Pending mới. Không giả định source biết intent tức thì. |
| C. Register đồng thời Retire | Nếu Register giữ Equipment lock trước: R Pending commit, intent sau đó bị blocker. Nếu E1 giữ lock trước: intent Coordinating commit, Register mới bị retirement-in-progress; M giữ Pending. | Final E3 luôn xét cả source proof và R; không có Retired kèm registration mới hợp lệ. Source pending phải được giải quyết/hủy intent, không bypass gate. |
| D. RPC đã gửi, caller không biết kết quả | M Pending. Nếu remote insert thắng gate: E R Pending; nếu intent thắng: R absent và Register bị từ chối hoặc response mất. | Query cùng op+intent, audit Unknown. Cả hai khả năng đều chặn Retire qua source Frozen blocker, tuyệt đối không coi deadline là absent/resolved. |
| E. Register commit, M chưa commit WO | E R Pending, intent Coordinating; M Pending. Nếu M commit hợp lệ: Committed+Scheduled WO+O, R Pending/Active. Nếu M rollback: vẫn Pending. | Hai kết quả đều là blocker. Recovery query cùng R, không tạo WO duplicate. Confirm chỉ sau M commit; không “drain” bằng việc bỏ qua WO Scheduled. |
| F. Retire khi CancelRequested | M Pending+CancelIntent; E R absent/Pending/Aborted tùy ranh giới; intent Coordinating, source Frozen | Chỉ M Aborted bền sau E tombstone mới hết blocker operation. Abort RPC được đi qua retirement gate để giải quyết operation cũ; timeout vẫn blocked. Không tự release R để Retire. |
| G. E/M restart giữa bước | Intent/epoch/receipts và source Frozen/Sealed/cancel tombstone còn trong SQL; lease tạm có thể hết nhưng gate không mở | Worker phục hồi từ DB, query intent theo ID, replay report/proof/CancelAck. E commit Completed trước source biết: source vẫn Sealed nên không có Pending mới; source cập nhật Retired sau. |
| H. Service query/report timeout | DB giữ trạng thái durable cuối: E Coordinating/Ready/Cancelling, nguồn Open/Frozen/Sealed tùy commit đã xảy ra | Chưa có đủ durable proof thì 202/unknown, không Completed. Retry cùng intent/epoch/proof; query result mới không ghi đè terminal/newer state. Nếu timeout sau E3 commit thì GET cho biết Completed; caller không tự khởi tạo intent khác. |

Race bổ sung:
- Cancel intent thắng final E3: E Cancelling, kể cả receipts cũ còn Sealed cũng không được final Retire; source unfreeze đúng intent.
- E3 thắng Cancel: E Completed/Retired; Cancel trả conflict, source không mở gate.
- Delayed Prepare N sau CancelAck N: source tombstone N làm no-op; không re-freeze.
- Delayed Cancel N khi N+1 Frozen: trả historical acknowledgment, không mở gate N+1.
- Source query “không blocker” trước Freeze không được phát Sealed; có Pending mới trước gate lock phải được thấy.
- Report Sealed bị mất response: nguồn giữ nguyên proof, gửi lại; E upsert đúng identity và phiên bản, không count duplicate thành nguồn khác.
- Inactive trước WO commit: không tạo WO, Pending/registration giữ nguyên; nó tiếp tục chặn Retire. Inactive sau WO commit giữ WO/R.
- Source mất liên lạc vô hạn: Retire không hoàn tất; availability không được ưu tiên hơn invariant.

## 10. Audit lịch sử Retry và Unknown

Reuse AuditEntries, không dùng LastError hoặc log thay lịch sử. Audit factory hỗ trợ EntityType=WorkOrderD3Operation/RetirementIntent/RetirementSourceGate và ActorService hoặc ActorUserId đúng nghĩa. Không giả user GUID cho worker.

Mỗi record có AuditId, EntityId/OperationId hoặc IntentId, CorrelationId, action, actor, timestamp UTC; structured ChangesJson chứa immutable AttemptId/CommandId, attempt ordinal, attempt kind, phase, fence/epoch, outcome/error code, nextRetry/lease info phù hợp. Không secret/raw SQL hoặc payload người dùng không cần thiết.

| Hành động | Ghi khi nào / trong transaction nào |
|---|---|
| AutomaticAttemptStarted | Cùng transaction claim: unique AttemptId gắn operation+fence; lưu budget count/cumulative ordinal, phase Query/Register/Abort |
| AutomaticAttemptResult | Cùng TX lưu outcome/retry metadata hoặc generation/Abort terminal commit; outcome Success/Rejected/TimeoutUnknown/TransportUnknown/StaleIgnored |
| RecoveryAfterCrash | Attempt Started không có result; worker mới ghi OutcomeUnknown cho attempt cũ khi reclaim, không suy đoán RPC chưa chạy; bắt đầu attempt mới có ID riêng |
| AdminRetryRequested/Applied | Actor+reason+CommandId+RowVersion và before/after budget ghi atomically với retry authorization; duplicate CommandId không reset budget hoặc nhân Audit nghiệp vụ |
| CancelRequested | Cùng TX ghi durable CancelIntentId/fence/actor/reason; không ghi CancelSucceeded sớm |
| AbortAttemptResult/AbortConfirmed | Unknown lưu mỗi attempt; Confirmed chỉ cùng TX local Aborted sau tombstone binding đúng |
| RetirementRequested/SourceFrozen/SourceSealed | Cùng local TX thay đổi intent/gate; worker báo proof chỉ sau đó |
| RetirementQueryAttempt/Result | Ghi Started trước network, kết quả sau network ở local TX; timeout/unknown giữ nguyên blocker |
| RetirementContinue/CancelRequested/SourceUnfrozen/Cancelled/Completed | Actor/service, reason, epoch, receipt/proof/CommandId; cùng local mutation tương ứng |

Started audit không chứng minh remote call đã thực thi; thiếu Result sau crash không thể tự thành failure chắc chắn. Stale worker không được ghi đè metadata mới, nhưng có thể append Audit StaleResultIgnored gắn AttemptId cũ, không thay state. Unique idempotency của command/attempt-outcome bằng AuditId xác định hoặc unique index thích hợp; query-only attempt cũng có Started/Result, tránh chỉ Audit lần Register.

Backoff SQL bền, batch giới hạn; Attempts per Admin-authorized budget không phải tổng lịch sử. Retry do Admin không xóa audit/lower fence. Worker hết lượt retry giữ intervention, gate/registration không hết hạn. Không dùng Task.Delay làm retry persistence.

## 11. Schema và migration dự kiến

Đây chỉ là thiết kế; **không tạo migration trong revision này**. Tên cột/bảng cuối cùng sẽ được review với EF model trước Stage 2.

### Maintenance IdempotencyRequests
Reuse OperationId/hash/State/RowVersion và unique actor/name/key. Thêm có điều kiện:
- EquipmentId uniqueidentifier NULL: logical reference + index cho source readiness, bắt buộc cho operation protocol mới, không suy ra legacy Pending không rõ.
- OccurrenceId uniqueidentifier NULL, FK local NoAction; RequestPayloadJson nvarchar(max) NULL.
- AttemptCount int default0, NextRetryAtUtc/LastAttemptAtUtc datetimeoffset NULL, LastError nvarchar(1000) NULL.
- LeaseOwner nvarchar(128) NULL, LeaseUntilUtc datetimeoffset NULL, FencingToken bigint default0.
- InterventionRequired bit default0, CancelIntentId uniqueidentifier NULL, CancelRequestedByUserId uniqueidentifier NULL, CancelReason nvarchar(2000) NULL, UpdatedAtUtc datetimeoffset NULL.
- CurrentAttemptId uniqueidentifier NULL và TotalAttemptCount bigint default0 để nối Started/Result sau crash và giữ lịch sử thứ tự dù reset budget.

Unique filtered OccurrenceId, pending retry index, và index EquipmentId+State+OperationName. Checks nonnegative count/fence, JSON, paired lease fields/intent fields. ExpiresAtUtc không cho phép xóa D3 operation/tombstone.

### EquipmentUseRegistrations
Thêm RequestHash binary(32) NULL, RequestPayloadJson nvarchar(max) NULL, FencingToken bigint default0, CancelIntentId uniqueidentifier NULL, và snapshot equipmentCode/name/departmentId/name nullable cho rows cũ.
Reuse SourceVersion riêng, states/unique source-operation và source-type-resource. Aborted row là tombstone; không xóa để mở Retire. Legacy chưa có hash/proof giữ NULL và đánh dấu chưa xác minh, không tạo hash giả. Thiếu Equipment FK thì Abort không được báo tombstone thành công.

### Retirement tables mới — lý do cần thiết
| Owner/table | Fields và constraints chính | Invariant |
|---|---|---|
| Equipment / EquipmentRetirementIntents | Id PK, EquipmentId local FK NoAction, Epoch bigint, State, Actor/Reason, request key/hash, required coverage JSON, Created/Updated, completed/cancel reason/actor/time, RowVersion, bounded retry/lease metadata cho coordination | Bền qua restart; unique EquipmentId+Epoch; unique EquipmentId với filter State <> N'Cancelled' AND State <> N'Completed' và check enum State; không xóa terminal epoch |
| Equipment / RetirementSourceReceipts | PK IntentId+SourceService; FK intent NoAction; CoverageVersion, ReportedGateVersion, Frozen/Blocked/Sealed/CancelAck, ReadyProofId/tombstone identity, summary/error/time, RowVersion | Thiếu source không thể Ready; exact binding, stale reports không overwrite |
| Maintenance / EquipmentRetirementGates | EquipmentId PK logical REF, CurrentIntentId, HighestRetirementEpoch, Phase, SourceGateVersion, ReadyProofId, coverage, UpdatedAt, RowVersion | Serialize mọi source Pending create với freeze/seal, kể cả Schedule |
| Incident / EquipmentRetirementGates | Cùng contract nguồn Incident | Incident không là lỗ hổng source Pending trước Register |

Gate row giữ last terminal intent/epoch/proof tombstone sau Open để chống delayed RPC. Chỉ giữ “current active intent” mà xóa cancellation history không đủ. Metadata terminal bổ sung lưu Audit/row history khi epoch tăng; mọi message so sánh epoch và binding trước write.

Bảng mới cần vì Equipment không sở hữu source operation và một RowVersion của Schedule không serialize được mọi Pending của nhiều aggregate. ReconciliationCheckpoint cũ theo snapshot source không thay được gate per Equipment. RequiredSource receipt không gộp bằng một timestamp LastVerifiedAt. Không thêm database hay distributed FK.

Incident operation metadata và mọi Schedule Pending cũ cũng phải đủ EquipmentId để quét chính xác. Cần thiết kế migration additive Incident nếu schema hiện hữu chưa có trường đó; chưa audit lại source trong lượt revision, không mặc định đã có. Legacy mapping phải có bằng chứng, nếu không thì readiness fail closed.

WorkOrder/Occurrence giữ schema hiện có: filtered unique OccurrenceId, exact times, rowversion, NoAction. AuditEntries giữ cấu trúc, bổ sung factory/index nếu cần để truy theo entity/attempt mà không thêm bảng “log” hình thức.

### Upgrade/rollback
- Không sửa InitialCreate hoặc migration lịch sử; không reset database.
- Nullable/default mới giữ legacy rows, không tạo Sealed proof hoặc tự coi legacy resolved trong backfill.
- Down guard phải kiểm tra sự tồn tại **bất kỳ dữ liệu protocol mới**, gồm terminal intent/receipt, gate tombstone/epoch, WO operation có payload/fence/audit proof và registration có proof. Không chỉ kiểm active rows. Sau Cancelled/Completed vẫn không được rollback làm mất fence/tombstone.
- Rollback cần dừng phiên bản writer mới trước. Nếu protocol đã dùng thì guard từ chối, không xóa dữ liệu để vượt guard; muốn rollback khi đó phải có kế hoạch downgrade riêng được duyệt.
- Test Up/Down/reapply trên catalog có dữ liệu legacy và chưa dùng protocol; test riêng guard khi đã dùng protocol, xác minh nguyên dữ liệu sau rollback bị từ chối.
- Ở Stage 0, **database ứng dụng chỉ InitialCreate**, còn fixture từng apply các migration mới hơn. Incident app còn thiếu AddIncidentOperationPhoto, Maintenance app còn thiếu AddOccurrenceReminderWindow. Đây là kết quả lịch sử Stage 0, chưa đọc lại hoặc apply DB trong revision này; không đổi trạng thái app thành “đã upgrade”.
- EF/model/ERD dictionaries và SQL scripts cần cập nhật owner-specific khi được triển khai, không mở rộng nhầm shared technical table của mọi service.

## 12. Failure recovery và kiểm soát triển khai

| Failure boundary | Recovery an toàn |
|---|---|
| E1 commit, response Admin mất | Idempotency key/GET tìm intent hiện có, không tạo intent thứ hai |
| Source thấy intent rồi crash trước Frozen | E chờ receipt; recovery lock source gate rồi freeze; Pending đã commit trước đó được xét |
| Frozen/Sealed commit, report timeout | Gate giữ nguyên; report lại cùng proof/epoch; không mở theo TTL |
| Source remote query timeout | Lưu Audit Unknown/next retry, không phát proof mới từ cache hoặc suy ra intent đã hủy |
| E nhận proof rồi restart trước E3 | Intent/receipts còn SQL; E3 vẫn final-check dưới Equipment lock |
| E3 commit, caller/source timeout | E đã Completed, source Sealed vẫn chặn tạo Pending; GET cho biết kết quả, source chuyển Retired sau |
| E4 Cancel commit, nguồn mất kết nối | E Cancelling/gate đóng; không Retire, không báo Cancelled hoàn tất |
| Nguồn mở gate nhưng CancelAck mất | Source terminal tombstone chống late Freeze, retry Ack; E đóng admission cho tới đủ Ack |
| Old worker/response sau epoch mới | Reject binding/epoch, append stale audit; không sửa gate/proof/state mới |
| Legacy hoặc source coverage chưa được triển khai | Intent không Ready; Retire completion disabled/blocked, không bỏ qua nguồn |

Deployment gate: chưa cho enable final Retire nếu Maintenance WO/Schedule và Incident writers chưa được chứng minh dùng admission gate, còn replica phiên bản cũ, hoặc legacy chưa xác minh. Coverage version chỉ được advertised sau migration + deploy toàn bộ writers + tests, không phải chỉ một worker mới bật cờ. Runtime identity keys không cấp quyền tự xác nhận coverage của nguồn khác. Mọi bảo đảm giả định database không bị sửa ngoài protocol; restore/downgrade cần hold coordination và tái xác minh epoch/gate, không dùng receipt trước restore một cách mù quáng.

## 13. Test plan bổ sung

Tất cả runtime tests dưới đây **NOT TESTED** trong revision này. Dùng SQL Server LocalDB/SQL Server thật và Equipment gRPC thật, barriers điều khiển thứ tự commit; không dùng InMemory hoặc sleep để kết luận concurrency.

| IDs | Ca bắt buộc và assertion |
|---|---|
| RC-A | Pending source commit trước E1, chưa gửi RPC; E không Completed; source Frozen báo blocker dù R absent |
| RC-B1/B2 | E1 trước source Pending nhưng source Freeze chưa chạy: Pending được capture; Freeze thắng gate trước Pending: writer bị chặn, không insert lén |
| RC-C1/C2 | Register thắng Equipment lock / intent thắng lock; mỗi thứ tự có kết quả SQL và API đúng, không Retired + R Pending/Active mới |
| RC-D | Delay response sau remote Register commit và RPC không tới server; cả hai đều Pending/blocked, query/restart cùng OperationId |
| RC-E | Remote commit rồi local M rollback/commit; verify WO field, occurrence, op, O và R cả hai DB |
| RC-F | CancelRequested trước/during Retire, tombstone commit nhưng M response mất; chỉ hết Pending sau M Aborted bền; không release theo timeout |
| RC-G | Kill/restart ở E1/S1/S2/E2/E3/E4/S3 boundaries; persisted gates/epochs/proofs giữ đúng, không tự mở |
| RC-H | Timeout List/Get/report/seal receipt/CancelAck; không coi là không blocker; 202 Unknown và Audit attempt/result |
| RC-I | Sealed proof xong rồi cố insert Pending mới, reactivate Schedule, import/legacy writer và replica cũ; nếu path bypass thì không được enable final Retire |
| RC-J | Snapshot/query zero trước freeze, race tạo Pending; proof không hợp lệ, scan dưới gate mới được dùng |
| RC-K | Source thiếu/coverage sai, chỉ receipt Maintenance nhưng thiếu Incident, giả report thay source, Admin JWT giả source; đều không Ready |
| RC-L | Schedule/Incident Pending chưa đăng ký, inactive Schedule R Pending/Active, active Schedule; final Retire vẫn bị chặn đúng nguồn |
| RC-M | Intent Cancel-vs-final E3 hai thứ tự; delayed Prepare cũ sau cancel; delayed Cancel cũ sau intent mới; không re-freeze/unfreeze sai |
| RC-N | Hai Admin cùng mở intent/cùng key khác payload; một intent active; Continue không đổi epoch/payload hoặc xóa blocker |
| RC-O | Nguồn mở gate sau E Cancelling nhưng Ack mất, Pending mới trước E mở admission; retry Ack phục hồi, không final Retire bằng proof cũ |
| RC-P | Register replay có R cũ khi intent active được đọc lại, nhưng key/payload/resource mới bị chặn; verified Abort cũ được phép ghi tombstone |
| RC-Q | Retired/Broken status, intent start/cancel/restart không đổi Broken thành Operational; giữ history và mọi registration |
| MAP-1..5 | RPC trước event, event trước RPC, duplicate event, stale version, event trái tombstone; R+I atomic, ACK sau commit, cùng domain transition |
| AUD-1..5 | Mỗi auto attempt có Started/Result hoặc recovery Unknown; Admin Retry/Cancel reason; timeout mỗi lần; stale worker audit append; LastError thay đổi không xóa history |
| MIG-R1..4 | Existing InitialCreate app vs newer fixture được ghi riêng; legacy Up/Down/reapply; guard cả terminal protocol rows; rollback bị chặn giữ nguyên dữ liệu |
| REG | Toàn Module 1/2, Schedule Gateway API, Identity recipient, Equipment gRPC, real Redis và toàn database suite chạy lại khi implementation xong |

Tests phải kiểm count rows, business identities, epochs/fences, registration field, Audit history, unique/check constraints và trace correlation của cả owners. Muốn kết luận “mọi writer được cover” phải có inventory các API/worker/import/legacy paths và test không bypass; kiểm riêng worker WO là chưa đủ.

## 14. Luồng tổng quát đề xuất

```mermaid
sequenceDiagram
 participant A as Admin
 participant E as Equipment DB/API
 participant M as Maintenance worker/DB
 participant I as Incident worker/DB
 A->>E: POST Retire (key, version, reason)
 Note over E: E1: intent Coordinating, close new registration
 E-->>A: 202 IntentId
 M->>E: Get/List intent (no source SQL TX)
 Note over M: S1 freeze gate; source Pending scan
 I->>E: Get/List intent
 Note over I: S1 freeze gate; source Pending scan
 M->>E: Report Blocked or durable Sealed proof
 I->>E: Report Blocked or durable Sealed proof
 Note over E: E2 store authenticated receipts
 alt Missing proof or unresolved blocker
 E-->>A: Coordinating/Blocked, never Retired
 else All sources Sealed and E local checks clear
 Note over E: E3 lock + final checks + Retire/Audit/history
 E-->>A: GET Completed / IsRetired true
 end
```

Không arrow nào ngụ ý SQL transaction xuyên service. Source workers không giữ transaction khi gọi E.

## 15. Những quyết định cần chủ dự án duyệt trước Stage 2

1. **RetirementIntent chặn tiếp nhận bền**: khi intent đang Coordinating/Ready/Cancelling, chặn registration mới của WO/Schedule/Incident; source Frozen chặn persist Pending mới và mutation tạo blocker như Schedule reactivation. Không tự hết hạn. Đây là thay đổi trải nghiệm nghiệp vụ bổ sung, không đổi operational status.
2. **Admin Continue/Cancel RetirementIntent**: role Admin, reason+RowVersion+idempotency; Cancel chỉ mở lại admission sau đủ source cancel acknowledgments. Cancel intent không Cancel/Abort/Release operation hoặc WO.
3. **Pending trước intent nhưng chưa Register**: không cho tạo reservation mới xuyên intent; giữ Pending blocked. Admin có thể hủy intent để tiếp tục công việc hoặc dùng Option A đã duyệt cho WO; Incident/Schedule unresolved tiếp tục chặn khi chưa có quy trình xử lý tương ứng, không tự mở quyền Cancel mới.
4. **Mở rộng phạm vi triển khai**: cần gate/coverage cho cả MaintenanceSchedule và Incident ngoài worker WO, cùng migration/source writer integration và test. Điều này vượt phương án Equipment-only của Stage 2 cũ; phải được duyệt trước code để không âm thầm sửa luồng đã duyệt. Giữ chiều gRPC nguồn→Equipment trong C1.

Nếu không duyệt đủ phạm vi gate/coverage, phương án an toàn là **chưa bật hoàn tất Retire**; không tuyên bố đã bảo đảm D3 Pending chặn Retire trong mọi race của hệ thống hiện tại. Không có source/migration nào được triển khai bởi tài liệu này.
