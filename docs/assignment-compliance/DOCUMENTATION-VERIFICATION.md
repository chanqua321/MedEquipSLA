# Documentation/foundation audit — 23/10/2026

Phạm vi: tài liệu, README, Git hygiene, build và các thành phần foundation hiện có. Không nghiệm thu Final Assignment. BR v1.0 gốc vẫn Proposed; ERD/D1–D4 Approved; Code First schema đã tạo. Kết quả sẽ được ghi theo lệnh thực chạy trong lượt audit này.

## Audit source

Đã đối chiếu solution 25 projects, BR/review/D1–D4, C0/C1, đủ 3 trang đề PRN232, JSON ERD/data dictionaries năm DB, DbContexts/Fluent configurations/migrations, Program.cs/handlers, proto/events, Dockerfiles/Compose, frontend proxy, Git remote/status/ignore. Không có business API/controller bị bỏ qua ngoài route inventory đã ghi.

Ba tài liệu chính: [Business](../BUSINESS-DOCUMENTATION.md), [API](../API-DOCUMENTATION.md), [Database](../DATABASE-DOCUMENTATION.md). Root/service READMEs và guides onboarding liên kết từ [docs index](../README.md). Các endpoint/DTO mới chỉ Planned; historical scaffold evidence được đánh dấu snapshot.

## Kiểm tra lượt này

| Kiểm tra | Trạng thái |
|---|---|
| Markdown relative paths/anchors | PASS qua scripts/Test-Documentation.py, các local links/anchors đều tồn tại |
| Restore/build/backend test command | Restore locked PASS; Release build 25 projects, 0 warnings/errors; dotnet test exit 0 nhưng **0 test projects**, không có unit test coverage |
| Frontend clean install/build | npm ci và npm run build PASS, npm báo 0 vulnerabilities tại lần kiểm tra |
| Native health/OpenAPI/Gateway/gRPC/JWT negative probe | PASS 15 assertions: 6 health, 5 OpenAPI, 2 REST→gRPC probes, 1 missing-JWT 401, 1 Gateway proxy |
| SQL schema/behavior checks | PASS 10 suites trên 5 LocalDB databases, 47 application tables; rollback mọi fixture |
| Compose config | PASS --quiet với 6 giá trị validation tạm trong process; không ghi .env |
| Docker Engine/full deployment | BLOCKED: Linux Engine pipe chưa khả dụng khi audit |
| Secret scan current source + Git history | PASS pattern scan trên selected source và toàn bộ reachable history tại baseline 9c4a46a; không phát hiện private key/token/password literal theo patterns |
| Tracked files / .gitignore | PASS: không tracked file trái ignore; 22 positive ignore + 11 keep-source cases; không stage cache, DB files hoặc credentials |

`dotnet test` hiện không có .NET test project, nên không có unit test coverage để báo PASS. SQL rollback tests là kiểm chứng persistence riêng. Full app producer/consumer, scheduler/SLA, JWT issuance/refresh, department policies, SMTP/SignalR và E2E chưa triển khai. Không đánh dấu các tiêu chí đó PASS theo docs/build.

Pattern scan không bảo đảm tuyệt đối không có secret; các candidate bị phát hiện sẽ ẩn giá trị và yêu cầu review. Script dùng stdlib Python, được thêm vào CI để kiểm tra links/ignore/current-source patterns. Kiểm tra history tùy chọn: `python scripts/Test-Documentation.py --scan-history`. Remote URLs/runtime endpoints được kiểm tra riêng.

Public documentation links: 5/5 HTTP HEAD 200 (.NET support, Microsoft distributed data/EF SQL provider/SQL platform, Node releases). GitHub Private URL/commit được xác minh bằng Git remote/API; localhost URLs bằng native smoke.

## Git scope

Remote dự kiến/đã đối chiếu: https://github.com/chanqua321/MedEquipSLA. Owner yêu cầu push main cho lượt này. Bảo toàn Code First commit 9c4a46a từ feature/database-code-first, đồng bộ main bằng fetch/merge an toàn, không force push. Hash cuối và push/CI state được báo sau khi hoàn tất; không tự ghi hash của commit chưa tạo.

## Danh sách file của lượt hoàn thiện

### Tạo mới

- [docs/API-DOCUMENTATION.md](../API-DOCUMENTATION.md)
- [docs/BUSINESS-DOCUMENTATION.md](../BUSINESS-DOCUMENTATION.md)
- [docs/DATABASE-DOCUMENTATION.md](../DATABASE-DOCUMENTATION.md)
- [docs/README.md](../README.md)
- [docs/api-contracts/README.md](../api-contracts/README.md)
- [docs/architecture/SOLUTION-STRUCTURE.md](../architecture/SOLUTION-STRUCTURE.md)
- [docs/assignment-compliance/DOCUMENTATION-VERIFICATION.md](DOCUMENTATION-VERIFICATION.md)
- [docs/database/README.md](../database/README.md)
- [docs/development/CODING-CONVENTIONS.md](../development/CODING-CONVENTIONS.md)
- [docs/development/DEVELOPMENT-WORKFLOW.md](../development/DEVELOPMENT-WORKFLOW.md)
- [docs/development/ENVIRONMENT-SETUP.md](../development/ENVIRONMENT-SETUP.md)
- [docs/development/GETTING-STARTED.md](../development/GETTING-STARTED.md)
- [scripts/Test-Documentation.py](../../scripts/Test-Documentation.py)

### Cập nhật

- [.github/workflows/ci.yml](../../.github/workflows/ci.yml)
- [.gitignore](../../.gitignore)
- [README.md](../../README.md)
- [docs/architecture/C0-System-Context.md](../architecture/C0-System-Context.md)
- [docs/architecture/C1-Container.md](../architecture/C1-Container.md)
- [docs/assignment-compliance/PRN232-CHECKLIST.md](PRN232-CHECKLIST.md)
- [docs/assignment-compliance/SCAFFOLD-VERIFICATION.md](SCAFFOLD-VERIFICATION.md)
- [docs/database/SETUP.md](../database/SETUP.md)
- [docs/database/TECHNICAL-TABLES.md](../database/TECHNICAL-TABLES.md)
- [docs/development/GIT-WORKFLOW.md](../development/GIT-WORKFLOW.md)
- [docs/development/MESSAGING.md](../development/MESSAGING.md)
- [docs/development/TEAM-RESPONSIBILITIES.md](../development/TEAM-RESPONSIBILITIES.md)
- [src/Services/Equipment/README.md](../../src/Services/Equipment/README.md)
- [src/Services/Identity/README.md](../../src/Services/Identity/README.md)
- [src/Services/Incident/README.md](../../src/Services/Incident/README.md)
- [src/Services/Maintenance/README.md](../../src/Services/Maintenance/README.md)
- [src/Services/Notification/README.md](../../src/Services/Notification/README.md)

Không sửa business source, protobuf, database model hoặc migrations trong lượt tài liệu này. Source addition chỉ là script kiểm tra tài liệu; CI gọi script đó. Các thay đổi Code First từ commit 9c4a46a được bảo toàn khi đưa nhánh lên main.
