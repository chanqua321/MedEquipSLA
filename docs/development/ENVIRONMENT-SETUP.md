# Environment Setup

## Toolchain và chế độ chạy

| Công cụ | Khi cần / source version |
|---|---|
| Git và GitHub access | Clone repo Public; push cần quyền, credential manager hoặc SSH; không token trong remote |
| .NET SDK | 10.0.402 theo global.json, target net10.0; local dotnet-ef 10.0.13 qua dotnet tool restore |
| Node/npm | Node 24.19.0 theo src/Web/.node-version, npm ci theo package-lock |
| Docker Desktop/Engine + Compose | Linux containers, build/run môi trường chung |
| PowerShell 7 | Migration/test wrapper .ps1, dùng được trên nền tảng có pwsh |
| sqlcmd | SQL verification/administration; trên PATH |
| grpcurl | Kiểm thử proto trực tiếp; optional nếu dùng REST diagnostic |
| SQL Server | Team 2022 CU27 compatibility 160; LocalDB tùy chọn Windows |
| Python 3.10+ | Tùy chọn chạy scripts/Test-Documentation.py (chỉ stdlib); không cần cho ứng dụng .NET/Web |

`dotnet --info`, `node --version`, `npm --version`, `git --version`, `docker version`, `docker compose version`, `pwsh --version`, `sqlcmd -?` kiểm tra cài đặt. Không đổi phiên bản project vì host có SDK khác. x86-64 SQL Server có thể chạy trên máy nhóm/remote cho thành viên ARM. Chưa có minimum RAM toàn stack đã đo; native smoke chỉ chạy process/probe, không đòi SQL/Redis.

## Cấu hình đang dùng

| Key env | Owner | Mặc định / hành vi |
|---|---|---|
| ConnectionStrings__Database | Mỗi service | Catalog đúng owner, host localhost,14333 native hoặc sqlserver,1433 container; password không có trong appsettings |
| ConnectionStrings__Redis | Mỗi service | localhost:6379,abortConnect=false native; redis:6379 container |
| Grpc__EquipmentAddress | Incident/Maintenance | http://localhost:5103 native; http://equipment:8081 container |
| Jwt__Issuer / Jwt__Audience | Mỗi API | https://identity.medequip.local / medequip-api |
| Jwt__PublicKeyPath | Mỗi API | Rỗng; token không được chấp nhận khi chưa cấu hình key |
| Workers__Enabled | Maintenance/Incident/Notification | false; true hiện chỉ tick log |
| ASPNETCORE_ENVIRONMENT | API | Development từ launch profile; Swagger/probe development only |
| MEDEQUIP_<SERVICE>_MIGRATION_CONNECTION | Migration wrapper | Secret connection có DDL ở đúng DB, không dùng runtime user |
| SQLCMDPASSWORD | SQL checks khi dùng -Username | Password trong env, không command-line -P |

Không có SMTP/SignalR config contract hoàn chỉnh vì hai tính năng còn Planned. `.env` không được .NET tự nạp; `dotnet run` nhận env của terminal hoặc configuration provider đã code. EF design-time factory chỉ đọc ConnectionStrings__Database, không tự đọc user-secrets của API. Mỗi terminal đặt connection đúng owner.

## Docker environment

Copy `.env.example` sang `.env` khi chưa tồn tại, tự đặt 6 password mạnh khác nhau theo ghi chú file (ít nhất 16 ký tự, đủ nhóm ký tự; tránh quote/$/semicolon/space trong bootstrap SQLCMD hiện tại). Không commit `.env`, không ghi password vào terminal history/log/PR. `docker compose config --quiet` kiểm tra mà không in env đã nội suy; tránh chia sẻ `docker compose config` đầy đủ vì có thể lộ password.

```sh
docker compose up -d sqlserver redis db-bootstrap
docker compose ps
```

SQL host 127.0.0.1:14333, Redis 127.0.0.1:6379; container dùng service DNS, không localhost để gọi service khác. Network medequip, volumes sql-data/redis-data. Bootstrap không rotate login password cũ; không xóa volume để xử lý sai mật khẩu.

### Provision migration identity cho fresh development database

