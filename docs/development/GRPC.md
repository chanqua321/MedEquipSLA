# gRPC verification

Equipment REST port 5102 (container 8080, HTTP/1); gRPC port 5103 (container 8081, HTTP/2). Native dùng launch profile Equipment. Incident/Maintenance đọc Grpc__EquipmentAddress, native http://localhost:5103, Compose http://equipment:8081. h2c chỉ cho development network; cấu hình TLS cho deployment ngoài máy dev.

Chạy ba process theo README; dùng grpcurl và các diagnostic endpoints ở README để xác minh đường gRPC thật. GetServiceInfo chỉ ở Development. Business GetEquipment có [Authorize] nhưng chưa implement: thiếu token -> UNAUTHENTICATED, token hợp lệ -> UNIMPLEMENTED. Không trả equipment giả.

Sau khi implement, test ID tồn tại/không tồn tại, Retired, bearer metadata, department isolation, deadlines/cancellation, endpoint unavailable. Expected REST mapping và lifecycle registration cần theo BR đã chốt. Test gRPC transport probe không chứng minh lookup nghiệp vụ hoặc retire coordination đã hoàn thành.
