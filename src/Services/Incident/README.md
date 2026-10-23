# Incident Service

## Service Owner và trách nhiệm

Owner boundary: **Incident**, không phải tên thành viên. Team member chưa phân công; điền tại [Team Responsibilities](../../../docs/development/TEAM-RESPONSIBILITIES.md).

Incident, SLA policies/versions/snapshots, ResolutionAttempts, nghiệm thu và cảnh báo deadline; final SLA theo D1. Đọc [Business Documentation](../../../docs/BUSINESS-DOCUMENTATION.md) và [API Documentation](../../../docs/API-DOCUMENTATION.md) trước feature code.

## Database Ownership

`MedEquip_Incident`, `IncidentDbContext`, **9 bảng ứng dụng + một history**; có service-owned InitialCreate. [ERD/data dictionary](../../../docs/database/services/Incident.md), [Database Documentation](../../../docs/DATABASE-DOCUMENTATION.md), [verification](../../../docs/database/VERIFICATION.md). Không truy vấn/ghi database service khác; reference ngoài owner là ID logic, không FK/JOIN.

Schema đã kiểm chứng trên SQL Server LocalDB compatibility 160; môi trường nhóm SQL Server 2022 trong Compose chưa runtime-verified. Private setters/persistence shape không chứng minh workflows đã implement.

## Project Structure và API – Services – Repository

```text
Incident.Api/             Program.cs, endpoints, composition/config
Incident.Application/     Features/Command/Query/Handler/Validator; Abstractions
Incident.Domain/          Entity/value object; không framework infrastructure
Incident.Infrastructure/  Persistence context/config/repository/migrations
                       Messaging, technical entities, Workers nếu có
```

API → Application Services/CQRS handlers → repository/read abstractions → Infrastructure EF implementation → SQL. ScaffoldCommand/ScaffoldQuery vẫn ném NotImplemented; validator được đăng ký không đồng nghĩa command validation đã được gọi. Không generic repository hình thức hoặc shared entity/context.

## API và gRPC

REST trực tiếp: `http://localhost:5105`, Gateway prefix `/incident`.

| Method / route | Auth / kết quả hiện tại |
|---|---|
| GET / | Anonymous, 200 service/phase/businessImplemented=false |
| GET /health/live | Anonymous, 200 Healthy; không test SQL/Redis |
| GET /api/incidents | JWT hợp lệ, chưa role/department policy; thiếu token 401, sau auth handler 501 |
| GET /swagger/index.html | Development, Swagger UI |
| GET /swagger/v1/swagger.json | Development, OpenAPI JSON |

Generated EquipmentLookup client đọc Grpc__EquipmentAddress. GET /api/diagnostics/equipment-grpc (Development, anonymous) gọi GetServiceInfo với deadline 3 giây; business lookup/registration Planned. CRUD/search/filter/sort/pagination và business DTO còn Planned; xem API Documentation để tránh gọi route đề xuất như route đã có. Không có user/token mặc định.

## Events Produced và Consumed

- **Produced:** IncidentCreated/Assigned/Resolved/Closed và SlaResponseBreached/SlaResolutionBreached Planned; alert là provisional theo D1. Outbox mapping/XADD adapter có, workflow/dispatcher chưa có.
- **Consumed:** Chưa có subscription/Inbox business consumer trong C1 hiện tại.

Envelope có EventId/SchemaVersion/AggregateVersion/correlation; Payload là JSON string. [Messaging guide](../../../docs/development/MESSAGING.md) tách Redis infrastructure probe khỏi app producer/consumer evidence.

## Dependencies và configuration

SQL Server, Redis, Equipment gRPC; Identity user/authorization gRPC Planned. SlaDeadlineScanner registered, Workers__Enabled=false; true chỉ tick log mỗi phút, không scan SQL hoặc phát alert/event.

`ConnectionStrings__Database` phải đúng `MedEquip_Incident`; `ConnectionStrings__Redis` mặc định localhost:6379,abortConnect=false. JWT issuer/audience/public PEM theo hosting; không private key trong repo. Incident/Maintenance dùng `Grpc__EquipmentAddress=http://localhost:5103` native, `http://equipment:8081` container. `.env` chỉ Compose đọc, không tự nạp vào dotnet run. [Environment Setup](../../../docs/development/ENVIRONMENT-SETUP.md).

## Run Commands

Từ repository root, sau tool restore/build:

```sh
dotnet run --project src/Services/Incident/Incident.Api --launch-profile Incident
```

Swagger http://localhost:5105/swagger/index.html. Process/probe hiện không cần business data để start; muốn thao tác SQL phải cấu hình đúng database. Docker: chuẩn bị .env → `docker compose up -d sqlserver redis db-bootstrap` → apply migrations riêng → `docker compose up -d --build incident`. Bootstrap chưa tạo business tables, runtime user không có DDL. Xem [Database Setup](../../../docs/database/SETUP.md).

## Testing Commands

```sh
curl http://localhost:5105/health/live
curl -i http://localhost:5105/api/incidents
dotnet build src/Services/Incident/Incident.Api -c Release
```

SQL trên LocalDB dev/test đã migrate: `pwsh ./scripts/Test-Databases.ps1 -Service Incident`; fixtures rollback. `dotnet test MedEquipSLA.sln -c Release --no-build` chạy model tests; có SQLSERVER_TEST_CONNECTION thì chạy thêm runtime EF tests trên DB mới riêng. Xem [EF integration tests](../../../tests/MedEquipSLA.Database.IntegrationTests/README.md). Runtime skip không được báo PASS. [gRPC guide](../../../docs/development/GRPC.md) có diagnostic/CLI checks; [latest verification](../../../docs/assignment-compliance/DOCUMENTATION-VERIFICATION.md) có evidence/giới hạn.

## Development Guidelines

Làm use case theo [Development Workflow](../../../docs/development/DEVELOPMENT-WORKFLOW.md), [Coding Conventions](../../../docs/development/CODING-CONVENTIONS.md), [Git Workflow](../../../docs/development/GIT-WORKFLOW.md). Entity/domain method → owner Fluent API → repository/query adapter → Command/Query/Validator → endpoint → migration tên mới/test. Không sửa InitialCreate đã apply, EnsureCreated hoặc auto-migrate mọi replica. Role/scope, concurrency, audit/event atomicity phải có test thực. Update [PRN232 matrix](../../../docs/assignment-compliance/PRN232-CHECKLIST.md); không đánh dấu business PASS theo skeleton.
