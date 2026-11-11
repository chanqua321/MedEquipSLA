# Scaffold verification — 23/10/2026

> Snapshot lịch sử của scaffold baseline f4d0f24. Những dòng chưa migration/chưa mapping bên dưới mô tả thời điểm baseline, đã được thay bằng [database verification](../database/VERIFICATION.md) sau Code First. Audit hiện tại ở [Documentation Verification](DOCUMENTATION-VERIFICATION.md); không dùng snapshot này để suy ra database hiện chưa có bảng.

Đây là kết quả kiểm tra foundation hiện tại, **không phải nghiệm thu Final Assignment**. [Compliance Matrix](./PRN232-CHECKLIST.md) giữ nguyên các phần nghiệp vụ còn thiếu.

## Nguồn yêu cầu trước khởi tạo

Đọc/extract đủ 3 trang PDF chính thức trước khi tạo MedEquipSLA.sln. Bản trong repository có SHA256:

```text
36c0fa0026f1d58656b5e51280140c1246df50426269c16cd7af86ec43808fe3
```

Source user cung cấp và bản copy trong repository được giữ nguyên; Business Rules là Proposed, không tự đổi thành Approved. README đã báo các khác biệt CRUD/Delete, internal REST/gRPC và các quyết định BR còn mở trước khi viết business code.

## Kết quả thực tế

| Kiểm tra scaffold | Kết quả | Phạm vi bằng chứng |
|---|---|---|
| dotnet restore MedEquipSLA.sln --locked-mode | PASS | SDK 10.0.402, 25 project và lockfiles; NuGet audit không bị tắt |
| dotnet build MedEquipSLA.sln -c Release --no-restore | PASS | 25 project, 0 warnings, 0 errors |
| npm ci / npm run build | PASS | Node 24.19.0, TypeScript + Vite 7.3.7, dist sinh thành công |
| npm audit sau bản vá | PASS tại thời điểm kiểm tra | 0 vulnerabilities được báo; không phải bảo đảm tuyệt đối về bảo mật |
| NuGet vulnerable include-transitive | PASS tại thời điểm kiểm tra | [nuget-audit.json](./nuget-audit.json); không dùng NoWarn để che advisory |
| Project graph / layer checks | PASS | 25 project, graph acyclic, 5 DbContext, Domain không framework refs; links Markdown tồn tại |
| Native service start | PASS | 5 API + Gateway chạy process độc lập, mỗi health/live HTTP 200 |
| Swagger/OpenAPI runtime | PASS | 5 service trả JSON hợp lệ ở /swagger/v1/swagger.json; chưa đủ business paths |
| JWT guard | PASS một negative case | Equipment /api/equipment không token -> 401; chưa kiểm tra login/refresh hoặc token hợp lệ |
| Gateway proxy | PASS một route | /equipment/health/live -> Equipment health 200 |
| Incident -> Equipment gRPC | PASS transport probe | HTTP diagnostic -> generated client -> HTTP/2 GetServiceInfo, businessLookupImplemented=false |
| Maintenance -> Equipment gRPC | PASS transport probe | Như trên, không thay business GetEquipment chưa implement |
| Docker Compose config --quiet | PASS static | Cấu hình được nội suy bằng giá trị validation-only tạm trong process; không ghi .env hoặc secret vào repo |
| Docker image tags | PASS registry lookup | Kiểm được 6 tag image bằng docker manifest inspect, chưa pull/build/run |
| Docker image build / full-stack deployment | BLOCKED | Docker Linux Engine named pipe không tồn tại, chưa thể chạy daemon API |
| SQL Server bootstrap / DB ownership permissions / migrations | NOT RUN | Không có engine; bootstrap chưa chạy, chưa có migration nghiệp vụ |
| Redis app producer/consumer / Outbox/Inbox | NOT IMPLEMENTED | Adapter/type/skeleton không phải runtime business proof |
| Maintenance/SLA jobs | NOT IMPLEMENTED | Hosted services disabled; bật chỉ log tick, không có nghiệp vụ |
| Secrets/.gitignore | PASS static | Không stage .env/private key/build cache; git check-ignore xác nhận env, PEM và node_modules bị ignore |
| GitHub Private repository / push | PASS repository operation | Đã xác minh tài khoản qua Git Credential Manager, tạo Private repo và push main; URL ở phần Git evidence |
| GitHub Actions | RUNNING WHEN CHECKED | Push tạo CI run; không đồng nghĩa CI hoặc nghiệp vụ đã đạt |

15 native assertions được lưu tại [native-smoke-results.json](./native-smoke-results.json). Process thử nghiệm đã được dừng sau kiểm tra. Các lệnh chạy lại được ghi trong README và gRPC guide; không đòi hỏi đường dẫn SDK cá nhân trong source.

## Dependency fixes đã thực hiện

Restore ban đầu phát hiện System.Security.Cryptography.Pkcs 9.0.11 qua dependency gián tiếp. Đã pin bản vá 10.0.13 bằng CentralPackageTransitivePinningEnabled; restore/build sau đó không còn advisory đó. Frontend Vite 7.3.1 được nâng bản vá 7.3.7, lockfile được cập nhật bản vá esbuild tương thích bằng npm audit fix không force; npm ci/build và audit được chạy lại.

## Giới hạn và việc chưa triển khai

- JWT issuance/refresh, AuthorizationVersion, Department Isolation và role-based business checks chưa hoàn thành.
- REST CRUD/search/filter/sort/pagination, domain invariants và acceptance tests nghiệp vụ chưa hoàn thành.
- Equipment gRPC GetEquipment còn UNIMPLEMENTED sau auth; probe không trả dữ liệu thiết bị giả.
- Outbox/Inbox types chưa map vào EF; consumer, retry/DLQ, jobs, email và SignalR chưa implement.
- Chưa tạo business migrations vì model còn phải đồng bộ BR review, không dùng bootstrap SQL làm bằng chứng hoàn thành Code First.
- Dockerfile/Compose chưa chạy thực; manifest/config validation không thay image build hoặc deployment proof.
- CI workflow đã được GitHub khởi chạy sau push; kết quả theo từng commit nằm tại Actions, không suy ra thành công từ kết quả local.

## Git evidence

Commit chứa tài liệu này là scaffold baseline. Dùng `git rev-parse HEAD` và `git status --short` để lấy hash/trạng thái hiện tại; hash cuối được báo trong phản hồi cho người dùng. Repository Private: [chanqua321/MedEquipSLA](https://github.com/chanqua321/MedEquipSLA). Remote origin dùng HTTPS qua Git Credential Manager, không chứa token. Baseline scaffold f4d0f24863807f12d63d1f5f7c531094164b2d7d đã được push và đối chiếu bằng git ls-remote. [GitHub Actions](https://github.com/chanqua321/MedEquipSLA/actions).

Static structure evidence: [structure-check.json](./structure-check.json). Không chạy integration tests SQL/Redis khi Docker Engine không khả dụng.
