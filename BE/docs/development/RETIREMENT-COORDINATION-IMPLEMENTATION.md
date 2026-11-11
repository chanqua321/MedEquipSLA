# Retirement source gates and coordination implementation

Ngày kiểm chứng: 06/11/2026. Repository hiện hữu duy nhất; không clone, không commit/push.
Tài liệu này cập nhật tiến độ Stage 2.4–2.6; không tuyên bố hoàn tất Module 3 hoặc toàn Backend.

## Đã triển khai

- Maintenance và Incident có gate riêng trong database của mình. EquipmentId/IntentId là logical references, không FK xuyên DB.
- Writer tạo Schedule, reactivation Schedule và tạo Incident lấy gate trước khi tạo blocker mới.
- Pending lưu EquipmentId và immutable versioned payload. Legacy NULL không bị backfill bằng suy đoán.
- Incident tạo Pending trong prepare transaction, gRPC ngoài transaction, final transaction lấy gate và operation lock trước commit Incident/Activity/Audit/Outbox.
- Source store Freeze và kiểm blocker dưới cùng admission lock; chỉ Seal sau khi không còn Pending, hồ sơ mở hoặc dữ liệu unknown. Inactive không tự Release Equipment registration.
- Cancellation giữ epoch tombstone; delayed Prepare không freeze lại cùng intent đã hủy. Epoch cũ không mở gate mới. Gate không có TTL release.
- Equipment có RPC ListRetirementIntents, GetRetirementIntent và ReportRetirementSourceState.
- Authentication retirement dùng scheme riêng, key riêng từng source; service identity được suy ra từ credential. JWT Admin hoặc WorkOrder key không thay thế retirement key.
- Admin có Begin, GET, Continue và Cancel. Begin/Continue khi chưa final trả 202; Completed/Cancelled trả 200. Continue/Cancel cần RowVersion và Idempotency-Key.
- Final Retire và Cancel lấy cùng Equipment SQL row lock. Final yêu cầu hai source Sealed, không registration Pending/Active và không blocking projection; thiết bị, intent, history, Audit và idempotency cùng local transaction.
- Source worker chủ động poll/report Equipment, đúng chiều Source → Equipment trong C1; Equipment không gọi source hoặc giữ transaction khi network.
- Worker ghi QueryAttempt/QueryResult, từng CoordinationAttempt/Result hoặc Unknown trong Audit. Tối đa 8 action attempts mỗi intent/ContinueCount/state, backoff tăng đến 300 giây; exhausted ghi NeedsIntervention. Admin Continue tạo series mới. Passive status polling vẫn hoạt động để thấy terminal/cancelling decision; không tự release hoặc abort.
- Workers tắt nếu Workers:Enabled hoặc Workers:Retirement:Enabled không bật. Compose cấu hình key source riêng và bật retirement worker.

## API

| Method | Route | Request |
|---|---|---|
| POST | /api/equipment/{equipment}/retirement-intents | reason; header Idempotency-Key UUID |
| GET | /api/equipment/{equipment}/retirement-intents/{intent} | Admin |
| POST | /api/equipment/{equipment}/retirement-intents/{intent}/continue | reason, RowVersion base64; Idempotency-Key |
| POST | /api/equipment/{equipment}/retirement-intents/{intent}/cancel | reason, RowVersion base64; Idempotency-Key |

Representation: Id, EquipmentId, Epoch, State, Reason, RowVersion, Receipts và ContinueCount.
Receipt có source, coverage, version, proof/ack và received time. Không trả service key.
Continue có thể vẫn Ready nếu Equipment-local blocker còn tồn tại; Ready không đồng nghĩa Retired.

## Migration và application database

- Maintenance: 20261106073414_AddSourceRetirementGate.
- Incident: 20261106073418_AddSourceRetirementGate.
- Mỗi migration thêm gate table, nullable EquipmentId/RequestPayloadJson vào IdempotencyRequests, index và constraints.
- InitialCreate không sửa. Down ném 51031 trước DDL nếu có gate hoặc protocol linkage/payload; không drop dữ liệu protocol.
- Owner-specific technicalTableOverrides trong erd-model.json thể hiện hai cột mới chỉ ở hai source. Equipment/Identity/Notification không có các cột này.
- SQL script owner đã export lại để review. Không chạy script trên application DB.
- Application DB vẫn giữ trạng thái đã ghi nhận InitialCreate; tất cả migrate/test trong lượt này chỉ dùng catalog MedEquip_<Owner>_Test_<GUID> do fixture tạo và sở hữu.
- Không start API auto-migrate, không reset database, không dùng EnsureCreated.

## Evidence thực tế

| Nhóm | Kết quả đã chạy |
|---|---|
| Source gate + exact EF/SQL schema, Up/Down/reapply | 21/21 PASS, 0 skipped |
| Retirement API/gRPC, source DB proof, race Cancel/final, injected Audit failure, WorkOrder protocol, scheduler và schema regression | 50/50 PASS, 0 skipped |
| Source coordinators hai owner, SQL + gRPC, restart/cancel, credentials sai, retry budget bền và Admin Continue | 6/6 PASS, 0 skipped |
| Current solution regression sau các thay đổi | 104 PASS, 4 SKIP do Redis chưa cấu hình trong lượt chạy; 0 FAIL, TRX backend-current-regression.trx |
| Chạy riêng 4 test bị skip với Redis 8.4.2 test port 16379 DB15 và SQL thật | 4/4 PASS, 0 skipped, TRX backend-redis-regression.trx; instance test đã shutdown NOSAVE |
| Release solution build | PASS, 0 warnings, 0 errors; solution test build cũng exit 0 |

TRX nằm ở tests/MedEquipSLA.Database.IntegrationTests/TestResults:
stage2.4-source-gates.trx, retirement-and-scheduler-regression.trx, stage2.5-source-worker.trx.
Các bộ có test trùng nhau, không cộng số này thành tổng số test độc lập.
Các thay đổi sau mỗi lần chạy cần được kiểm lại trong regression cuối; không coi evidence cũ là test code mới.

## Vẫn chưa hoàn tất

- WorkOrder generation chưa được bật hoặc triển khai đủ durable source operation/lease/fence/Abort reconciliation.
- WorkOrderCreated consumer/Inbox, lifecycle WorkOrder, ảnh nghiệm thu và Calibration chưa hoàn tất.
- Full Stage 2.7 matrix, Redis WorkOrder events, full-stack production deployment và toàn BE chưa PASS.
- Automatic startup host lifetime của retirement worker chưa có process-kill test; tests hiện gọi coordinator thực qua scope/context mới, real gRPC/SQL.
- Không có SQL Server container/Docker runtime evidence mới; LocalDB không thay bằng chứng Docker.
- Quyền kết thúc Schedule vĩnh viễn và Abort Pending Incident/Schedule chưa có quyết định bổ sung. Không triển khai quyền này; unresolved registrations tiếp tục chặn Retire.
- Pending Incident/Schedule bị Equipment từ chối vẫn giữ dấu vết, không tự coi Aborted.
