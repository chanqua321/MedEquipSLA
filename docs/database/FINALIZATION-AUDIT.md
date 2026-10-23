# Database finalization audit — 23/10/2026

Baseline kiểm tra: commit 3714a87. Không sinh lại InitialCreate; năm migration và 47 bảng ứng dụng đã tồn tại từ commit 9c4a46a. Lượt này bổ sung EF integration tests/rollback và đồng bộ phụ lục BR, không đổi schema hoặc kiến trúc.

## Phase 1 — kết luận trước implementation

Đã đối chiếu DATABASE-DOCUMENTATION, BUSINESS-DOCUMENTATION, BR/review, erd-model.json/dictionaries, technical tables, C0/C1, Compliance Matrix với Entity/DbContext/Fluent API/migrations hiện có.

| Hạng mục audit | Kết luận |
|---|---|
| Entity/quan hệ và ownership | 5 owner, 32 business/Identity + 15 technical local copies; không thêm entity ngoài ERD |
| PK/FK và logical references | Composite keys đúng thiết kế, FK nội bộ, ID ngoài service chỉ REF; không navigation xuyên database |
| SQL type/nullability | 515 cột theo dictionary; kiểm chứng model và SQL schema độc lập |
| Unique/filtered indexes | Asset/Identity, occurrence/WO, accepted attempts, registration, reminder/alert, Inbox/idempotency/notification theo ERD |
| Check constraints | 87 constraint hiện có; không tự coi chúng là domain workflow |
| RowVersion/AggregateVersion | SQL-generated concurrency token khác version thứ tự event; application tăng aggregate version còn Planned |
| Audit/Outbox/Inbox | Owner-local mappings có; automatic audit/event processing/atomic commands chưa implement |
| DeleteBehavior | Mọi FK NoAction, không cascade lịch sử |
| ResolutionAttempt / D1 | Lưu riêng Completed/Reviewed, rejected history, unique Accepted; final outcome handler Planned |
| ScheduleOccurrence / D2 | Unique schedule/kỳ và filtered WO.OccurrenceId, reminder trước WO; job Planned |
| EquipmentUseRegistration / D3 | Schema Approved; **fencing/abort/snapshot protocol BLOCKED**, không triển khai suy đoán |
| Calibration Failed / D4 | Lưu result/measurement/review; transition/blocking enforcement tại Domain/Application Planned |
| Repository | Owner-specific abstractions và EF Exists/read adapters đã có; chưa cần generic repository hoặc thêm CRUD ngoài phạm vi |
| Domain independence | Domain không tham chiếu EF/ASP.NET; test aggregator tham chiếu nhiều Infrastructure chỉ phục vụ kiểm thử |

D1–D4 được đồng bộ bằng [phụ lục BR đã duyệt](../business/BUSINESS-RULES-D1-D4-APPROVED.md). BR gốc vẫn Proposed; không dùng phụ lục để tự chốt authorization freshness, Delete semantics, typed events hoặc phần protocol D3 còn mở.

## Migrations được giữ nguyên

| Owner | InitialCreate | Bảng ứng dụng |
|---|---|---:|
| Identity | 20261023044348_InitialCreate | 10 |
| Equipment | 20261023044354_InitialCreate | 11 |
| Maintenance | 20261023044400_InitialCreate | 10 |
| Incident | 20261023044406_InitialCreate | 9 |
| Notification | 20261023044411_InitialCreate | 7 |

Danh sách entity/table đầy đủ ở [Database Documentation](../DATABASE-DOCUMENTATION.md); data dictionaries/ERDs không bị thay đổi. Không auto-migrate ở API startup, không EnsureCreated, không apply production.

## Verification lượt này

Kết quả sẽ được ghi từ lần chạy thực: restore/build, 5 EF model cases, SQL migration cycle Up→0→Up cho từng service, exact SQL indexes/filters, schema/behavior suites và EF concurrency/transaction tests. Runtime tests phải Skip/BLOCKED nếu không cấu hình SQL Server; không dùng InMemory để thay.

Test SQL chỉ nhận LocalDB hoặc SQL Server loopback port 14333, base catalog master, tạo DB mới MedEquip_<Owner>_Test_<randomGuid>. Fixture chỉ cleanup DB do chính nó CREATE thành công; không rollback/drop năm DB ứng dụng đang có. Các lockfiles đã bị thay đổi trong checkout gốc được giữ nguyên, không đưa vào PR này.


## Kết quả thực tế

- Restore locked và Release build 26 projects thành công, 0 warnings/errors.
- Chạy không có SQL env: 5 model passed, 4 skipped groups (3 facts + 1 theory cho 5 owners), skip reason BLOCKED; xác minh không báo runtime PASS giả.
- **13 passed, 0 failed, 0 skipped** trên SQL Server LocalDB 2025, compatibility 160: 5 model + 5 migration cycle + 3 EF behavior cases.
- Mỗi migration cycle apply InitialCreate, đối chiếu schema/index/check, chạy SQL behavior, Down về 0 (không application tables/history rows), Up lại và chạy lại suites.
- Đối chiếu 74 non-PK indexes (name/key order/unique/filter); 87 check constraint identities/count/enabled/trusted. SQL Server chuẩn hóa IN thành OR nên không so raw SQL text để kết luận khác nghĩa; negative behavior tests kiểm tra dữ liệu bị từ chối.
- Test EF xác nhận rowversion cũ ném DbUpdateConcurrencyException, rollback aggregate+Outbox, SaveChanges failure rollback thay đổi khác.
- Chạy lại 10 SQL suites trên 5 application DB đều PASS; các DB vẫn ONLINE, compatibility 160; không còn test DB sau cleanup.
- [Sanitized test evidence](ef-integration-results.json), [cách chạy](../../tests/MedEquipSLA.Database.IntegrationTests/README.md). TRX đầy đủ sinh trong TestResults (gitignored), CI upload artifact theo run.
- Docker Engine local vẫn unavailable; thêm CI job SQL Server 2022 container để kiểm chứng baseline nhóm. Không đồng nhất LocalDB với full Docker deployment.

## Phần còn mở

D3 protocol là BLOCKED như đã báo trước implementation; authorization freshness, Delete semantics, typed events/retry và domain lifecycle/outcome handlers vẫn Planned. Không sửa Entity/Fluent API/InitialCreate hoặc thêm bảng chỉ để tự chốt các điểm đó. Repository Exists/read adapters hiện có được giữ nguyên; generic repository/CRUD không phát sinh trong task database này.
