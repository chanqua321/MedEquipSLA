# MedEquipSLA — PRN232 foundation

Repository: [chanqua321/MedEquipSLA](https://github.com/chanqua321/MedEquipSLA) — **Private**.

**Giai đoạn: scaffold, chưa hoàn thành Final Assignment.** Luồng đăng nhập, CRUD nghiệp vụ, quét hạn/SLA, Outbox/Inbox và consumer nghiệp vụ chưa triển khai. API tài nguyên hiện trả 401 nếu thiếu JWT; khi qua auth vẫn trả 501. Không có dữ liệu giả được mô tả như tính năng đã chạy.

## Nguồn yêu cầu và trạng thái

- [Đề học phần chính thức](docs/assignment-compliance/PRN232%20-%20Final%20Assignment.pdf) đã được đọc đủ 3 trang trước khởi tạo solution.
- [PRN232 Compliance Matrix](docs/assignment-compliance/PRN232-CHECKLIST.md): tiêu chí còn thiếu, cách kiểm chứng và bằng chứng.
- [Business Rules v1.0 đề xuất](docs/business/BUSINESS-RULES-v1.0-PROPOSED.md), [review nghiệp vụ](docs/business/REVIEW-v1.0.md).
- [C0](docs/architecture/C0-System-Context.md), [C1](docs/architecture/C1-Container.md), [evidence scaffold](docs/assignment-compliance/SCAFFOLD-VERIFICATION.md).

Ưu tiên đề PRN232 cho yêu cầu học phần; kiến trúc và BR là yêu cầu dự án. Không thay microservices bằng monolith, không bỏ CQRS/Clean Architecture/database-per-service. ERD được duyệt chốt D1–D4 về SLA, occurrence, registration/reconciliation và calibration failure. Schema lưu đủ dữ liệu cho các quy tắc này; authorization freshness và protocol liên service vẫn cần triển khai/test tại Application/Domain.

## Database Code First đã tạo

[ERD v0.1](docs/database/ERD-REVIEW.md) và D1–D4 đã được người dùng duyệt. Đã có 5 migration `InitialCreate`, 47 bảng ứng dụng và 5 bảng migration history. Đã apply và kiểm thử trên SQL Server LocalDB 2025, compatibility 160; môi trường chung của team vẫn là SQL Server 2022 CU27 trong Compose. Xem [cách tạo/kết nối database](docs/database/SETUP.md) và [kết quả kiểm tra thực tế](docs/database/VERIFICATION.md).

## Kiến trúc đáp ứng API – Services – Repository

```text
Client -> Gateway (REST reverse proxy) -> Service.Api
  -> Service.Application / Features / Command|QueryHandler   [Services layer]
  -> Service.Application / Abstractions / I...Repository|QueryAdapter
  -> Service.Infrastructure / Persistence / Repository|QueryAdapter
  -> EF Core / SQL Server database do service sở hữu

Domain: độc lập với EF, Redis, gRPC, ASP.NET và service khác.
```

Clean Architecture xác định hướng dependency; CQRS tách use case đọc/ghi trong Application Services. Vì thế vẫn có đủ API, Services và Repository theo đề, chỉ tổ chức theo use case thay vì một lớp service lớn. Repository abstraction nằm ở Application; implementation EF trong Infrastructure. Không có generic repository hình thức; repository hiện là placeholder theo service, sẽ thay bằng aggregate-specific interfaces khi model BR được khóa. Query có adapter riêng, không ghi dữ liệu. API chỉ tham chiếu Infrastructure để đăng ký DI, không dùng DbContext trong endpoint.

Mỗi service có Domain/Application/Infrastructure/Api, DbContext và database riêng. BuildingBlocks chỉ chứa interfaces CQRS và hosting, Contracts chỉ chứa protobuf/event DTO; không chia sẻ business entity hoặc DbContext.

| Ứng dụng | REST native / Compose host | Database | Tiêu chí chính |
|---|---|---|---|
| Identity | 5101 | MedEquip_Identity | JWT/Identity, quyền và tài khoản — pending |
| Equipment | 5102; gRPC 5103 | MedEquip_Equipment | CRUD, gRPC server, SQL Code First |
| Maintenance | 5104 | MedEquip_Maintenance | CQRS, scheduler, gRPC client, producer |
| Incident | 5105 | MedEquip_Incident | CQRS, SLA scanner, gRPC client, producer |
| Notification | 5106 | MedEquip_Notification | Consumer, Inbox, email, SignalR — pending |
| Gateway | 5100 | Không có | Routing REST tới các ứng dụng độc lập |
| Web | 5173 | Không có | React/TypeScript scaffold |

## Toolchain chung cho team

.NET SDK **10.0.402**, EF Core/SQL provider **10.0.13**, SQL Server **2022 CU27**, Redis **8.2.10**, Node **24.19.0**. SDK khóa ở global.json, NuGet ở Directory.Packages.props và packages.lock.json, frontend ở package-lock.json. Image dùng tag cụ thể; chưa xác minh image digest vì chưa chạy pull/build Docker. Kiến trúc dùng tính năng .NET 8+ mà đề yêu cầu; chọn .NET 10 LTS cho baseline mới, không theo SDK của một cá nhân.

Compose là môi trường chuẩn cho cả nhóm, không yêu cầu LocalDB hoặc đường dẫn Windows riêng. SQL Server container hỗ trợ x86-64; thành viên ARM dùng SQL Server x86-64 của nhóm/remote với connection string riêng. Không tuyên bố mọi CPU đều chạy được SQL Server container local. Xem [Microsoft platforms](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/setup?view=sql-server-ver17).

## Restore/build

Cài SDK trong global.json và Node trong src/Web/.node-version hoặc dùng Docker multi-stage build; không cần thay SDK mặc định của máy.

```sh
dotnet restore MedEquipSLA.sln --locked-mode
dotnet build MedEquipSLA.sln -c Release --no-restore
cd src/Web
npm ci
npm run build
```

Các lệnh dùng ở root repo nếu không ghi khác. dotnet-ef là local tool: `dotnet tool restore`. Không commit cache SDK/NuGet/npm hoặc node_modules.

## Chạy native REST API

```sh
dotnet run --project src/Services/Equipment/Equipment.Api --launch-profile Equipment
dotnet run --project src/Services/Incident/Incident.Api --launch-profile Incident
dotnet run --project src/Services/Maintenance/Maintenance.Api --launch-profile Maintenance
dotnet run --project src/Gateway/MedEquipSLA.Gateway --launch-profile Gateway
```

Chạy từng lệnh trong terminal riêng. Identity/Notification tương tự theo tên service. Health: `curl http://localhost:5102/health/live`; Swagger: http://localhost:5102/swagger/index.html. Gateway: http://localhost:5100/equipment/health/live. Health/live chỉ xác nhận process, không xác nhận database/broker hoặc nghiệp vụ.

Native frontend: `cd src/Web`, `npm ci`, `npm run dev`. Proxy `/api/equipment/...` đi Gateway; production nginx trong Compose dùng cùng đường dẫn.

JWT middleware đã cấu hình xác minh RSA, issuer/audience/lifetime; token issuance, refresh và resource authorization còn pending. Đặt public PEM qua `Jwt__PublicKeyPath`, không tạo signing key mặc định hoặc bypass auth. Không cấu hình key thì token không được chấp nhận; health và Development probes vẫn chạy. Không dùng khóa thật trong source control.

## Docker Compose

1. Copy `.env.example` thành `.env`, đặt 6 password mạnh khác nhau theo ghi chú. `.env` bị Git ignore. Đây là cấu hình development/demo, không phải cấu hình production.
2. `docker compose config --quiet` kiểm tra cấu hình.
3. `docker compose up -d sqlserver redis db-bootstrap` chạy hạ tầng. Apply migrations theo [database setup](docs/database/SETUP.md), rồi `docker compose up -d --build` chạy các ứng dụng.
4. `docker compose ps`, `docker compose logs --tail 50 equipment incident notification` kiểm tra process; gọi các health URL trong bảng.
5. `docker compose down` dừng, giữ volume. Không thêm `-v` khi muốn giữ dữ liệu.

Bootstrap chỉ tạo 5 database và 5 login/user riêng với datareader/datawriter, không tạo bảng nghiệp vụ và không cấp db_owner/DDL cho runtime. Migrations chạy thủ công bằng deployment identity có DDL trên đúng database qua `scripts/Update-Databases.ps1`; không chạy bằng credential runtime. Compose bootstrap chưa tự apply business migrations. Schema đã được tạo/kiểm thử tại LocalDB; Docker Engine chưa chạy được trong lượt xác minh này. Password bootstrap không tự rotate login đã tồn tại; rotate bằng thao tác quản trị riêng, không xóa volume để sửa mật khẩu.

Network `medequip`, volumes `sql-data`/`redis-data`, connection strings bằng env và DNS service; không dùng localhost từ container để gọi service khác. Mỗi service chỉ có credential database của mình. SQL bootstrap dùng sa chỉ trong job quản trị; app không dùng sa. Swagger và diagnostic probe chỉ mở ở Development. Trước cloud deployment phải bổ sung TLS, quản lý secrets và readiness checks thật.

## Kiểm thử gRPC riêng

Xem [gRPC guide](docs/development/GRPC.md). Hiện có probe giao tiếp thật HTTP/2:

```sh
grpcurl -plaintext -import-path src/Contracts/MedEquipSLA.Contracts.Grpc/Protos -proto equipment.proto -d '{}' localhost:5103 medequip.equipment.v1.EquipmentLookup/GetServiceInfo
curl http://localhost:5105/api/diagnostics/equipment-grpc
curl http://localhost:5104/api/diagnostics/equipment-grpc
```

Probe chỉ trả service/phase scaffold, không thay thế business GetEquipment (chưa triển khai). Không có REST thay cho gRPC ở hai client này. Lệnh grpcurl cần cài công cụ riêng; protobuf contract được cung cấp để không cần reflection.

## Background jobs và Producer/Consumer

MaintenanceScheduler, SlaDeadlineScanner và NotificationConsumer có registered hosted-service skeleton, mặc định `Workers__Enabled=false`. Bật true hiện chỉ tạo log tick; không quét DB, phát event, gửi email hoặc đọc stream. Xem [messaging guide](docs/development/MESSAGING.md) để kiểm tra broker và các bước hoàn thiện producer/outbox/consumer/inbox. Không đánh dấu criteria background/message PASS bằng log tick hoặc redis-cli.

## Thêm Entity, Repository, Command, Query và migration

1. Chốt BR/acceptance test; thêm aggregate/invariants trong `<Service>.Domain`, không tham chiếu infrastructure.
2. Thêm Fluent API configuration và DbSet trong `<Service>.Infrastructure/Persistence`; không thêm FK hoặc navigation sang database khác.
3. Đặt repository abstraction theo aggregate và read DTO/query interface trong Application; implement EF repository/query adapter ở Infrastructure. Đăng ký DI.
4. Thêm Features/<UseCase>/Command, Handler, Validator và Query/Handler/DTO. ValidateAsync tại application boundary; domain kiểm tra invariant; query AsNoTracking/projection/sort whitelist/pagination.
5. API map endpoint -> handler, kiểm tra JWT + role/department/version; audit + outbox cùng local SQL transaction. Không gọi mạng trong SQL transaction kéo dài.
6. Tạo Code First migration (ví dụ dưới), review script, apply bằng migration identity có DDL; runtime user không có DDL. Không gọi EnsureCreated hoặc Migrate tự động từ mọi replica.
7. Thêm test có ý nghĩa, cập nhật OpenAPI, checklist và evidence. Không đánh dấu PASS bằng sự tồn tại của class.

```sh
dotnet tool restore
dotnet ef migrations add DescribeYourChange --project src/Services/Equipment/Equipment.Infrastructure --startup-project src/Services/Equipment/Equipment.Api --context EquipmentDbContext --output-dir Persistence/Migrations
dotnet ef migrations script --idempotent --project src/Services/Equipment/Equipment.Infrastructure --startup-project src/Services/Equipment/Equipment.Api --context EquipmentDbContext
```

`InitialCreate` đã tồn tại trong mỗi service; chỉ tạo migration tên mới khi thay đổi model. `ConnectionStrings__Database` dùng env local cho EF design-time factory; factory kiểm tra đúng tên database của service. Không copy password vào tài liệu, log hoặc PR. Khi chạy API native, đặt biến này riêng cho đúng service; không dùng cùng connection string cho cả năm service.

## Git và đóng góp

Xem [Git workflow](docs/development/GIT-WORKFLOW.md), [phân công mẫu](docs/development/TEAM-RESPONSIBILITIES.md). Tạo branch feature/<issue>-<scope>, commit nhỏ, push branch, mở PR và đính kèm build/test evidence; không push thẳng main. Không tự bịa tên thành viên hoặc số liệu đóng góp.

GitHub private chỉ tạo/push khi có kết nối tài khoản và quyền phù hợp. Evidence ghi rõ remote/commit thực tế; repo local không được mô tả như repo GitHub đã tạo.

## End-to-end demo sau khi hoàn thiện

1. Admin đăng nhập, tạo khoa và thiết bị.
2. Staff báo sự cố trong khoa; Incident REST gọi Equipment gRPC, commit Incident + Outbox.
3. Dispatcher phát Redis; Notification consumer commit Inbox + Notification, Equipment cập nhật projection.
4. Technician nhận/xử lý; Admin nghiệm thu/Close; đối soát SLA và trạng thái thiết bị.
5. Cho Maintenance scheduler/SLA scanner chạy thật và minh họa deadline, duplicate event, broker recovery, gRPC timeout và Department isolation.

Đây là kịch bản tương lai, chưa chạy trong scaffold. README từng service liên kết hướng dẫn chung và ghi phần trách nhiệm còn thiếu.
