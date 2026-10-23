# Getting Started

Mục tiêu onboarding là chạy **những phần hiện có**, hiểu giới hạn rồi bắt đầu feature. Không có login/demo account hoặc nghiệp vụ end-to-end đã hoàn thiện.

## 1. Clone và build

Yêu cầu quyền repo Private, Git, SDK 10.0.402 và Node 24.19.0. [Environment Setup](ENVIRONMENT-SETUP.md) mô tả tùy chọn SQL/Docker/PowerShell.

```sh
git clone https://github.com/chanqua321/MedEquipSLA.git
cd MedEquipSLA
dotnet tool restore
dotnet restore MedEquipSLA.sln --locked-mode
dotnet build MedEquipSLA.sln -c Release --no-restore
```

## 2. Chạy transport/scaffold native trước

Không cần SQL business data hoặc Redis để xem trang gốc/health/Swagger/probe hiện tại. Health không xác minh dependency. Mỗi process dùng một terminal; bắt đầu với:

```sh
dotnet run --project src/Services/Equipment/Equipment.Api --launch-profile Equipment
dotnet run --project src/Services/Incident/Incident.Api --launch-profile Incident
dotnet run --project src/Services/Maintenance/Maintenance.Api --launch-profile Maintenance
dotnet run --project src/Gateway/MedEquipSLA.Gateway --launch-profile Gateway
```

Identity/Notification dùng cùng lệnh, đổi project/profile thành Identity/Notification. Profile đặt Development; Equipment có REST 5102, gRPC HTTP/2 5103. Incident/Maintenance mặc định gọi `http://localhost:5103`.

```sh
curl http://localhost:5102/
curl http://localhost:5102/health/live
curl -i http://localhost:5102/api/equipment
curl http://localhost:5105/api/diagnostics/equipment-grpc
curl http://localhost:5100/equipment/health/live
```

Kỳ vọng: root có `businessImplemented:false`, health `Healthy`, resource 401, diagnostic có `businessLookupImplemented:false`, Gateway health 200. Mở http://localhost:5102/swagger/index.html. Các business GET sau auth vẫn 501. Không tạo token/key giả để xem như đã đăng nhập thành công.

## 3. Frontend

```sh
cd src/Web
npm ci
npm run dev
```

Mở http://localhost:5173. Chỉ trang giới thiệu, chưa UI CRUD/login. Dừng bằng Ctrl+C; trở lại root trước khi chạy dotnet/Docker/scripts. Build bằng `npm run build`.

## 4. Database và full infrastructure

Đọc [Environment Setup](ENVIRONMENT-SETUP.md) để chọn SQL Server Compose/remote hoặc Windows LocalDB. [Database Setup](../database/SETUP.md) có đúng 5 tên DB/migration connections, phân biệt runtime/deployment identity. Không dùng một connection cho năm service.

```sh
docker compose config --quiet
docker compose up -d sqlserver redis db-bootstrap
```

Các lệnh trên yêu cầu `.env` đã điền. Bootstrap tạo DB/runtime users; apply migration riêng trước full app. Chạy `pwsh ./scripts/Update-Databases.ps1` với deployment connection env đã thiết lập, rồi `docker compose up -d --build`. Docker Engine chưa được kiểm chứng chạy thành công trong audit hiện tại, xem [evidence](../assignment-compliance/DOCUMENTATION-VERIFICATION.md).

## 5. Bắt đầu công việc

Đọc [Business](../BUSINESS-DOCUMENTATION.md) → [API](../API-DOCUMENTATION.md) → [Database](../DATABASE-DOCUMENTATION.md), README service phụ trách. Tạo feature branch theo [Git workflow](GIT-WORKFLOW.md), làm một use case theo [Development Workflow](DEVELOPMENT-WORKFLOW.md). Tên thành viên/nhiệm vụ điền tại [Team Responsibilities](TEAM-RESPONSIBILITIES.md).
