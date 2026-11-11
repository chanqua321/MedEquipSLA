# Solution Structure và Layered Architecture

Solution [MedEquipSLA.sln](../../MedEquipSLA.sln) có **26 .NET projects**: 5 service ×4 layer =20, 2 BuildingBlocks, 2 Contracts, 1 Gateway, thêm 1 Database.IntegrationTests project. Web React tách riêng, không tính vào số .NET projects. `tests/database` là SQL suites, được .NET integration test host chạy lại trên DB cô lập; test host tham chiếu nhiều Infrastructure chỉ để verification, không phải shared runtime service.

```text
src/Services/<Service>/
  <Service>.Api/             HTTP/gRPC endpoints, composition root, appsettings
  <Service>.Application/     Features/<UseCase>, Command/Query/Handler/Validator
                            Abstractions: repository + read adapter
  <Service>.Domain/          Business entities/value objects, domain invariants
  <Service>.Infrastructure/  EF context/config/repository/migrations
                            Messaging adapters, Workers, owner-local technical entities
  README.md
```

| PRN232 layer | Code responsibility | Dependency |
|---|---|---|
| API | Program.cs/endpoints và DI composition | Application; Infrastructure để đăng ký DI |
| Services | Application Features, CQRS handlers/validators | Domain + abstractions/BuildingBlocks |
| Repository abstraction | Application.Abstractions | Không phụ thuộc EF/SQL |
| Repository implementation | Infrastructure.Persistence | Implements abstractions, EF Core SQL Server |
| Domain | Entities và invariant | Không tham chiếu framework hạ tầng |

Compile references hướng vào trong. API được reference Infrastructure cho composition, không dùng DbContext thay handler. Infrastructure tham chiếu Application, Application tham chiếu Domain. Không project reference tới service khác. Contracts chỉ protobuf/envelope/DTO, không chứa DbContext/entity. BuildingBlocks không trở thành shared business model.

CQRS là lớp Application Services của đề: Command thay đổi aggregate qua repository/unit-of-work; Query dùng projection/read adapter, AsNoTracking phù hợp. Không bắt buộc MediatR, database đọc riêng hoặc Event Sourcing. Không tạo generic repository chỉ để có tên layer.

Các handler mẫu NotImplemented và existence-only repositories không dùng đã được bỏ. Application có handlers/services và aggregate/query ports thực; Domain có factories/transitions/invariants, Infrastructure có EF transactions, workers và network adapters. API không truy vấn DbContext trực tiếp.

| Khu vực khác | Trách nhiệm |
|---|---|
| src/BuildingBlocks/MedEquipSLA.Application | CQRS interfaces, IUnitOfWork |
| src/BuildingBlocks/MedEquipSLA.Hosting | JWT validation, health/Swagger, ProblemDetails handler |
| src/Contracts/MedEquipSLA.Contracts.Grpc | Proto và generated client/server |
| src/Contracts/MedEquipSLA.Contracts.Events | IntegrationEvent + producer/consumer interfaces |
| src/Gateway/MedEquipSLA.Gateway | YARP, health; không DB nghiệp vụ |
| src/Web | React/TypeScript/Vite, nginx config |
| infrastructure/sql | Tạo DB/runtime logins, không tạo business tables |
| scripts / tests/database | Migration wrapper và rollback SQL tests |

[Development workflow](../development/DEVELOPMENT-WORKFLOW.md), [database](../DATABASE-DOCUMENTATION.md), [PRN232 matrix](../assignment-compliance/PRN232-CHECKLIST.md).
