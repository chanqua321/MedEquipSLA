# D3 — đối soát snapshot có checkpoint

Cập nhật 07/11/2026. Bổ sung thực thi phương án D3 đã duyệt, không thêm quyền tự Abort/Release hoặc thay source admission gate. Không thay schema: dùng Outbox, Inbox, AuditEntries và ReconciliationCheckpoints hiện có.

## Luồng và transaction

Maintenance/Incident RegistrationSnapshotExporter chụp inventory trong local Serializable transaction: aggregate hiện hữu + các operation còn lại, kể cả Pending, CancelRequested, Aborted và legacy unknown. Source application lock ngăn hai exporter tạo manifest trùng; Audit + toàn bộ page Outbox commit cùng giao dịch. Không gọi mạng trong transaction này. Mỗi snapshot gồm tối đa 100 item/trang, manifest hash, page hash, tổng số item và CapturedAtUtc bền; Outbox dispatcher retry độc lập sau restart. Capture kiểm tra mỗi 30 giây, tối thiểu 5 phút giữa snapshot; không tạo mới khi Outbox snapshot cũ chưa publish.

Outbox → Redis medequip:registration:snapshots:v1 → Equipment RegistrationSnapshotConsumer. Consumer kiểm producer/binding/hash, dùng SQL application lock theo source để serialize checkpoint. Chỉ xử lý trang đúng cursor; trang đến trước chờ Pending để reclaim, trang trùng/cũ không ghi đè tiến độ. Cursor lưu số trang và số item đã xử lý, hoàn tất phải khớp manifest.

Mỗi positive proof được apply qua lifecycle consumer/registration store có transaction riêng, Inbox/version/binding guard. Checkpoint + page Inbox + Audit chỉ commit sau tất cả positive proof của trang. Crash sau apply proof trước checkpoint commit: phát lại trang không lặp thay đổi vì EventId xác định và SourceVersion. Không có distributed transaction; không có network trong transaction Equipment.

Committed Incident/WorkOrder được áp dụng với version và registration binding. Schedule Inactive vẫn giữ Active registration. Schedule chỉ được đối soát Released khi source đã có RegistrationReleasedAtUtc của quy trình kết thúc đã duyệt. Pending/Unknown chỉ ghi nhận inventory, không tự commit hồ sơ hoặc release; Aborted inventory không phải lệnh Abort Equipment.

## Concurrency và lỗi

Retire vẫn serialize trên Equipment row và cần hai source Sealed đúng intent/epoch. Checkpoint Completed chỉ nghĩa đã duyệt đủ inventory; không phải proof hết blocker. Không infer release từ absence. Event mới có SourceVersion lớn hơn thắng snapshot cũ; không hồi sinh Released/Aborted/Retired. WO/Schedule không tự đưa Broken về Operational.

XAUTOCLAIM phục hồi PEL. Sai thứ tự chờ trang trước. Payload lỗi vào DLQ; lỗi xử lý thực retry tối đa 8 deliveries, Audit Retry/Failed và checkpoint Failed nếu binding đúng. Snapshot mới hơn có thể chạy lại từ inventory nguồn; snapshot Failed không được báo Completed. SQL không khả dụng thì giữ Pending; LastError không thay lịch sử Audit. GET /api/equipment-reconciliation chỉ Admin xem tiến độ.

Incident/Schedule Pending chưa có hồ sơ vẫn cần retry request gốc với phiên hiện hành hoặc Admin controlled cancel. Worker không mạo danh người dùng để commit create. WorkOrder có service-only protocol riêng, tự retry/fence theo budget đã duyệt.

## Kiểm chứng và giới hạn

RegistrationSnapshotTests: 7 cases thực, nằm trong regression 157/157 SQL Server 2022 + Redis PASS. Bao phủ source concurrent capture, Audit failure rollback, hash/binding/coverage, duplicate/out-of-order/restart, no absence release, Inactive schedule, mất lifecycle event, positive Closed Incident/terminal WO, Broken preservation, lỗi commit checkpoint sau proof và hosted consumer/ACK thật.

Capture Serializable giữ source read locks, cần đánh giá tải khi dữ liệu lớn; chưa benchmark production. Snapshot không xóa orphan/legacy unknown; Admin phải xử lý theo protocol, không sửa DB trực tiếp để hết blocker. Không có TTL release hoặc tự dọn tombstone.
