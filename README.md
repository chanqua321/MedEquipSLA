# MedEquipSLA

MedEquipSLA là hệ thống quản lý thiết bị y tế và thời hạn xử lý sự cố (SLA). Nhân viên khoa báo hỏng thiết bị. Quản trị viên phân công kỹ thuật viên. Kỹ thuật viên cập nhật tiến độ và nộp minh chứng để quản trị viên nghiệm thu. Dự án còn có các phần quản lý hồ sơ thiết bị, bảo trì định kỳ, hiệu chuẩn và thông báo.

## Bắt đầu đọc code

| Bạn muốn tìm | Vị trí |
| --- | --- |
| Tài khoản, đăng nhập, vai trò và phân quyền | [BE/src/Services/Identity](BE/src/Services/Identity/) |
| Phiếu sự cố, minh chứng sửa chữa và chính sách SLA | [BE/src/Services/Incident](BE/src/Services/Incident/) |
| Kiểm thử bảo mật Identity | [IdentitySecurityTests.cs](BE/tests/MedEquipSLA.Database.IntegrationTests/IdentitySecurityTests.cs) |
| Giao diện đăng nhập, tài khoản, quản lý người dùng và sự cố | [FE/src/features](FE/src/features/) |
| Hàm gọi API cho Identity và Incident | [FE/src/api](FE/src/api/) |
| Trạng thái phiên đăng nhập và quyền truy cập trên giao diện | [FE/src/app](FE/src/app/) |

Trong mỗi service BE, thư mục `*.Api` khai báo endpoint và cấu hình khởi động. `*.Application` chứa nghiệp vụ và các giao diện dùng bởi nghiệp vụ. `*.Domain` chứa mô hình nghiệp vụ. `*.Infrastructure` triển khai lưu trữ, tích hợp và tác vụ nền. Các migration EF Core nằm trong `Persistence/Migrations/` của Infrastructure.

FE dùng React và TypeScript. Các màn hình nằm trong `FE/src/features/`, mã gọi backend nằm trong `FE/src/api/`, còn logic phiên và kiểm tra quyền nằm trong `FE/src/app/`.

## Phân công và trạng thái tích hợp

| Thành viên | Phần phụ trách | Trạng thái trên `main` khi viết README |
| --- | --- | --- |
| Phúc | Identity, RBAC/MFA, Incident/SLA và các màn hình liên quan | Đã có hai commit BE và FE trong [PR #1](https://github.com/chanqua321/MedEquipSLA/pull/1) |
| Khánh | Equipment, giao tiếp gRPC cho thiết bị, đăng ký và thanh lý | Chờ commit của thành viên |
| Nghiêm Anh | Maintenance/Calibration, lịch định kỳ, WorkOrder và nghiệm thu | Chờ commit của thành viên |
| Thắng | Notification, Gateway, hạ tầng dùng chung và tích hợp | Chờ commit của thành viên |

## Điều kiện để chạy

Repo đang tích hợp từng phần. Cần bổ sung các thành phần sau trước khi chạy ứng dụng từ một bản clone mới:

- **BE:** cấu hình build và package dùng chung, các dự án trong `BE/src/BuildingBlocks/` và `BE/src/Contracts/`, cùng cấu hình môi trường cho các dịch vụ. Có thể thấy các tham chiếu trực tiếp trong [Identity.Api.csproj](BE/src/Services/Identity/Identity.Api/Identity.Api.csproj) và [Incident.Api.csproj](BE/src/Services/Incident/Incident.Api/Incident.Api.csproj).
- **FE:** `FE/package.json`, cấu hình build, điểm khởi động ứng dụng, HTTP client dùng chung và các thành phần mà màn hình hiện tại import.

Sau khi các phần này được tích hợp, PR tích hợp cần ghi rõ phiên bản SDK, lệnh cài đặt và chạy, cấu hình môi trường mẫu và các kiểm thử đã chạy.

## Đóng góp theo phần được giao

```bash
git clone https://github.com/chanqua321/MedEquipSLA.git
cd MedEquipSLA
git switch main
git pull --ff-only
git switch -c feature/ten-thanh-vien-ten-cong-viec
```

Đặt mã backend trong `BE/`, mã frontend trong `FE/`. Stage các file thuộc công việc của bạn và xem lại bằng `git diff --cached --stat` trước khi commit. Khi mở PR, ghi rõ chức năng, phụ thuộc cần merge cùng và cách đã kiểm thử. Giữ `.env`, thông tin đăng nhập, khóa bí mật, dữ liệu local và file sinh từ build ngoài Git.
