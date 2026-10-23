# EF Core SQL Server integration tests

Test project độc lập với runtime service graph, tham chiếu năm Infrastructure chỉ để kiểm chứng ownership/model/migrations. Không thêm business tables hoặc domain behavior.

## Các case

- 5 model cases so với JSON ERD: bảng/cột/type/nullability/PK, FK NoAction trong owner, rowversion metadata, không shadow properties, một migration và không pending model changes.
- 5 SQL Server migration cycle cases: Up → schema/behavior suites → exact index name/keys/uniqueness/filter → Down về 0 → không còn application tables/history rows → Up lại và chạy lại suites.
- 3 EF persistence cases: stale rowversion ném DbUpdateConcurrencyException; aggregate + Outbox rollback cùng transaction; Outbox JSON check thất bại làm SaveChanges rollback các thay đổi khác.

Không kiểm tra D3 fencing/reconciliation, SLA outcome hoặc domain transitions ở đây; các workflow đó chưa implement. Fixture Entry.CurrentValues chỉ dùng seed test cho entity private setter, không phải API bypass trong production.

## Chạy thực tế

Từ root, build/restore solution trước. PowerShell ví dụ dùng LocalDB:

```powershell
$env:SQLSERVER_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test tests/MedEquipSLA.Database.IntegrationTests -c Release --logger trx
Remove-Item Env:\SQLSERVER_TEST_CONNECTION
```

Đối với SQL Server container riêng, nhập connection bằng secrets manager/env (host loopback port 14333, catalog master), không ghi password vào source/history. Không trỏ vào production. Base connection có existing application catalog hoặc server remote sẽ bị từ chối.

Fixture CREATE database mới tên MedEquip_<Owner>_Test_<randomGuid>, compatibility 160, rồi EF migrations tạo schema. Chỉ ghi ownership sau CREATE thành công; cleanup kiểm tra đúng tên/connection và chỉ DROP database do fixture vừa tạo. Không gọi Down/drop năm MedEquip_* databases đang dùng. Nếu CREATE đụng tên đã tồn tại thì fail và không cleanup database đó.

Không cấu hình SQLSERVER_TEST_CONNECTION: 5 model cases vẫn chạy; 8 runtime cases **Skipped với lý do BLOCKED**, không báo runtime PASS. CI job build có thể skip SQL; job database chạy SQL Server 2022 thật riêng, dùng password ngẫu nhiên masked và xóa container sau test. Xem [finalization audit](../../docs/database/FINALIZATION-AUDIT.md).

VSTest/xUnit hiển thị **5 passed + 4 skipped** khi thiếu SQL: ba Fact và một Theory bị skip trước khi Theory tách thành năm owner cases. Khi có SQL connection thì Theory được tách đầy đủ và tổng cộng **13 cases** chạy. Không so sánh hai con số skip để suy ra runtime đã được kiểm chứng.
