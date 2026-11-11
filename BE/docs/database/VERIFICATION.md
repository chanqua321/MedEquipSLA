# Code First verification — 23/10/2026

Phạm vi: ERD v0.1 đã duyệt, entities/Fluent API, migrations và SQL Server persistence. Kết quả dưới đây là kiểm tra thực tế, không đại diện cho hoàn thành Final Assignment.

## Môi trường và kết quả

- .NET SDK 10.0.402, EF Core / dotnet-ef 10.0.13.
- SQL Server LocalDB 2025 **17.0.4025.3**, `(localdb)\MSSQLLocalDB`, Windows authentication; cả 5 DB compatibility **160**, trạng thái ONLINE.
- Kiểm tra trước khi tạo: chưa có database mang tên `MedEquip_*`. Tạo bằng `dotnet ef database update`, không dùng SQL thủ công để thay business migrations.
- Build solution sau khi thêm entities/migrations: **0 warnings, 0 errors**.
- Mỗi DB có một `InitialCreate` đã apply. Chạy lại SQL idempotent của cả 5 DB thành công; chạy lại wrapper migration báo database already up to date.
- `dotnet ef migrations has-pending-model-changes`: cả 5 context không có model changes chưa được migration lưu lại.
- Không có SQL FK xuyên database; mọi FK trong DB đều enabled, trusted, NO ACTION.

| Database | Bảng ứng dụng | Tổng cả history | Cột theo ERD | FK | Check constraints | Schema / behavior |
|---|---:|---:|---:|---:|---:|---|
| MedEquip_Identity | 10 | 11 | 77 | 8 | 7 | PASS / PASS |
| MedEquip_Equipment | 11 | 12 | 127 | 7 | 19 | PASS / PASS |
| MedEquip_Maintenance | 10 | 11 | 129 | 6 | 25 | PASS / PASS |
| MedEquip_Incident | 9 | 10 | 115 | 6 | 26 | PASS / PASS |
| MedEquip_Notification | 7 | 8 | 67 | 2 | 10 | PASS / PASS |
| Tổng | **47** | **52** | **515** | **29** | **87** | **10 suites PASS** |

Schema assertions đối chiếu đầy đủ tên bảng/cột, SQL type, nullability, PK (kể cả thứ tự khóa ghép), FK/cặp cột và history với JSON ERD đã duyệt. Kiểm tra FK/check constraints được trusted, index không disabled/hypothetical.

## Behavior đã kiểm tra trên SQL Server

- Identity: unique normalized email/token hash; không thể xóa user đang có refresh token.
- Equipment: FK khoa hợp lệ, retirement flag/status nhất quán; rowversion thay đổi sau update, stale write theo rowversion không cập nhật được. Outbox chỉ nhận JSON hợp lệ; Inbox và idempotency scope/key từ chối duplicate. Equipment + Outbox cùng transaction được rollback, không còn fixture.
- Maintenance: duy nhất một occurrence cho mỗi kỳ, một WorkOrder cho mỗi occurrence; cho phép nhiều manual WorkOrder có OccurrenceId NULL. Tối đa một attempt Accepted trên mỗi WorkOrder. Calibration không chấp nhận Passed sai threshold.
- Incident: version SLA phải tồn tại theo composite FK; deadline phải khớp snapshot duration; tối đa một resolution attempt Accepted.
- Notification: cùng event/recipient/kind không tạo notification thứ hai; một notification không tạo hai email delivery.
- Tất cả fixture rollback khi thành công hoặc lỗi. Chỉ 3 Identity role hệ thống được seed bởi migration; không tạo user/mật khẩu hay business demo data.

Chạy lại từ root bằng `./scripts/Test-Databases.ps1`. SQL nguồn nằm tại `tests/database/`; hướng dẫn đầy đủ tại [SETUP](SETUP.md).

## Giới hạn còn lại

- Docker Desktop đã được khởi động nhưng Docker Linux Engine pipe chưa khả dụng; chưa pull/build/run full Compose hoặc xác minh SQL Server container 2022. LocalDB không thay thế evidence Docker deployment. Compatibility 160 không phải bằng chứng engine 2022 đã chạy.
- Credential isolation của 5 runtime login và deployment identity trên Compose chưa được kiểm thử; LocalDB dùng Windows identity hiện tại.
- Chưa triển khai factories/domain transitions, CRUD/CQRS handlers, login/authorization, workers, Outbox dispatcher/Inbox consumers hoặc retry. Các entity hiện là persistence shape có private setters; lớp nghiệp vụ cần thêm invariant/method/factory khi triển khai use case.
- Cross-service registration/reconciliation, SLA outcome cuối cùng, calibration rework và overlapping SLA policy intervals cần Application-level transaction/protocol tests; SQL constraints chỉ bảo vệ phần đã nêu. Schema tồn tại không chứng minh producer/consumer hoặc scheduler hoạt động.

Các giới hạn này được giữ IN PROGRESS/BLOCKED trong [PRN232 checklist](../assignment-compliance/PRN232-CHECKLIST.md). Chỉ EF Core, SQL Server và database migrations có evidence đủ cho tiêu chí tương ứng ở giai đoạn này.

## Bổ sung EF/migration cycle

Lượt [finalization audit](FINALIZATION-AUDIT.md) thêm .NET tests cho model, exact SQL indexes/filters, check metadata, Up→0→Up, EF concurrency và transaction rollback. Xem [cách chạy](../../tests/MedEquipSLA.Database.IntegrationTests/README.md). Schema/InitialCreate của lượt trước được giữ nguyên.
