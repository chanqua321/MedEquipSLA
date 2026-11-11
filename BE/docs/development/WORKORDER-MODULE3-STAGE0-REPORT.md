# Module 3 — Stage 0 report
Ngày 05/11/2026. Branch giữ nguyên: feature/database-code-first.

## 1. Mục tiêu và kết quả
Đã re-audit source và quyết định của yêu cầu Module 3 mới, tạo [audit](WORKORDER-MODULE3-STAGE0-AUDIT.md) và chuyển tiếp sang [Stage 1 design](WORKORDER-MODULE3-STAGE1-DESIGN.md). Option A và Retire tối thiểu đã được chủ dự án phê duyệt trong yêu cầu mới. Không còn hai blocker của lượt audit trước.

Không phát hiện mâu thuẫn nghiệp vụ buộc dừng. Điều kiện thanh lý hiện hành vẫn phải kiểm tra cả Incident/Schedule/WorkOrder Pending hoặc Active, không bỏ vì Module 3 tập trung WorkOrder.

## 2. File tạo/sửa
- Tạo docs/development/WORKORDER-MODULE3-STAGE0-AUDIT.md.
- Tạo docs/development/WORKORDER-MODULE3-STAGE0-REPORT.md.
- Stage 1 có hai tài liệu riêng, liệt kê ở Stage 1 report.
Không sửa source ứng dụng, frontend, package, migration hoặc Git branch trong Stage 0/1. Checkout đã có nhiều thay đổi từ các lượt trước; giữ nguyên.

## 3. Nghiệp vụ, database và transaction
Chưa thay đổi nghiệp vụ hoặc schema. Read-only SQL audit thấy cả 5 MedEquip databases ONLINE, compatibility 160, chỉ có InitialCreate. Incident chưa apply AddIncidentOperationPhoto; Maintenance chưa apply AddOccurrenceReminderWindow vào database ứng dụng. Equipment/Maintenance hiện không có dữ liệu nghiệp vụ/operation/registration ở thời điểm đọc.

Test dùng SQL Server LocalDB thật với catalog fixture riêng. Chỉ cleanup các catalog fixture sở hữu; không reset hoặc thay đổi database ứng dụng.

## 4. Kiểm chứng thực tế
| Kiểm tra | Kết quả | Bằng chứng và giới hạn |
|---|---|---|
| Build Release toàn solution | PASS | dotnet build MedEquipSLA.sln --no-restore -c Release -m:4 --nologo -v:q; exit 0; 0 warning, 0 error; 9.76 giây |
| Database Integration Tests hiện có | PASS | 61 passed, 0 failed, 0 skipped; duration 35s; LocalDB + Redis 8.4.2 thật; không dùng kết quả lịch sử |
| Module 1 occurrence regression | PASS | Có trong suite 61 tests; planner/safety tests chạy lại |
| Module 2 Reminder/Inbox regression | PASS | Có trong suite; gồm SQL rollback và migration snapshot reminder |
| Real Redis regression | PASS | 4 RedisReminderTests chạy với REDIS_TEST_CONNECTION loopback 16379/database15, không skipped |
| Gateway/HTTP/gRPC/Identity recipient smoke | PASS | tests/check_backend_workflows.py chạy exit 0, 15 nhóm PASS; thực tế Schedule D3/restart/concurrency, recipient auth và stale writes |
| Migration history database ứng dụng | PASS | sqlcmd read-only xác minh 5 DB; chỉ InitialCreate. PASS ở đây chỉ có nghĩa phép kiểm tra đã chạy, không có nghĩa schema ứng dụng đã cập nhật |
| Module 3 generation/Retire/Abort/fencing runtime | NOT TESTED | Chưa có implementation; các kết quả trên chỉ baseline |
| Migration Module 3 Up/Down/reapply | NOT TESTED | Chưa sinh migration |
| Docker Compose toàn hệ thống | NOT TESTED | Không chạy trong Stage 0; không dùng Redis qua WSL thay bằng chứng Docker Compose |

TRX local (được .gitignore loại trừ):
`tests/MedEquipSLA.Database.IntegrationTests/TestResults/module3-stage0-baseline.trx`
SHA256: `6BFC565EBB87B972766F7F0A0DF9706A279B5B08D365A535F516368692E9F9B5`.

API smoke logs local: `.tools/identity-web-smoke/bcb750fa40845a22-*.log`. Script dùng catalog test có suffix riêng, cleanup khi kết thúc. Log bootstrap/private key của test không được đưa vào repository. Redis test chạy nonpersistent, chỉ loopback, đã tắt sau kiểm chứng.

Lệnh tái lập suite (PowerShell, tại repository):
```powershell
$env:SQLSERVER_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
$env:REDIS_TEST_CONNECTION='127.0.0.1:16379,defaultDatabase=15,abortConnect=false,connectTimeout=2000'
& ./.tools/dotnet/dotnet.exe test tests/MedEquipSLA.Database.IntegrationTests --no-build -c Release --nologo --logger 'trx;LogFileName=module3-stage0-baseline.trx'
```
Redis phải là instance kiểm thử riêng đang chạy. Fixture từ chối stream có sẵn, không dùng FLUSHDB. Chạy API smoke bằng Python 3 khả dụng và SDK đã build; script tự tìm .tools/dotnet.

## 5. Diff và rủi ro
git diff --check toàn checkout trả exit 1 do blank line EOF có sẵn ở docs/database/migrations/Incident.sql, Maintenance.sql và src/Web/src/main.tsx; không sửa các thay đổi ngoài Stage 0 để làm đẹp kết quả. Bốn file tài liệu Module 3 mới được kiểm tra whitespace riêng.

Thiếu implementation Module 3, protocol snapshot reconciliation toàn hệ thống và workflow release Schedule là giới hạn thực tế. Pending Schedule registration tiếp tục chặn Retire; không lấy Inactive làm bằng chứng release. Chưa thể dùng báo cáo này để kết luận toàn bộ D3 hoặc backend hoàn tất.

## 6. Stage tiếp theo
Stage 1 đã lập contract/database design và self-review crash boundaries. Stage 2 sẽ triển khai Equipment RPC/registration/Retire và test độc lập trước khi viết generation/reconciliation Maintenance. Không commit hoặc push.
