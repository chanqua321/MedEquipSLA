# SQL Server Code First — tạo và kết nối database

ERD v0.1 và D1–D4 đã [được duyệt](APPROVAL.md). Mỗi service sở hữu entity/mapping, DbContext và migration riêng. `InitialCreate` là baseline; không tạo lại migration cùng tên, không dùng `EnsureCreated`, không xóa database/volume để nâng cấp schema.

| Service | Database | Bảng ứng dụng | Migration |
|---|---|---:|---|
| Identity | MedEquip_Identity | 10 | 20261023044348_InitialCreate |
| Equipment | MedEquip_Equipment | 11 | 20261023044354_InitialCreate |
| Maintenance | MedEquip_Maintenance | 10 | 20261023044400_InitialCreate |
| Incident | MedEquip_Incident | 9 | 20261023044406_InitialCreate |
| Notification | MedEquip_Notification | 7 | 20261023044411_InitialCreate |

Mỗi DB có thêm `__EFMigrationsHistory`: tổng 47 bảng ứng dụng, 52 bảng vật lý. Chỉ seed 3 role cố định Admin, Technician, DepartmentStaff; không tạo tài khoản/mật khẩu mặc định hoặc dữ liệu nghiệp vụ giả.

## Môi trường team: SQL Server 2022 CU27

[Environment Setup](../development/ENVIRONMENT-SETUP.md) có template provision deployment login theo owner và cách nhập connection qua env mà không lưu credential vào command history.

1. Cài SDK theo `global.json`, chạy `dotnet tool restore`, `dotnet restore MedEquipSLA.sln --locked-mode`, `dotnet build MedEquipSLA.sln`.
2. Copy `.env.example` thành `.env`, đặt password riêng, chạy `docker compose up -d sqlserver redis db-bootstrap`. Bootstrap tạo 5 DB và 5 runtime login, chưa tạo bảng ứng dụng.
3. Người quản trị provision deployment identity riêng trên từng DB, đủ quyền DDL và seed/migration data. Runtime chỉ có datareader/datawriter; không dùng runtime credential để migrate, không cấp quyền quản trị server cho app.
4. Đặt connection string migration qua secret manager hoặc biến môi trường `MEDEQUIP_IDENTITY_MIGRATION_CONNECTION`, `MEDEQUIP_EQUIPMENT_MIGRATION_CONNECTION`, `MEDEQUIP_MAINTENANCE_MIGRATION_CONNECTION`, `MEDEQUIP_INCIDENT_MIGRATION_CONNECTION`, `MEDEQUIP_NOTIFICATION_MIGRATION_CONNECTION`. Mỗi biến phải có đúng `Database=MedEquip_<Service>`; host từ máy là `localhost,14333`, từ container là `sqlserver,1433`. Password không đưa vào source hoặc command history. Script đọc biến, không in connection string.
5. Từ root repo chạy `pwsh ./scripts/Update-Databases.ps1 -NoBuild`. Có thể chọn `-Service Equipment`. Các service apply tuần tự, dừng tại lỗi đầu tiên; không phải distributed transaction.
6. Chạy `docker compose up -d --build`. Khi chạy native, đặt `ConnectionStrings__Database` riêng cho từng terminal/service; Compose đã đặt connection của runtime đúng owner.

Deployment identity chưa được script bootstrap tự provision; cần quản trị viên tạo ở môi trường đích. Không tự giả định database đã có dữ liệu nhưng chưa có EF history là baseline tương đương: kiểm tra schema/dữ liệu và chuẩn bị baseline riêng trước khi apply. `InitialCreate` sẽ báo lỗi nếu gặp bảng đã tồn tại; tuyệt đối không drop để vượt lỗi.

SQL xuất ở [migrations](migrations/) được sinh từ C# migrations bằng `dotnet ef migrations script --idempotent`. Có thể review/apply qua deployment pipeline thay EF CLI; chọn một cách apply, không chỉnh tay SQL export để thay C# model. Khi dùng `sqlcmd`, thêm `-I -b -C` và dùng `SQLCMDPASSWORD` cho password, tránh `-P`. Export không chứa credential.

## Database đã tạo trong phiên hiện tại

Docker Linux Engine chưa khả dụng; đã dùng **SQL Server LocalDB 2025 17.0.4025.3** có sẵn trên Windows để apply thật. Cả 5 DB ONLINE, compatibility level **160** (SQL Server 2022). Đây là cách xác minh local; baseline dùng chung của team vẫn là Compose ở trên.

Kết nối bằng SSMS/Azure Data Studio hoặc công cụ SQL hỗ trợ Windows authentication:

```text
Server: (localdb)\MSSQLLocalDB
Authentication: Windows Authentication
Database: chọn một trong 5 tên ở bảng trên
Encrypt: True
TrustServerCertificate: True (local development)
```

Ví dụ biến môi trường cho riêng Equipment API:

```powershell
$env:ConnectionStrings__Database='Server=(localdb)\MSSQLLocalDB;Database=MedEquip_Equipment;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet run --project src/Services/Equipment/Equipment.Api --launch-profile Equipment
```

Tạo lại trên một Windows developer machine có LocalDB, hoặc apply migration mới vào DB hiện tại:

```powershell
dotnet tool restore
dotnet build MedEquipSLA.sln
./scripts/Update-Databases.ps1 -LocalDb -NoBuild
./scripts/Test-Databases.ps1
```

`-LocalDb` dùng Windows identity hiện tại; không chứng minh runtime login isolation như môi trường Compose. Không commit file `.mdf`, `.ldf`, `.bak` hoặc connection string chứa password.

## Kiểm tra và thêm migration

`Test-Databases.ps1` cần `sqlcmd` trên PATH. Mặc định chạy 10 suite schema/behavior, so với ERD được duyệt; fixtures nằm trong transaction và rollback cả khi thất bại. Chỉ chạy ở development/test database. Ví dụ SQL Server dùng SQL authentication: đặt `SQLCMDPASSWORD` qua secret manager rồi `pwsh ./scripts/Test-Databases.ps1 -Server localhost,14333 -Username <verification-user>`. Verification user cần đọc metadata và ghi fixture vào DB, không nhất thiết là runtime identity.

Khi thay đổi model, dùng tên migration mô tả thay đổi:

```sh
dotnet ef migrations add DescribeYourChange --project src/Services/Equipment/Equipment.Infrastructure --startup-project src/Services/Equipment/Equipment.Api --context EquipmentDbContext --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/Services/Equipment/Equipment.Infrastructure --startup-project src/Services/Equipment/Equipment.Api --context EquipmentDbContext
dotnet ef migrations script --idempotent --project src/Services/Equipment/Equipment.Infrastructure --startup-project src/Services/Equipment/Equipment.Api --context EquipmentDbContext --output docs/database/migrations/Equipment.sql
```

Review các thao tác có thể mất dữ liệu trước khi apply vào môi trường có dữ liệu. Thay đổi schema cần cập nhật ERD, schema assertions và evidence. Không thêm project reference, FK hoặc SQL JOIN sang database của service khác.

## Kiểm tra migration rollback an toàn

[EF integration test suite](../../tests/MedEquipSLA.Database.IntegrationTests/README.md) tự tạo DB mới theo owner, chạy InitialCreate Up → Down về 0 → Up và kiểm tra schema/SQL behavior. Chỉ cleanup DB do fixture CREATE thành công; không dùng database production hoặc rollback application DB đang có. Test base connection bắt buộc master hoặc không có catalog và chỉ chấp nhận LocalDB/loopback:14333. Không cấu hình SQLSERVER_TEST_CONNECTION thì runtime tests Skip/BLOCKED.
