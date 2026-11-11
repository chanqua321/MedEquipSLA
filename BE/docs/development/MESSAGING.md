# Background processing và Redis Streams

Cập nhật 07/11/2026. Producer ghi aggregate + Audit + Outbox trong cùng local SQL transaction. Dispatcher claim lease, XADD envelope, sau đó đánh dấu Published. Crash sau XADD trước SQL mark có thể phát lại: consumer dùng EventId/Inbox cùng transaction với thay đổi, ACK sau commit. XAUTOCLAIM khôi phục PEL sau restart; out-of-order xử lý bằng SourceVersion, fencing và binding.

| Stream | Producer → consumer |
|---|---|
| medequip:incident:lifecycle:v1 | Incident → Equipment projection/registration |
| medequip:workorder:lifecycle:v1 | Maintenance → Equipment projection/registration |
| medequip:maintenance:reminders:v1 | Maintenance → Notification |
| medequip:identity:access:v1 | Identity → Maintenance/Incident assignment Inbox và Notification connection revocation |
| medequip:identity:sessions:v1 | Identity → Notification thu hồi đúng session |
| medequip:registration:snapshots:v1 | Maintenance/Incident → Equipment checkpoint/positive proof |

Tên stream reminder/user/mail/lifecycle khác lấy từ constants tại [Contracts.Events](../../src/Contracts/MedEquipSLA.Contracts.Events); không tạo stream scaffold để giả làm producer nghiệp vụ. Có DLQ cho envelope lỗi và retry budget tùy consumer; dependency unknown không tự release thiết bị. Snapshot pages chờ đúng cursor, không coi trang đến sai thứ tự là thành công.

Workers:Enabled bật hosted scheduler/scanner/outbox/consumer. Maintenance thêm WorkOrderGeneration:Enabled; retirement thêm Retirement:Enabled. Compose bật các worker cần thiết, Email:Enabled vẫn opt-in. [Runbook](BACKEND-RUNBOOK.md) giải thích cấu hình. Scheduler tạo occurrence đúng hạn/catch-up, Reminder unique, WO chỉ đến kỳ; SLA alerts unique, không chỉ log tick.

## Kiểm tra producer/consumer thật

Chạy [integration tests](../../tests/MedEquipSLA.Database.IntegrationTests/README.md) với Redis test DB riêng và SQL test catalogs. MaintenanceReminderInboxTests/RedisReminderTests kiểm retry/duplicate/downtime; UserAccessProjectionTests và SessionRevocationBrokerTests kiểm propagation; RegistrationSnapshotTests kiểm source capture → Outbox → Redis → hosted consumer → checkpoint/Inbox/ACK. Tests không FLUSHDB database ứng dụng.

Khi demo: tạo/sửa hồ sơ qua API, đối chiếu EventId/CorrelationId trong Outbox nguồn, XINFO/XPENDING ở Redis, Inbox và trạng thái ở consumer. Ngừng publisher/consumer rồi chạy lại phải giữ dữ liệu và phục hồi. Không ACK thủ công để làm mất Pending chưa commit.

SMTP at-least-once, stable Message-ID và lịch sử từng attempt; Sent chỉ sau SMTP ACK. SignalR chỉ là hint sau SQL commit, reconnect đọc REST. Compose hiện chạy một Notification replica; chưa triển khai SignalR backplane nhiều replica. [Snapshot protocol](D3-SNAPSHOT-RECONCILIATION.md), [tracker](BACKEND-COMPLETION-TRACKER.md).
