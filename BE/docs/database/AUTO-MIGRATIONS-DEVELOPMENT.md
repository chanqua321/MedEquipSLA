# Tự apply migration khi Start/Run — Development

Theo yêu cầu owner ngày 29/10/2026, mỗi service có startup hook **chỉ ở Development và khi Migrations:ApplyOnStartup=true**. Đây là ngoại lệ development đối với quy trình migration thủ công trước đây; không thay quy trình deployment/production.

## Bấm Start trong Visual Studio hoặc chạy CLI

Chọn project API làm Startup Project và profile đúng tên service: Identity, Equipment, Maintenance, Incident hoặc Notification. Nhấn Start/F5. Profile mặc định này dùng **SQL Server LocalDB trên Windows**, Windows Authentication, không password trong source. Cần .NET SDK theo global.json và LocalDB đã cài. LocalDB sẽ được kết nối và tự khởi động qua SQL client nếu instance mặc định sẵn có; có thể chạy `SqlLocalDB start MSSQLLocalDB` để kiểm tra.

```sh
dotnet run --project src/Services/Equipment/Equipment.Api --launch-profile Equipment
```

Chạy các service khác bằng tên project/profile tương ứng. Chỉ service được Start mới migrate database của nó; muốn cả 5 thì đặt Multiple Startup Projects cho 5 API trong Visual Studio hoặc chạy từng service. Gateway/Web không có database nên không chạy migration.

Luồng startup: đọc config → kiểm tra Development + flag → kiểm tra catalog đúng owner → EF Core MigrateAsync → chỉ sau thành công mới mở API/khởi động hosted workers. EF dùng migration history và migration lock SQL Server; lần sau chỉ apply migration còn thiếu, không tạo trùng hoặc xóa dữ liệu. Hai phút là timeout của bước migration development. Nếu thất bại, startup dừng và log lỗi tổng quát; dùng EF CLI để chẩn đoán kết nối/quyền chi tiết, không log secret.

Startup không tự sinh migration từ entity. Khi model thay đổi phải tạo migration mới bằng EF CLI, review rồi Start để apply. Không EnsureCreated, không tự drop/reset DB, không tự seed user/password/demo business data.

## SQL Server khác / thành viên không dùng LocalDB

Chọn profile `<Service>-SqlServer` (ví dụ Equipment-SqlServer) và đặt ConnectionStrings__Database riêng cho service qua environment/secrets phù hợp. Profile này không đặt LocalDB connection. Không đổi baseline team thành LocalDB; Compose vẫn dùng SQL Server 2022.

```sh
dotnet run --project src/Services/Equipment/Equipment.Api --launch-profile Equipment-SqlServer
```

Nếu runtime user chỉ có datareader/datawriter, cấu hình thêm **ConnectionStrings__Migration** bằng deployment credential có DDL trên đúng database. Startup dùng Migration connection nếu có, nếu không dùng Database connection. Không cho runtime user quyền DDL chỉ để bật Start. Sau migrate, DI runtime vẫn dùng Database credential. Hai connection phải trỏ cùng database server/catalog; không cấu hình một connection cho cả năm service.

Nếu thiếu SQL/password/quyền hoặc catalog sai owner, API dừng thay vì trả vẻ như schema đã sẵn sàng. Identity/Equipment/Maintenance/Incident/Notification đều dùng cùng cơ chế với context riêng.

## Tắt tự apply

- Đặt `Migrations:ApplyOnStartup=false` trong cấu hình có precedence cao hơn launch env, ví dụ CLI: `dotnet run --project src/Services/Equipment/Equipment.Api --launch-profile Equipment -- --Migrations:ApplyOnStartup=false`.
- Chạy `--no-launch-profile` thì launch settings không được áp dụng; flag mặc định false. Cần đặt env/cổng theo môi trường nếu chạy process trực tiếp.
- Production/Staging **không chạy hook kể cả flag=true**. Docker Compose hiện không bật flag, vẫn apply migrations thủ công qua deployment job/script; không để nhiều replica production tự migrate khi startup.

## Kiểm chứng

[EF integration tests](../../tests/MedEquipSLA.Database.IntegrationTests/README.md) kiểm tra cả 5 startup callbacks trên DB test riêng: apply rồi chạy lại, chỉ có một history row; catalog sai bị từ chối; disabled/non-Development không gọi migration; lỗi không lộ provider secret. Không rollback/reset database ứng dụng. Xem [Database Setup](SETUP.md) để chạy migration thủ công khi cần.

Kết quả local ngày 29/10/2026: **27 passed, 0 failed/skipped**, Release build 0 warnings/errors. Đã khởi động cả 5 API với các env trong profile Start; mỗi service log database ready trước khi health trả 200. Process kiểm thử được dừng sau xác minh. Không tạo/sửa migration baseline hoặc schema trong thay đổi startup này.
