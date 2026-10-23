# Notification Service

Scaffold only. PRN232 responsibilities: Redis consumer/Inbox, notification REST, email/SignalR. Database: MedEquip_Notification; DbContext/migrations owned only by this service. Do not query another service database.

## API – Services – Repository / CQRS

Notification.Api/Program.cs -> Notification.Application/Features (Command/Query handlers = Services layer) -> Notification.Application/Abstractions -> Notification.Infrastructure/Persistence (EF repositories/query adapters) -> SQL Server. Notification.Domain has no infrastructure references. API references Infrastructure only for composition/DI. ScaffoldCommand/ScaffoldQuery are intentionally unimplemented; replace them with aggregate/use-case-specific handlers and DTOs, not a generic repository.

## Run REST

From repository root: `dotnet run --project src/Services/Notification/Notification.Api --launch-profile Notification`. Swagger http://localhost:5106/swagger/index.html; process-only health http://localhost:5106/health/live. Protected resource endpoints require JWT and currently return 501 after authentication. JWT issuer/login is pending, no default signing key or auth bypass.

## Background jobs, producer/consumer and gRPC

See [messaging guide](../../../docs/development/MESSAGING.md) and [gRPC guide](../../../docs/development/GRPC.md). Redis consumer/Inbox, notification REST, email/SignalR. Hosted workers are skeleton/disabled and Outbox/Inbox types are not mapped; no business success is claimed. Equipment alone hosts gRPC; Incident/Maintenance are actual configured clients with Development transport probe. Other services do not fake gRPC endpoints.

## Compose and end-to-end

Use root `.env.example` setup, then `docker compose up -d --build`; individual service: `docker compose up -d --build notification`. It depends on SQL bootstrap and Redis. Runtime DB user has datareader/datawriter, migrations need a separate scoped deployment identity. Full workflow is [documented as pending](../../../README.md#end-to-end-demo-sau-khi-hoàn-thiện).

## Add entity / repository / Command / Query / migration

Follow [root development steps](../../../README.md#thêm-entity-repository-command-query-và-migration): Domain entity/invariants, Infrastructure Fluent API/DbContext, Application repository/query abstractions, EF adapter implementation/DI, feature Command/Validator/Handler and Query/DTO/Handler, API auth/scope mapping, then service-owned Code First migration using dotnet-ef. No migrations generated until BR/model review. Never EnsureCreated + migrations or cross-database FK/transaction.

## Team/Git and criteria

Use [branch/PR workflow](../../../docs/development/GIT-WORKFLOW.md), [team table](../../../docs/development/TEAM-RESPONSIBILITIES.md), [PRN232 matrix](../../../docs/assignment-compliance/PRN232-CHECKLIST.md). Update verification/evidence when behavior is implemented; skeleton class existence is not PASS.