Admin của SQL instance chạy template dưới bằng SQLCMD mode/`sqlcmd -i` với password nhận qua env `MIGRATION_PASSWORD`. Đây là hướng dẫn thao tác quản trị, **chưa được chạy trên Docker trong audit**. Login/user mẫu chỉ cho Equipment; lặp theo bảng tên bên dưới cho bốn owner còn lại. Dùng password khác mỗi owner, không thay credential runtime.

```sql
-- SQLCMD substitution $(MIGRATION_PASSWORD) lấy từ environment, không commit giá trị thật.
USE [master];
GO
CREATE LOGIN [medequip_equipment_migrator] WITH PASSWORD = '$(MIGRATION_PASSWORD)', CHECK_POLICY=ON;
GO
USE [MedEquip_Equipment];
GO
CREATE USER [medequip_equipment_migrator] FOR LOGIN [medequip_equipment_migrator];
ALTER ROLE db_ddladmin ADD MEMBER [medequip_equipment_migrator];
ALTER ROLE db_datareader ADD MEMBER [medequip_equipment_migrator];
ALTER ROLE db_datawriter ADD MEMBER [medequip_equipment_migrator];
GRANT VIEW DEFINITION TO [medequip_equipment_migrator];
GO
```

Template dành cho login mới; nếu đã tồn tại phải kiểm tra quyền và rotation theo quản trị, không chạy CREATE mù. Không cấp sysadmin/db_owner cho runtime. `db_ddladmin` chỉ dùng deployment identity đáng tin trong development; production nên có quy trình quyền theo script đã review và secrets manager.

| Catalog | Deployment login đề xuất | Environment name |
|---|---|---|
| MedEquip_Identity | medequip_identity_migrator | MEDEQUIP_IDENTITY_MIGRATION_CONNECTION |
| MedEquip_Equipment | medequip_equipment_migrator | MEDEQUIP_EQUIPMENT_MIGRATION_CONNECTION |
| MedEquip_Maintenance | medequip_maintenance_migrator | MEDEQUIP_MAINTENANCE_MIGRATION_CONNECTION |
| MedEquip_Incident | medequip_incident_migrator | MEDEQUIP_INCIDENT_MIGRATION_CONNECTION |
| MedEquip_Notification | medequip_notification_migrator | MEDEQUIP_NOTIFICATION_MIGRATION_CONNECTION |

Connection shape: `Server=localhost,14333;Database=MedEquip_Equipment;User Id=medequip_equipment_migrator;Password=<secret>;Encrypt=True;TrustServerCertificate=True`. `<secret>` là placeholder, không copy nguyên làm credential. TrustServerCertificate chỉ cho development; deployment dùng certificate/TLS hợp lệ.

Nhập connection an toàn trong PowerShell 7 mà không ghi giá trị vào history/output:

```powershell
$env:MEDEQUIP_EQUIPMENT_MIGRATION_CONNECTION = Read-Host 'Equipment deployment connection' -MaskInput
pwsh ./scripts/Update-Databases.ps1 -Service Equipment
Remove-Item Env:\MEDEQUIP_EQUIPMENT_MIGRATION_CONNECTION
```

Lặp cho từng owner hoặc đặt cả năm env rồi chọn All mặc định. Shell khác dùng secrets manager/env tương ứng; EF CLI từng service có thể đọc `ConnectionStrings__Database`. Không ghi connection có secret vào source.

## LocalDB bổ sung trên Windows

Không yêu cầu cả team cài LocalDB. Nếu dùng: `SqlLocalDB start MSSQLLocalDB`, chạy wrapper `-LocalDb`, rồi SQL checks. Native API cần env riêng, ví dụ:

```powershell
$env:ConnectionStrings__Database='Server=(localdb)\MSSQLLocalDB;Database=MedEquip_Equipment;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet run --project src/Services/Equipment/Equipment.Api --launch-profile Equipment
```

Không dùng biến Equipment cho Incident/Maintenance. LocalDB dùng Windows identity, không chứng minh credential isolation của Compose. Xem [Database Setup](../database/SETUP.md), [root troubleshooting](../../README.md#troubleshooting).
