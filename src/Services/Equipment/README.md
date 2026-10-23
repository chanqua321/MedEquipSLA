# Equipment Service

## Service Owner và trách nhiệm

Owner boundary: **Equipment**, không phải tên thành viên. Team member chưa phân công; điền tại [Team Responsibilities](../../../docs/development/TEAM-RESPONSIBILITIES.md).

Khoa/phòng, loại thiết bị, thiết bị, trạng thái vận hành; giữ projection, registration và reconciliation checkpoint. Chỉ owner này được đổi trạng thái Equipment. Đọc [Business Documentation](../../../docs/BUSINESS-DOCUMENTATION.md) và [API Documentation](../../../docs/API-DOCUMENTATION.md) trước feature code.

## Database Ownership

`MedEquip_Equipment`, `EquipmentDbContext`, **11 bảng ứng dụng + một history**; có service-owned InitialCreate. [ERD/data dictionary](../../../docs/database/services/Equipment.md), [Database Documentation](../../../docs/DATABASE-DOCUMENTATION.md), [verification](../../../docs/database/VERIFICATION.md). Không truy vấn/ghi database service khác; reference ngoài owner là ID logic, không FK/JOIN.

Schema đã kiểm chứng trên SQL Server LocalDB compatibility 160; môi trường nhóm SQL Server 2022 trong Compose chưa runtime-verified. Private setters/persistence shape không chứng minh workflows đã implement.

## Project Structure và API – Services – Repository

```text
Equipment.Api/             Program.cs, endpoints, composition/config
Equipment.Application/     Features/Command/Query/Handler/Validator; Abstractions
Equipment.Domain/          Entity/value object; không framework infrastructure
Equipment.Infrastructure/  Persistence context/config/repository/migrations
                       Messaging, technical entities, Workers nếu có
```

API → Application Services/CQRS handlers → repository/read abstractions → Infrastructure EF implementation → SQL. ScaffoldCommand/ScaffoldQuery vẫn ném NotImplemented; validator được đăng ký không đồng nghĩa command validation đã được gọi. Không generic repository hình thức hoặc shared entity/context.

## API và gRPC

REST trực tiếp: `http://localhost:5102`, Gateway prefix `/equipment`.

| Method / route | Auth / kết quả hiện tại |
|---|---|
| GET / | Anonymous, 200 service/phase/businessImplemented=false |
| GET /health/live | Anonymous, 200 Healthy; không test SQL/Redis |
| GET /api/equipment | JWT hợp lệ, chưa role/department policy; thiếu token 401, sau auth handler 501 |
| GET /swagger/index.html | Development, Swagger UI |
| GET /swagger/v1/swagger.json | Development, OpenAPI JSON |

EquipmentLookup ở HTTP/2 port 5103 (container 8081), độc lập REST port 5102. GetServiceInfo chạy Development; GetEquipment có Authorize nhưng nghiệp vụ UNIMPLEMENTED. Registration/department lookup chưa có proto. CRUD/search/filter/sort/pagination và business DTO còn Planned; xem API Documentation để tránh gọi route đề xuất như route đã có. Không có user/token mặc định.

## Events Produced và Consumed

- **Produced:** Equipment status/department/lifecycle events Planned; tên/payload chưa chốt. Có Outbox mapping và Redis XADD adapter, chưa có atomic command write/dispatcher.
- **Consumed:** Incident/Maintenance state/registration events Planned; Inbox mapping có, consumer/reconciliation worker chưa implement.

Envelope có EventId/SchemaVersion/AggregateVersion/correlation; Payload là JSON string. [Messaging guide](../../../docs/development/MESSAGING.md) tách Redis infrastructure probe khỏi app producer/consumer evidence.

## Dependencies và configuration

SQL Server, Redis; Identity authorization freshness và Incident/Maintenance snapshots Planned. Service này không đăng ký BackgroundService trong code hiện tại.

`ConnectionStrings__Database` phải đúng `MedEquip_Equipment`; `ConnectionStrings__Redis` mặc định localhost:6379,abortConnect=false. JWT issuer/audience/public PEM theo hosting; không private key trong repo. Incident/Maintenance dùng `Grpc__EquipmentAddress=http://localhost:5103` native, `http://equipment:8081` container. `.env` chỉ Compose đọc, không tự nạp vào dotnet run. [Environment Setup](../../../docs/development/ENVIRONMENT-SETUP.md).

## Run Commands

Từ repository root, sau tool restore/build:

```sh
dotnet run --project src/Services/Equipment/Equipment.Api --launch-profile Equipment
```

Swagger http://localhost:5102/swagger/index.html. Process/probe hiện không cần business data để start; muốn thao tác SQL phải cấu hình đúng database. Docker: chuẩn bị .env → `docker compose up -d sqlserver redis db-bootstrap` → apply migrations riêng → `docker compose up -d --build equipment`. Bootstrap chưa tạo business tables, runtime user không có DDL. Xem [Database Setup](../../../docs/database/SETUP.md).

## Testing Commands

```sh
curl http://localhost:5102/health/live
curl -i http://localhost:5102/api/equipment
dotnet build src/Services/Equipment/Equipment.Api -c Release
```

SQL trên LocalDB dev/test đã migrate: `pwsh ./scripts/Test-Databases.ps1 -Service Equipment`; fixtures rollback. `dotnet test MedEquipSLA.sln -c Release --no-build` hiện không có test project, không đại diện coverage. [gRPC guide](../../../docs/development/GRPC.md) có diagnostic/CLI checks; [latest verification](../../../docs/assignment-compliance/DOCUMENTATION-VERIFICATION.md) có evidence/giới hạn.

## Development Guidelines

Làm use case theo [Development Workflow](../../../docs/development/DEVELOPMENT-WORKFLOW.md), [Coding Conventions](../../../docs/development/CODING-CONVENTIONS.md), [Git Workflow](../../../docs/development/GIT-WORKFLOW.md). Entity/domain method → owner Fluent API → repository/query adapter → Command/Query/Validator → endpoint → migration tên mới/test. Không sửa InitialCreate đã apply, EnsureCreated hoặc auto-migrate mọi replica. Role/scope, concurrency, audit/event atomicity phải có test thực. Update [PRN232 matrix](../../../docs/assignment-compliance/PRN232-CHECKLIST.md); không đánh dấu business PASS theo skeleton.
