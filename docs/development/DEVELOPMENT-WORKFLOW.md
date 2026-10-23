# Development Workflow

Làm theo một use case có BR/acceptance criteria, không triển khai cả service chỉ để thay skeleton. [Solution Structure](../architecture/SOLUTION-STRUCTURE.md) ánh xạ API → Services/CQRS → Repository → EF/SQL. [Coding conventions](CODING-CONVENTIONS.md), [Git workflow](GIT-WORKFLOW.md).

## 1. Chốt phạm vi

Đọc Business/API/Database documentation và README service. Xác định actor, scope, state trước/sau, lỗi, event, idempotency và AC. D1–D4 đã duyệt; authorization freshness, fencing/snapshot protocol và Delete semantics còn cần quyết định. Không tự thay các quyết định này trong PR implementation.

## 2. Entity và persistence

1. Thêm/sửa aggregate/value object trong `<Service>.Domain`, factory/method kiểm tra invariant; không public setter cho trạng thái hệ thống.
2. Thêm Fluent API trong Infrastructure.Persistence.Configurations và DbSet của context owner. Length/nullability/precision/index/check/rowversion rõ ràng; FK chỉ trong DB owner, NoAction theo thiết kế.
3. Technical entity ở Infrastructure khi thích hợp; không đưa EF attributes/package vào Domain. Không shared DbContext/entity giữa service.
4. Với schema đã có, giữ migration cũ; thêm migration tên mới sau review model. Không dùng EnsureCreated.

## 3. Repository, Command và Query

- Application.Abstractions: repository theo aggregate; read adapter interface trả DTO. Tránh generic CRUD repository chỉ để đủ layer.
- Infrastructure.Persistence: implement repository/query adapter; query DTO projection/AsNoTracking, sort allowlist/page và scope ở server.
- Application.Features/<UseCase>: Command/Query, result DTO, validator, handler. Dùng interfaces CQRS hiện có, không mặc định cần MediatR.
- Command xác minh quyền hiện hành/assignment/department, load aggregate, gọi domain method, ghi audit và Outbox cùng **một local transaction**. Retry cần stable OperationId/key/hash và protocol phù hợp.
- Query áp dụng scope trước search/count/page; DTO không lộ hash/token hoặc snapshot ngoài khoa. Handler không nhận actor/deadline từ client như nguồn quyết định.
- Đăng ký DI tại AddInfrastructure và API composition; validator phải được gọi tại application boundary, không chỉ AddValidatorsFromAssembly rồi coi validation đã hoạt động.

## 4. Endpoint và contracts

Map Minimal API → handler, auth/policy rõ, DTO HTTP tách entity. Ghi status/error code/BR/owner vào API Documentation và OpenAPI. Hiện handler exception mapping chỉ 501/500; nếu thêm feature cần map validation/state/concurrency/dependency có test, không giả định đã có mapping.

Nếu thêm gRPC: update proto có version/backward compatibility, server/client DI, deadline/cancellation/auth, tests transport và business. Nếu thêm event: typed payload/schema version, producer/consumers, stream/group, full snapshot/version, retry/DLQ/duplicate policy; envelope hiện tại chỉ là khung.

## 5. Migration

Từ root, dùng ví dụ Equipment; đổi đủ project/context khi làm owner khác:

```sh
dotnet tool restore
dotnet ef migrations add DescribeYourChange --project src/Services/Equipment/Equipment.Infrastructure --startup-project src/Services/Equipment/Equipment.Api --context EquipmentDbContext --output-dir Persistence/Migrations
dotnet ef migrations script --idempotent --project src/Services/Equipment/Equipment.Infrastructure --startup-project src/Services/Equipment/Equipment.Api --context EquipmentDbContext --output docs/database/migrations/Equipment.sql
```

Review SQL, snapshot, data loss/constraint/backfill. Update ERD/dictionary/schema assertions. Apply bằng deployment credential riêng theo [SETUP](../database/SETUP.md); test DB rỗng và nâng cấp có dữ liệu liên quan. Không drop DB/volume để vượt lỗi. Runtime identity không có DDL và không tự migration tại mọi replica.

## 6. Kiểm tra và PR

```sh
dotnet restore MedEquipSLA.sln --locked-mode
dotnet build MedEquipSLA.sln -c Release --no-restore
dotnet test MedEquipSLA.sln -c Release --no-build
```

Đã có Database.IntegrationTests: 5 model + 8 SQL runtime cases; thiếu SQLSERVER_TEST_CONNECTION thì SQL cases Skip/BLOCKED. [Cách chạy](../../tests/MedEquipSLA.Database.IntegrationTests/README.md). Các feature nghiệp vụ vẫn cần acceptance tests riêng, không suy từ schema tests. SQL checks: `pwsh ./scripts/Test-Databases.ps1 -Service Equipment` trên LocalDB dev/test đã migrate; fixtures rollback. Frontend: `npm ci`, `npm run build` trong src/Web. Không dùng EF InMemory để kết luận rowversion/FK/SQL transaction đúng.

Review cases: role/department/assignment, invalid input/state, stale version, repeated request/event, dependency timeout, rollback/commit Outbox, restart worker. Không mirror implementation bằng tests hình thức. Update README/status/PRN232 matrix bằng evidence thực, ghi rõ phần chưa chạy. Push feature và mở PR, không force push main.
