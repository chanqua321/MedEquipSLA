# Coding Conventions

Quy ước cho feature mới; không yêu cầu format lại toàn bộ source scaffold trong PR tài liệu. `.editorconfig` dùng UTF-8, LF, 4 spaces cho code, 2 spaces cho JSON/YAML. Build bật nullable và TreatWarningsAsErrors.

| Chủ đề | Quy ước |
|---|---|
| C# naming | PascalCase class/method/property; camelCase local/parameter; I prefix interface; async I/O có Async và CancellationToken |
| File/feature | Tên file khớp type chính; feature theo use case trong Application.Features; Command/Query/Handler/Validator rõ |
| Dependencies | Domain độc lập; Application chứa abstractions; Infrastructure implement; API composition; không cross-service project refs |
| Data contract | DTO/protobuf/event tách EF entity; không serialize entity/nav graph trực tiếp |
| CQRS | Command có side effect; Query không ghi dữ liệu; không bắt buộc Event Sourcing hoặc DB đọc riêng |
| EF | Cấu hình Fluent explicit; decimal precision; NoAction nội bộ; SQLServer rowversion; không EnsureCreated khi có migrations |
| Query | Scope backend trước count/page, projection/AsNoTracking, sort allowlist, stable tie-breaker; không client-selected actor/scope |
| Thời gian | DateTimeOffset UTC ở persistence/business timestamp, UI Asia/Ho_Chi_Minh; DateOnly cho ngày không có giờ |
| Tiền/measurement | decimal theo precision đã duyệt, không float cho tiền |
| Authorization | Xác minh token và quyền hiện hành; role + department/assignment; deny/defer nếu quyền chưa xác minh |
| Validation | Syntax tại DTO/validator, invariant tại Domain/Application, DB constraint phòng vệ; invoke validator thực sự |
| Errors | ProblemDetails với stable code/traceId, không stack trace; 409 concurrency, 422 semantic, 503 dependency theo contract khi implement |
| Logging | ILogger structured fields, EventId/CorrelationId/operation; không log password/JWT/refresh token/private key/connection secret |
| Events | EventId ổn định, schema và aggregate version riêng; producer/consumer ownership rõ; không giả định exactly-once |
| Transactions | Aggregate + audit + Outbox cùng local transaction; Inbox + consumer data cùng local transaction; ACK sau commit |
| Workers | Idempotency, lease/retry có giới hạn/logging/cancellation; enabled flag không thay kiểm chứng nghiệp vụ |
| Secrets | Env/secrets manager; chỉ commit mẫu rỗng; key/cert thật ở ngoài repo; .env không tự đọc bởi .NET |
| Tests | Tình huống hành vi có ý nghĩa, SQL Server thật cho SQL-specific behavior; không fake PASS bằng skeleton |
| Git | Conventional Commits, diff nhỏ theo use case; không lẫn format toàn repo; PR có evidence và giới hạn |

Không đặt mã nguồn nghiệp vụ ở Gateway/BuildingBlocks, không dùng public setters để frontend tự chọn status/SLA/actor/version. Tên role hiện lưu DepartmentStaff; UI có thể hiển thị Staff. API version/schema change cần backward compatibility hoặc migration plan.

Các proposed contract trong API Documentation phải được chốt trước khi dùng như public stable API. Không tự sửa D1–D4 hoặc ràng buộc Delete/freshness bằng “best practice” không được phê duyệt.
