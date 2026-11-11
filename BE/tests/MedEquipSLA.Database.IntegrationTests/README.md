# Backend integration tests

Ngày 07/11/2026: **157/157 PASS, 0 SKIP** trên SQL Server 2022 CU27 và Redis 8.2.10; TRX backend-final-regression-20261107.trx trong TestResults local được ignore. Suite bao gồm schema/ownership/migration Up-Down-reapply/guards/RowVersion, D3 gRPC/retirement races/source gates, scheduler/reminder/WO/calibration, Identity/MFA/invitation/session, real Redis/Inbox/Outbox, snapshot/checkpoint, SMTP capture và SignalR WebSockets. Fixture CurrentValues/SQL chỉ chuẩn bị hoặc inject lỗi trong test, không phải production bypass.

## Chạy

Build/restore trước, đặt SQLSERVER_TEST_CONNECTION trỏ master của LocalDB hoặc SQL Server loopback test cổng 14333; dùng tcp:127.0.0.1 cho Docker SQL tránh localhost IPv6. Nhập credential qua secrets manager/env, không lưu vào repo. REDIS_TEST_CONNECTION phải là Redis test riêng, ví dụ 127.0.0.1:16380,defaultDatabase=15,abortConnect=false. Thiếu môi trường thì SQL/Redis facts SKIP/BLOCKED, không PASS.

```powershell
dotnet restore --locked-mode
dotnet test tests/MedEquipSLA.Database.IntegrationTests -c Release --logger trx
```

Fixture chỉ CREATE/DROP catalog MedEquip_<Owner>_Test_<GUID> vừa tạo và sở hữu. Không ghi application DB, không chạy Down/reset application DB. Runtime isolation tests tạo GUID logins riêng và cleanup đúng login đã tạo. Redis tests đòi stream chưa tồn tại trước khi ghi và chỉ xóa stream do test sở hữu; không FLUSHDB. SMTP chỉ loopback capture, không gửi mail ngoài.

## HTTP/Docker E2E

`python tests/check_backend_workflows.py` dùng 5 Test_GUID LocalDB catalogs, 5 Kestrel APIs + Gateway và JWT/TOTP thật. MEDEQUIP_TEST_DOCKER=1 chuyển sang các images đã build và network SQL/Redis test riêng được ghi trong ignored ownership manifest .tools/docker-verification.env; không tự trỏ vào Compose ứng dụng. MEDEQUIP_TEST_CAPTURE_DOCS=1 lưu OpenAPI đã kiểm vào docs/api/openapi. Harness cleanup đúng containers/catalogs/key files của nó khi kết thúc.

Hạ tầng test Compose: tests/compose.infrastructure.yaml, dùng project name riêng và SQL_TEST_PASSWORD ngẫu nhiên trong env; không đưa manifest/password vào Git. Docker E2E đã exit 0, gồm Swagger/auth, invitation/Excel, catalog CRUD, repair/review, schedule failure/restart/idempotency, actual Scheduler→D3→WO→calibration/photo→Closed, notification isolation, reports/CSV/RBAC và logout. Năm runtime-login isolation tests kiểm độc lập với E2E dùng deployment credential để tạo fixtures.

[Evidence/giới hạn](../../docs/development/BACKEND-COMPLETION-TRACKER.md), [runbook](../../docs/development/BACKEND-RUNBOOK.md), [gRPC](../../docs/development/GRPC.md), [snapshot protocol](../../docs/development/D3-SNAPSHOT-RECONCILIATION.md).
