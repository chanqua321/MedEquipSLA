# Identity — Tài khoản, MFA và phiên

Cập nhật 07/11/2026. Invite/Excel preview/partial success, activation 24h, resend, reset 30 phút, bắt buộc TOTP Admin, MFA tùy chọn nhân viên, refresh rotation/revoke, đổi role/khoa/access và Outbox sự kiện quyền.

## Sở hữu dữ liệu và kiến trúc

Database `MedEquip_Identity`, 11 bảng ứng dụng (không tính __EFMigrationsHistory). Chỉ DbContext của owner truy cập; không cross-database FK/JOIN. Domain không tham chiếu EF Core.

API → Application Services/CQRS contracts/handlers → aggregate store/query abstraction → Infrastructure/EF Core → SQL Server. Cấu trúc thực: IIdentitySessions, IAccountProvisioning; IdentitySessions, AccountProvisioning, SecurityTickets, MfaSecurity, IdentityOutboxDispatcher. Không tạo generic repository để đủ hình thức. Các use case và giới hạn vận hành được ghi trong [tracker](../../../docs/development/BACKEND-COMPLETION-TRACKER.md).

## Chạy và kiểm tra

Theo [runbook Backend](../../../docs/development/BACKEND-RUNBOOK.md) để cấu hình JWT, service keys, SQL Server, Redis, bootstrap và Docker Compose. Swagger tại cổng REST của service trong Development; prefix Gateway theo [README gốc](../../../README.md).

API chính: `/api/auth; /api/auth/mfa; /api/users; /api/users/import`. Các phép ghi kiểm tra quyền/state/RowVersion theo endpoint. Workers:Enabled=true để phát Identity Outbox. SMTP do Notification gửi; Identity chỉ render security mail qua endpoint nội bộ có service key.

Producer/consumer kiểm bằng integration tests có Redis riêng; xác minh Outbox Published, Inbox và dữ liệu đích, không chỉ XADD thủ công. SignalR/SMTP do Notification sở hữu. Hướng dẫn gRPC: [GRPC](../../../docs/development/GRPC.md), dùng Protobuf và endpoint HTTP/2 Equipment.

## Migration

- `20261023044348_InitialCreate`
- `20261106180429_AddIdentitySecurityOutbox`
- `20261106193753_AddInvitationSuspension`

Không sửa InitialCreate, reset DB hoặc EnsureCreated. Sinh migration với `dotnet ef migrations add <Name> --project src/Services/Identity/Identity.Infrastructure --startup-project src/Services/Identity/Identity.Infrastructure`, review C#/SQL và chạy test Up/Down/reapply. Bản SQL tại [migrations](../../../docs/database/migrations/Identity.sql). App DB chưa được nâng migrations mới trong phiên này; chỉ catalogs test đã apply. Không bật startup migration vào app DB khi chưa được phép.

## Phát triển và tiêu chí học phần

Thêm Entity trong Domain, Fluent API/migration trong Infrastructure, repository/adapter abstraction theo use case trong Application; thêm Command/Query rồi map API với validation/quyền/concurrency. Chạy `dotnet restore --locked-mode`, Release build và [database integration tests](../../../tests/MedEquipSLA.Database.IntegrationTests/README.md).

Đối chiếu [PRN232](../../../docs/assignment-compliance/PRN232-CHECKLIST.md): REST/JWT/validation theo API, EF/layering theo project, jobs/broker/gRPC theo luồng sở hữu, Dockerfile/Compose theo runbook. Không đánh dấu Final Assignment hoàn thành chỉ vì build/schema PASS.

[Team](../../../docs/development/TEAM-RESPONSIBILITIES.md), [Git/PR](../../../docs/development/GIT-WORKFLOW.md). Dùng branch riêng và PR có test evidence; không force push, không tự bịa đóng góp thành viên.
