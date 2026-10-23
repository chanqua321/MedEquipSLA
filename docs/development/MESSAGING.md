# Background processing và Redis Streams

Hiện trạng: RedisEventPublisher có adapter XADD, IntegrationEvent có versioned envelope; Outbox/Inbox đã có owner-local EF mapping và InitialCreate theo ERD được duyệt. Scheduler/scanner/NotificationConsumer vẫn là hosted-service skeleton, disabled mặc định. Chưa có producer/consumer nghiệp vụ, atomic command/event write, retry/DLQ, Inbox processing hoặc email/SignalR. [API/events](../API-DOCUMENTATION.md), [schema](../DATABASE-DOCUMENTATION.md).

## Kiểm tra hạ tầng khi Docker Engine sẵn sàng

```sh
docker compose up -d redis
docker compose exec redis redis-cli PING
docker compose exec redis redis-cli XGROUP CREATE medequip:scaffold:v1 scaffold-debug 0 MKSTREAM
docker compose exec redis redis-cli XADD medequip:scaffold:v1 '*' kind ScaffoldProbe
docker compose exec redis redis-cli XREADGROUP GROUP scaffold-debug debug COUNT 1 STREAMS medequip:scaffold:v1 '>'
docker compose exec redis redis-cli XPENDING medequip:scaffold:v1 scaffold-debug
```

Shell phải quote `*` và `>` như trên. Group đã tồn tại trả BUSYGROUP, không cần tạo lại. XACK bằng ID thực nhận sau khi kiểm tra. Các lệnh này chỉ chứng minh Redis, không thay producer/consumer app.

## Backlog implementation/verification

Producer: commit aggregate + audit + Outbox cùng SQL transaction; dispatcher lease/XADD/mark-published. Consumer: XREADGROUP + durable Inbox cùng local transaction; ACK sau commit; XAUTOCLAIM pending, bounded retry/backoff/DLQ, out-of-order theo AggregateVersion. Không shared database. Persistent AOF có durability tradeoff; không hứa exactly-once SMTP. Stream/group/payload nghiệp vụ chưa chốt; medequip:scaffold:v1 chỉ là probe thủ công.

Test bằng thao tác nghiệp vụ thực: log EventId/correlation ID ở producer và consumer; broker stop/start sau commit phải replay; gửi lại EventId không tạo notification trùng. Sau đó mới đánh dấu tiêu chí tương ứng PASS. Workers bật qua Workers__Enabled; trước implementation bật true chỉ log tick, không được dùng làm evidence job nghiệp vụ.
