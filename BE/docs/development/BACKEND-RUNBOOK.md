# Chạy Backend và Swagger

Ngày 07/11/2026. Hướng dẫn cho team; không tự thực thi migration ứng dụng trong lượt triển khai hiện tại.

## Chuẩn bị

```powershell
pwsh ./scripts/Initialize-Development.ps1
dotnet tool restore
dotnet restore --locked-mode
dotnet build MedEquipSLA.sln -c Release --no-restore
docker compose --env-file .env config --quiet
```

Script giữ nguyên `.env`/key cũ. `.env`, `.secrets`, `.tools`, bin/obj/TestResults được ignore. Private PEM chỉ mount trong Identity. DataProtection key phải bền qua restart và dùng chung giữa replicas; Compose có volume riêng. Không tạo lại key mỗi lần chạy.

## Database và Docker

```powershell
docker compose up -d sqlserver redis db-bootstrap
# Chỉ deployment operator chạy sau review SQL và backup:
docker compose --profile tools run --rm db-migrate
docker compose up -d --build identity equipment maintenance incident notification gateway
```

Bootstrap tạo 5 catalog/user riêng; runtime chỉ datareader/datawriter. `db-migrate` là job một lần dùng deployment credential, chạy SQL idempotent xuất từ EF. Không để replicas production tự migrate startup. App databases chưa được nâng migration trong phiên này; lệnh trên dành cho khi chủ dự án cho phép triển khai.

SQL, Redis và DataProtection có volumes. Không dùng `down -v` để nâng schema. Docker Engine đã khả dụng: 6 images build PASS, toàn bộ BE Docker E2E exit 0 trên hạ tầng test riêng; SQL Server 2022/Redis regression 157/157 PASS. Application databases chưa được nâng schema trong lượt này.

## Một Admin và MFA

Operator đặt `BootstrapAdmin__UserName`, `BootstrapAdmin__Email`, `BootstrapAdmin__Password` trong terminal riêng, rồi chạy sau khi schema sẵn sàng:

```powershell
docker compose run --rm --no-deps -e BootstrapAdmin__Run=true -e BootstrapAdmin__UserName -e BootstrapAdmin__Email -e BootstrapAdmin__Password identity
```

Xóa biến password khỏi terminal sau lệnh, không lưu source. Bootstrap từ chối khi đã có Admin. Admin chưa có phiên quản trị trước khi thiết lập TOTP.

Swagger Identity: `POST /api/auth/mfa/setup` với username/password/method=Totp → nhập secret hoặc `authenticatorUri` vào Authenticator → `/mfa/confirm` với userId/setupToken/code. Lưu 10 Recovery Codes một lần tại nơi riêng. Login bằng password + code hoặc recoveryCode. Code đã dùng khi confirm không dùng lại để login cùng time step. Admin không được disable MFA thông thường; khôi phục tài khoản Admin nằm ngoài phạm vi đã chốt.

## Các cấu hình

| Cấu hình | Ý nghĩa |
|---|---|
| ConnectionStrings:Database | Catalog của owner, runtime credential |
| ConnectionStrings:Migration | DDL credential development, cùng server/catalog, không thay runtime |
| Migrations:ApplyOnStartup | Development + opt-in; không dùng replicas production |
| Jwt:PrivateKeyPath/PublicKeyPath | RS256, private chỉ Identity |
| Identity:Address / Equipment:Address | Lookup và xác minh phiên REST theo C1 |
| Grpc:EquipmentAddress | HTTP/2 riêng, không phải REST port |
| Grpc:*RegistrationKey | Incident/Schedule registration kèm JWT người dùng |
| Grpc:MaintenanceWorkOrderKey | Worker-only, khác JWT Admin |
| Grpc:Retirement*Key / RetirementSourceKey | Credential ràng buộc source cho retirement/abort |
| Identity:RecipientLookupKey | Lookup chỉ đọc người nhận/email/quyền Technician, không cho client |
| Workers:Enabled | Scheduler, outbox, consumer, đối soát theo service |
| Workers:WorkOrderGeneration:Enabled | Bật sinh WorkOrder, đã bật trong Compose |
| Workers:Retirement:Enabled | Bật source poll/report, đã bật trong Compose |
| Email:Enabled | Opt-in SMTP; Inbox vẫn lưu khi tắt gửi |
| Email:Host/Port/From/EnableSsl/Username/Password | SMTP; Sent chỉ sau ACK |
| Identity:PublicWebAddress | Base URL activate/reset; FE tương ứng ở giai đoạn sau |
| Identity:DataProtectionPath | Kho key bền giải mã ticket/TOTP, không đưa Git |

`.env` chỉ được Compose đọc; `dotnet run` không tự nạp. Launch profile LocalDB có auto-migrate development; chưa Start vào app DB schema cũ khi chưa được phép nâng migration.

## Demo API

1. Admin MFA/login → tạo/import nhân viên. Excel A:D: `Email, FullName, Role, DepartmentId`; preview, partial success, duplicate không tạo lại. Activation một lần 24h, resend vô hiệu link cũ. Reset password 30 phút thu hồi mọi phiên.
2. Equipment: tạo khoa/category/asset, RowVersion update, chuyển khoa giữ history/Outbox.
3. Staff tạo Incident trong khoa với UUID Idempotency-Key; Admin assignment → Technician start → multipart attempts cần PNG/JPEG ≤5 MB → Admin accept/reject. SLA không reset khi trả lại.
4. Admin tạo schedule; due=scheduled+CompletionWindowMinutes. Scheduler → occurrence/reminder → đến kỳ tạo WorkOrder qua D3. Assignment/start/progress → ảnh + measurements khi Calibration → review.
5. Notification REST cá nhân và SignalR `notificationsChanged` đúng user. Reconnect đọc lại REST. Preferences tách email/in-app; Admin xem/retry email Failed có lý do và RowVersion.
6. Admin tra cứu Pending, retry/cancel có lý do, RowVersion và key. Timeout vẫn Pending. End schedule giữ lịch sử, release chỉ sau hết blocker.
7. Begin RetirementIntent → source Frozen/Sealed → Admin Continue → Completed chỉ khi Equipment hết blocker. Candidate list chỉ là snapshot local, chưa phải chứng cứ đủ điều kiện Retire.

## Lỗi và phục hồi

401: JWT/phiên/quyền hiện hành không hợp lệ hoặc không xác minh được; 403: role; 404: không có/ngoài scope; 409: RowVersion, key khác payload hoặc state conflict; 503: dependency/credential. Không đổi key để retry mutation chưa rõ kết quả.

SMTP at-least-once: crash sau ACK trước SQL commit có thể gửi lại cùng Message-ID. Không cam kết SMTP exactly-once. Redis Inbox chống xử lý lặp sau SQL commit. Tests chỉ dùng catalog/stream riêng; không flush/reset dữ liệu ứng dụng.
