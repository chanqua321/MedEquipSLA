# Identity Service

Business APIs remain scaffold. Code First schema and InitialCreate migration are implemented and verified. PRN232 responsibilities: JWT authentication, refresh rotation, RBAC/department scope. Database: MedEquip_Identity; DbContext/migrations owned only by this service. Do not query another service database.

## API – Services – Repository / CQRS

Identity.Api/Program.cs -> Identity.Application/Features (Command/Query handlers = Services layer) -> Identity.Application/Abstractions -> Identity.Infrastructure/Persistence (EF repositories/query adapters) -> SQL Server. Identity.Domain has no infrastructure references. API references Infrastructure only for composition/DI. ScaffoldCommand/ScaffoldQuery are intentionally unimplemented; replace them with aggregate/use-case-specific handlers and DTOs, not a generic repository.

## Run REST

From repository root: `dotnet run --project src/Services/Identity/Identity.Api --launch-profile Identity`. Swagger http://localhost:5101/swagger/index.html; process-only health http://localhost:5101/health/live. Protected resource endpoints require JWT and currently return 501 after authentication. JWT issuer/login is pending, no default signing key or auth bypass.

## Background jobs, producer/consumer and gRPC

See [messaging guide](../../../docs/development/MESSAGING.md) and [gRPC guide](../../../docs/development/GRPC.md). JWT authentication, refresh rotation, RBAC/department scope. Hosted workers are skeleton/disabled and owned technical tables are mapped; dispatch/consumer behavior is pending; no business success is claimed. Equipment alone hosts gRPC; Incident/Maintenance are actual configured clients with Development transport probe. Other services do not fake gRPC endpoints.

## Compose and end-to-end

Use root `.env.example` setup, start infrastructure, apply service-owned migrations using the [database guide](../../../docs/database/SETUP.md), then `docker compose up -d --build`; individual service: `docker compose up -d --build identity`. It depends on SQL bootstrap and Redis. Runtime DB user has datareader/datawriter, migrations need a separate scoped deployment identity. Full workflow is [documented as pending](../../../README.md#end-to-end-demo-sau-khi-hoàn-thiện).

## Add entity / repository / Command / Query / migration

Follow [root development steps](../../../README.md#thêm-entity-repository-command-query-và-migration): Domain entity/invariants, Infrastructure Fluent API/DbContext, Application repository/query abstractions, EF adapter implementation/DI, feature Command/Validator/Handler and Query/DTO/Handler, API auth/scope mapping, then service-owned Code First migration using dotnet-ef. InitialCreate is generated and applied on SQL Server LocalDB; add a newly named migration for subsequent model changes. See [database setup](../../../docs/database/SETUP.md) and [verification](../../../docs/database/VERIFICATION.md). Never EnsureCreated + migrations or cross-database FK/transaction.

## Team/Git and criteria

Use [branch/PR workflow](../../../docs/development/GIT-WORKFLOW.md), [team table](../../../docs/development/TEAM-RESPONSIBILITIES.md), [PRN232 matrix](../../../docs/assignment-compliance/PRN232-CHECKLIST.md). Update verification/evidence when behavior is implemented; skeleton class existence is not PASS.
