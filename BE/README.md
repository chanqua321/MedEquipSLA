# MedEquipSLA

Hệ thống thiết bị y tế, bảo trì/hiệu chuẩn, sửa chữa có ảnh nghiệm thu và SLA theo khoa. **5 microservices ASP.NET Core 10, SQL Server database per service, EF Core Code First, CQRS, Clean Architecture, gRPC và Redis Streams**; Gateway YARP, Web React/TypeScript.

Cập nhật 07/11/2026: Backend đã triển khai và qua 157/157 kiểm thử SQL Server/Redis cùng Docker E2E; FE/Final Assignment chưa nghiệm thu. Xem [tracker](docs/development/BACKEND-COMPLETION-TRACKER.md). App databases vẫn InitialCreate; migrations mới chỉ chạy trên catalogs test do harness sở hữu. Không Start binary mới vào app schema cũ.

## Bắt đầu

Dùng checkout hiện tại, không clone thêm bản dự án. Cài SDK theo global.json, Docker Desktop Linux Engine, PowerShell 7. Baseline chung là SQL Server 2022 trong Compose; LocalDB chỉ là lựa chọn test Windows.

```powershell
pwsh ./scripts/Initialize-Development.ps1
dotnet tool restore
dotnet restore --locked-mode
dotnet build MedEquipSLA.sln -c Release --no-restore
```

Script tạo RSA key và .env ngẫu nhiên nếu chưa có, không ghi đè cấu hình cũ hoặc apply DB. Theo [Backend runbook](docs/development/BACKEND-RUNBOOK.md) để chạy Docker/migration/bootstrap Admin/TOTP và demo. Swagger tại http://localhost:PORT/swagger/index.html trong Development.

| Owner | REST port | Gateway prefix | README |
|---|---|---|---|
| Identity | 5101 | /identity | [Identity](src/Services/Identity/README.md) |
| Equipment | 5102 | /equipment | [Equipment](src/Services/Equipment/README.md) |
| Maintenance | 5104 | /maintenance | `src/Services/Maintenance/README.md` (available after the Maintenance owner branch is integrated) |
| Incident | 5105 | /incident | [Incident](src/Services/Incident/README.md) |
| Notification | 5106 | /notification | [Notification](src/Services/Notification/README.md) |

Gateway 5100; Equipment gRPC HTTP/2 riêng 5103. JWT do Identity cấp sau xác thực đầy đủ. Liveness không chứng minh SQL/Redis hay nghiệp vụ sẵn sàng.

## Kiến trúc và PRN232

API → Application Services/CQRS handlers → repository/adapter abstraction → Infrastructure/EF Core → SQL Server của owner. Domain độc lập EF; không FK hoặc navigation xuyên database. Ví dụ Department: DepartmentCommands/Queries → IDepartmentStore → DepartmentStore. CQRS tách đọc/ghi trong cùng database owner; Clean Architecture giữ hướng dependency vào Domain/Application, vẫn đáp ứng API–Services–Repository của đề.

Các API nghiệp vụ gọi Application handlers/services và aggregate/query ports; EF và SQL transaction nằm trong Infrastructure. Handler/repository mẫu không dùng đã được loại bỏ. Không tạo generic repository để đủ hình thức. Xem [C0](docs/architecture/C0-System-Context.md), [C1](docs/architecture/C1-Container.md), [PRN232 matrix](docs/assignment-compliance/PRN232-CHECKLIST.md).

## Luồng nghiệp vụ

Staff báo hỏng trong khoa → Admin phân công → Technician thực hiện và nộp biên bản + ảnh máy vận hành → Admin nghiệm thu/trả lại; giữ mọi attempt và SLA cũ. Schedule → Occurrence → WorkOrder đến kỳ; Calibration Failed tiếp tục chặn thiết bị. End schedule vĩnh viễn khác Inactive và không thể kích hoạt lại.

Retire cần RetirementIntent, hai source gate Sealed và hết blocker Equipment. Pending/Active registration chặn Retire; timeout không phải chứng cứ hết operation. Không transaction SQL xuyên service hoặc gọi gRPC trong local SQL transaction.

## Kiểm thử và phát triển nhóm

[Tests](tests/MedEquipSLA.Database.IntegrationTests/README.md), [Business](docs/BUSINESS-DOCUMENTATION.md), [API](docs/API-DOCUMENTATION.md), [Database](docs/DATABASE-DOCUMENTATION.md), [phân công](docs/development/TEAM-RESPONSIBILITIES.md), [Git workflow](docs/development/GIT-WORKFLOW.md).

Thêm Entity trong Domain, configuration trong Infrastructure, repository/adapter theo aggregate/use case, Command/Query trong Application rồi API với validation/quyền/RowVersion. Sinh migration mới đúng owner; review SQL và chạy Up/Down/reapply trên test catalog. Không sửa InitialCreate, EnsureCreated hoặc reset dữ liệu.

Dùng feature branch, PR vào main có test evidence; không force push. Thay đổi hiện tại chưa commit/push.
