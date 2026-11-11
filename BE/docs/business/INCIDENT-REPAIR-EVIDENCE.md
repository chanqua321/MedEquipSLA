# Bằng chứng hoàn thành sửa chữa thiết bị

Một lần báo sửa xong cần biên bản, kết quả chạy thử và ảnh cho thấy thiết bị đã chạy sau sửa. Ảnh JPEG hoặc PNG tối đa 5 MB được lưu cùng lần xử lý. Hệ thống kiểm tra kích thước và dấu hiệu định dạng file. Người nghiệm thu đối chiếu nội dung ảnh và kết quả chạy thử.

## Kiểm tra trên Swagger và Web

Chạy Identity, Equipment, Gateway và Web theo [hướng dẫn local](IDENTITY-RBAC-LOCAL-REVIEW.md). Mở terminal mới tại thư mục solution để chạy Incident:

```powershell
$env:Jwt__PublicKeyPath = (Resolve-Path .secrets/identity-public.pem).Path
dotnet run --project src/Services/Incident/Incident.Api --launch-profile Incident
```

Profile Development áp dụng migration `AddIncidentOperationPhoto` vào database Incident. Mở Swagger của Identity tại `http://localhost:5101/swagger/index.html` và đăng nhập qua `POST /api/auth/login`. Dùng Access Token ở nút Authorize trên Swagger Incident tại `http://localhost:5105/swagger/index.html`.

1. Gọi `GET /api/incidents` để lấy hồ sơ `InProgress` và `rowVersion`.
2. Gọi `POST /api/incidents/{id}/attempts` với `notes`, `result`, `rowVersion` và file `photo`. Quyền gửi thuộc Admin hoặc Technician đang được giao hồ sơ.
3. Kiểm tra hồ sơ chuyển sang `Resolved` và lần xử lý có trạng thái `Pending`. Mở ảnh qua `GET /api/incidents/{id}/attempts/{attemptId}/photo`.
4. Thử lại trên Web ở mục **Sự cố và ảnh sửa chữa**. Form xuất hiện khi chọn hồ sơ đang xử lý mà tài khoản có quyền cập nhật.

Với hồ sơ có sẵn ở trạng thái `Open`, Admin phân công bằng `PUT /api/incidents/{id}/assignment` với `technicianId` và `rowVersion`. Incident xác minh kỹ thuật viên qua REST nội bộ của Identity. Kỹ thuật viên được giao gọi `POST /api/incidents/{id}/start` với phiên bản mới để tiếp nhận. Tải lại hồ sơ sau mỗi thao tác để lấy phiên bản hiện hành.

Khi hồ sơ ở `Resolved`, Admin có thể trả lại bằng `POST /api/incidents/{id}/attempts/{attemptId}/reject` với `reason` và `rowVersion`. Hồ sơ trở về `InProgress`. Lần sửa cũ giữ ảnh và lý do trả lại. Kỹ thuật viên có thể nộp lần sửa mới. Các thao tác tương ứng xuất hiện trên Web theo vai trò.

Tạo ticket và nghiệm thu đóng ticket còn phụ thuộc D3 và đối soát đăng ký thiết bị. Hồ sơ cũ có thể thiếu ảnh. Quy tắc bắt buộc áp dụng cho lần xử lý mới qua API.
