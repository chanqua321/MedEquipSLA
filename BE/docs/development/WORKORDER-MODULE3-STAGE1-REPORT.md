# Module 3 — Stage 1 report
Ngày 05/11/2026.

## 1. Mục tiêu
Hoàn thiện thiết kế contract/storage/concurrency trước khi tạo migration. Chi tiết trong [WORKORDER-MODULE3-STAGE1-DESIGN.md](WORKORDER-MODULE3-STAGE1-DESIGN.md); source audit trong [Stage 0](WORKORDER-MODULE3-STAGE0-AUDIT.md).

## 2. File
Tạo docs/development/WORKORDER-MODULE3-STAGE1-DESIGN.md và docs/development/WORKORDER-MODULE3-STAGE1-REPORT.md. Không sửa entity, proto, API hoặc database tại Stage 1.

## 3. Quyết định kỹ thuật đã thiết kế
- Bốn RPC service-only Register/Query/Confirm/VerifyAbort; key riêng, không giả Admin JWT.
- Business key OccurrenceId, OperationId/payload/hash bất biến; WorkOrderId và OccurrenceId không bị đánh đồng.
- Reuse IdempotencyRequests và EquipmentUseRegistrations, không thêm database hoặc bảng mới.
- Source lease/fence độc lập SourceVersion; commit kiểm expiry, owner, token và RowVersion.
- Cancel intent bền trước network, ngăn generation commit ngay tại source; Aborted chỉ sau tombstone xác nhận ở Equipment.
- Retire dùng khóa Equipment giống Register, chặn mọi unresolved registration và giữ quy tắc Incident/Schedule.
- Equipment consumer của WorkOrderCreated xác nhận registration qua Inbox; Scheduled không đặt UnderMaintenance.
- Migration additive owner-specific, guard Down không làm mất proof/fencing của protocol đã bắt đầu; giữ dữ liệu legacy thiếu proof ở trạng thái chưa xác minh.
Không thay đổi quyết định nghiệp vụ đã duyệt.

## 4. Kiểm tra thiết kế đã thực hiện
| Nội dung review | Kết quả review | Chứng cứ trong design |
|---|---|---|
| Identity/idempotency | PASS | §1, §3, §5/6: stable business key, immutable payload/hash, source/resource uniques |
| State/late Abort | PASS | §4, §7: local durable cancel intent trước RPC, terminal state guards |
| Lock order | PASS | §7: source Schedule→Occurrence→Operation; claim operation-only; Equipment→Registration |
| Crash after remote success | PASS | §7/9: Pending bền, query same operation, không timeout-release |
| Crash after source commit | PASS | §10: durable Outbox→Equipment Inbox confirm; RPC không phải cơ chế phục hồi duy nhất |
| Migration compatibility | PASS | §5/6/11: nullable legacy, không fabricated backfill, Down guard |
| Module 1/2 compatibility | PASS | §7/9/10: worker/stream độc lập, không sửa Reminder hay công thức D2 |
| RPC/SQL runtime mới | NOT TESTED | Chưa implementation; PASS review không thay runtime tests |
| Migrations mới | NOT TESTED | Chưa tạo/apply migration |

PASS ở bảng này chỉ cho việc review tài liệu đã thực hiện, không kết luận behavior của code. Build và regression hiện tại xem Stage 0 report; không cộng chúng thành số test Module 3.

## 5. Rủi ro và điểm cần xác minh ở Stage 2
- Legacy registration/WO thiếu proof phải fail closed khi thanh lý; không suy ra an toàn từ database rỗng tại một thời điểm.
- Guard rollback phải kiểm tra trước khi drop cột; không rollback ứng dụng đang chạy worker.
- Active remote nhưng Pending source là tình trạng phải đối soát; không cho Admin Cancel hồi tố một WO đã Committed.
- Abort trước Register cần tombstone bền có FK Equipment. Missing equipment không thể tạo tombstone giả.
- Service-only authorization phải được kiểm bằng real gRPC, gồm Admin JWT không có service credential bị từ chối.
- Tất cả mutation Equipment phải dùng cùng lock, kể cả Inbox consumer. Không thêm source verification network trong local SQL transaction.
- Ứng dụng Maintenance/Incident hiện còn migration pending từ module trước; việc nâng schema ứng dụng cần báo cáo riêng với fixture tests.

Không có quyết định nghiệp vụ mới cần chủ dự án phê duyệt tại Stage 1.

## 6. Gate Stage 2
Thiết kế đã được self-review trước khi viết migration. Stage 2 tiếp theo gồm proto, service auth, registration/hash/fence/tombstone và Retire minimal cùng migration Equipment. Chỉ sau khi SQL Server + Equipment gRPC tests của Stage 2 chạy đạt mới triển khai persistence Maintenance ở Stage 3. Chưa đánh dấu hoàn thành Module 3. Không commit/push.
