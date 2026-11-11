# Bắt đầu với MedEquipSLA

Cập nhật 07/11/2026. Dùng checkout hiện tại; không tạo bản clone/worktree trùng. Thành viên mới đọc [README gốc](../../README.md), [Business](../BUSINESS-DOCUMENTATION.md), [API](../API-DOCUMENTATION.md), [Database](../DATABASE-DOCUMENTATION.md) và README service mình phụ trách.

1. Cài SDK theo global.json và Docker Desktop Linux Engine. SQL Server 2022/Redis Compose là baseline chung, không buộc mọi máy dùng LocalDB.
2. Chạy `pwsh ./scripts/Initialize-Development.ps1`, `dotnet tool restore`, `dotnet restore --locked-mode`, `dotnet build MedEquipSLA.sln -c Release --no-restore`.
3. Theo [Backend runbook](BACKEND-RUNBOOK.md) để tạo hạ tầng, operator migration một lần, bootstrap Admin và TOTP. Không Start binary mới vào app database InitialCreate còn cũ.
4. Swagger trên cổng 5101/5102/5104/5105/5106, Gateway 5100, Equipment gRPC 5103. Chạy luồng nghiệp vụ trong runbook, không dùng health thay bằng chứng.
5. Theo [test guide](../../tests/MedEquipSLA.Database.IntegrationTests/README.md) cho database test riêng, Redis/SMTP/SignalR và HTTP/Docker E2E.
6. Đọc [Team](TEAM-RESPONSIBILITIES.md), [Git/PR](GIT-WORKFLOW.md), tạo feature branch và cập nhật evidence thực. Không tự bịa đóng góp hay force push.

FE được xử lý sau BE theo yêu cầu chủ dự án; lượt này không sửa hoặc nghiệm thu FE. Tình trạng hiện hành và giới hạn ở [tracker](BACKEND-COMPLETION-TRACKER.md).
