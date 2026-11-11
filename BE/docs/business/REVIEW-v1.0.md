# MedEquipSLA - Đánh giá Business Rules v1.0 và tính nhất quán kiến trúc

Ngày: 23/10/2026. Trạng thái: review và đề xuất để duyệt; không phải Approved Specification.

Nguồn: [Business Rules v1.0 người dùng cung cấp](./BUSINESS-RULES-v1.0-PROPOSED.md), [đề bài](../assignment-compliance/PRN232%20-%20Final%20Assignment.pdf), [C0](../architecture/C0-System-Context.md), [C1](../architecture/C1-Container.md).

## 1. Kết luận đánh giá

Luồng phù hợp domain MedEquipSLA và đủ tình huống để thể hiện REST, gRPC, background jobs, message broker và database per service. Nên dùng làm baseline nghiệp vụ để thiết kế tiếp. Chưa nên khóa Approved vì một số điều kiện nghiệp vụ chưa đủ cụ thể để code/test cho kết quả duy nhất.

Điểm nên giữ: ba vai trò và department isolation; Admin nghiệm thu trước đóng; Incident Closed không mở lại; SLA snapshot; nhiều Incident cùng ảnh hưởng một thiết bị; SQL Server Code First; CQRS trong từng database; Outbox/Inbox; phân biệt thông báo bền với SignalR và SMTP.

Chọn công nghệ dựa trên yêu cầu, vòng đời hỗ trợ và khả năng tái lập môi trường cho nhóm. Không dùng SDK, LocalDB, Node hay trạng thái Docker riêng của một thành viên làm baseline chung.

## 2. Đối chiếu đề bài và C0/C1

| Yêu cầu | Đánh giá | Việc phải thể hiện khi triển khai |
|---|---|---|
| Domain tự chọn; .NET 8 trở lên | MedEquipSLA phù hợp; .NET 10 cũng nằm trong yêu cầu | REST API độc lập, Swagger và demo CRUD |
| API -> Services -> Repository | Clean Architecture/CQRS đáp ứng nếu lớp được ánh xạ rõ | API -> Application handlers (Services) -> repository implementations |
| JWT, EF Core, relational DB | Phù hợp | JWT validation từng service, SQL Server migrations từng database |
| Search/filter/sort/pagination | BR-VAL-03 chưa bao quát hết bốn khả năng | Bổ sung acceptance test cho từng khả năng trên Equipment/Incident |
| Background job | Phù hợp | Due/reminder/SLA scanner chạy thực, có chống trùng |
| Broker Kafka hoặc Redis, producer/consumer | Redis Streams phù hợp | Log publish/consume EventId, retry sau broker downtime |
| Independent gRPC service | Equipment hoặc Identity host gRPC ở process riêng với REST caller | Demo REST Incident -> Equipment gRPC, không chỉ định nghĩa proto |
| Docker/cloud và giao tiếp end-to-end | Compose phù hợp | Build/run từ checkout sạch, health checks, migrations và seed |
| Deliverables và thuyết trình | Business Rules chưa thay thế các deliverables | Source, migration, Dockerfile, Compose, README, OpenAPI, phân công, demo |
| C0: ba người dùng, Email System bên ngoài | Phù hợp | Identity/Redis/SQL đều thuộc boundary MedEquipSLA |
| C1: database riêng, Gateway, web, events, realtime | Phù hợp về cấu trúc | Cập nhật gRPC internal và framework baseline, giữ ownership |

C0/C1 là bản thiết kế cần đồng bộ với BR; sơ đồ không tự giải quyết race condition, phân quyền hoặc SLA. Đề không bắt buộc CQRS/YARP/SignalR hay năm service, nhưng các lựa chọn này hợp với phạm vi đã đề xuất.

## 3. Những điểm cần chốt trước khi code

### R1 - Resolution SLA đang có hai cách hiểu

Liên quan BR-SLA-16/17/20/21/22 và BR-INC-14/21/22.

Ví dụ: hạn 10:00, technician Resolve lúc 09:50, Admin nghiệm thu đạt lúc 10:30. “Thời điểm hoàn tất xử lý được Admin nghiệm thu” chưa cho biết SLA đạt hay trễ.

Đề xuất: SLA dùng ResolvedAtUtc của lần xử lý cuối cùng được Admin chấp nhận; AcceptedAtUtc/ClosedAtUtc lưu riêng cho thời gian nghiệm thu. Khi bị trả lại, giữ lịch sử lần Resolve cũ và chỉ chọn AcceptedResolutionAttemptId khi Close. Nếu muốn SLA bao gồm cả chờ Admin thì phải chọn ClosedAtUtc thay thế và sửa tên KPI/acceptance test cho rõ.

Trong thời gian chưa nghiệm thu, trạng thái SLA là tạm tính. Quá deadline khi chưa xác nhận có thể phát cảnh báo, nhưng không được ghi Breach cuối cùng không thể sửa rồi lại kết luận đạt sau đó. Tách DeadlineAlert và FinalSlaOutcome; unique (IncidentId, AlertKind) chống trùng cảnh báo, kết quả cuối chốt ở Close. Không chỉ có boolean IsBreached không thể phân biệt các trường hợp.

Timestamp chuyển trạng thái phải do server tạo theo BR-VAL-04/05. Nếu technician nhập thời gian thực hiện thực tế thì lưu riêng như ghi chú/evidence; không dùng giá trị client tự nhập để lùi ResolvedAtUtc và đổi SLA.

### R2 - Close/Cancel có thể giải phóng blocking khi hiệu chuẩn vẫn thất bại

Liên quan BR-EQP-06/07, BR-MNT-19/21/23/24 và AC-14.

BR-MNT-24 yêu cầu tiếp tục không sẵn sàng, nhưng nếu Admin Close hoặc Cancel WorkOrder failed và không có Incident blocking, công thức trạng thái hiện tại có thể trả Operational.

Đề xuất MVP: không cho Close kết quả Calibration failed; trả Completed -> InProgress để khắc phục và nghiệm thu lại. Cancel trực tiếp chỉ cho Scheduled chưa thực hiện; InProgress/Completed không được xóa blocking bằng một thao tác Cancel thông thường. Nếu nghiệp vụ cần hủy việc đã bắt đầu, phải có disposition/quality hold được Equipment ghi nhận trước khi giải phóng WorkOrder; đây là nhánh mở rộng phải thiết kế riêng.

BlockingFlag không được là ô client tự bật/tắt tùy ý. Phải có policy theo WorkType/trạng thái/kết quả; failed calibration luôn blocking cho đến khắc phục được nghiệm thu. Đây là quy tắc mô phỏng đồ án cần duyệt, không được gọi là chuẩn an toàn y tế.

### R3 - Nhắc trước 24 giờ nhưng WorkOrder chỉ tạo lúc đến hạn

Liên quan BR-MNT-07/25/27/28.

Nếu 09:00 ngày mai mới tạo WorkOrder thì không thể gửi thông báo gắn WorkOrderId vào 09:00 hôm nay.

Đề xuất: reminder trước hạn gắn với (ScheduleId, ScheduledOccurrenceAtUtc, ReminderType), không bắt buộc đã có WorkOrder. WorkOrder tạo một lần cho kỳ khi đến hạn, liên kết reminder sau nếu cần. Event MaintenanceDueSoon phải chứa occurrence và snapshot đủ hiển thị. Phương án khác là sinh WorkOrder trước hạn; chỉ chọn một phương án rồi cập nhật rule/schema tương ứng.

Chốt thêm: overdue tính từ ScheduledOccurrenceAtUtc hay CompletionDueAtUtc; ngừng Schedule chỉ dừng kỳ tương lai hay hủy cả WorkOrder đã sinh; catch-up sau downtime xử lý theo batch, có giới hạn backlog và giữ lịch gốc.

### R4 - Retire cần một contract phối hợp thực sự

Liên quan BR-EQP-14/17/19/21. RowVersion và một lần GetEquipment không khóa được giao dịch ở hai database.

Đề xuất contract đăng ký tham chiếu bền tại Equipment trước khi service nguồn commit Incident, Schedule hoặc WorkOrder:

1. Nguồn sinh ResourceId/OperationId ổn định, gọi RegisterEquipmentUse qua gRPC với idempotency key.
2. Equipment khóa hàng thiết bị trong transaction cục bộ, chỉ nhận đăng ký khi Active; lưu registration Pending có reference type, SourceService, ResourceId và version.
3. Nguồn commit aggregate + registration ID + Outbox. Event sau commit chuyển registration thành Active; Close/Cancel hợp lệ hoặc kết thúc Schedule mới cho release sau commit.
4. Retire khóa cùng hàng Equipment, kiểm tra không còn registration Pending/Active và không còn lịch còn hiệu lực, rồi chuyển Retired trong cùng transaction. Đăng ký đến sau bị từ chối.
5. Nguồn crash trước commit tạo Pending cần đối soát. Không tự giải phóng chỉ vì hết TTL; chỉ release khi nguồn xác nhận thao tác đã kết thúc hoặc abort bền và không thể commit muộn. Trạng thái không rõ thì tiếp tục chặn Retire.

Registration là metadata phối hợp trong Equipment DB, không phải FK hoặc transaction xuyên database. Dữ liệu còn Pending có thể làm Retire bị chặn tạm thời; đó là chi phí bảo toàn invariant cần thể hiện trên UI. Các chi tiết fencing/abort, bootstrap dữ liệu cũ và late-release event phải được viết thành contract/test trước Approved.

### R5 - AuthorizationVersion cần định nghĩa mức độ tức thời

Liên quan BR-AUTH-09..14 và AC-02. JWT 15 phút hoặc event invalidate cache không tự bảo đảm thu hồi quyền ngay lập tức.

Đề xuất MVP: resource service gọi Identity.ValidateAuthorization qua gRPC trước quyết định phân quyền với tài nguyên bảo vệ; so UserId, AuthorizationVersion, IsActive, role và DepartmentId hiện hành. Áp dụng cả query/list/search/export của Staff, không chỉ command. Không dùng positive cache TTL nếu vẫn tuyên bố hiệu lực tức thời. Identity unavailable -> 503 với thao tác chưa xác minh, không biến thành 403 “không có quyền”.

Định nghĩa hiệu lực: mọi quyết định authorization mới sau khi Identity commit thay đổi phải thấy quyền mới; request đã được cho phép trước đó có thể hoàn thành. Không thể thu hồi dữ liệu đã trả về. Muốn ràng buộc mạnh hơn cho in-flight command cần thêm protocol, không thể chỉ thêm claim.

SignalR phải xử lý kết nối còn sống và nội dung thông báo sau đổi khoa. Không đẩy tên/chi tiết hồ sơ khoa cũ trên một kết nối chỉ xác thực lúc handshake; dùng tín hiệu dữ liệu mới và REST kiểm tra quyền hiện hành, hoặc revalidate trước push nội dung.

### R6 - Reconciliation và event gap chưa có điều kiện kết thúc

Liên quan BR-EQP-09..12 và BR-EVT-09..11. “Version cũ” khác “thiếu event trung gian”.

Đề xuất: event có full current state và version hợp lệ cho cùng source aggregate có thể áp dụng version mới nhất; ghi gap và kích hoạt reconciliation. Event thiếu AggregateVersion, schema lỗi hoặc producer không xác minh được -> retry/DLQ. Nếu yêu cầu phải nhận đủ mọi version, không thể block chỉ chờ version kế tiếp vì event assignment không phải consumer nào cũng subscribe.

Reconciliation cần gRPC snapshot API của Incident/Maintenance: phân trang ổn định, snapshot/watermark, aggregate version, các hồ sơ liên quan và trạng thái kết thúc/tombstone. Chỉ đối soát xóa blocking khi đã đọc xong snapshot hoàn chỉnh và xử lý version/event đến đồng thời. Không suy ra “đã đóng” từ một trang trả rỗng hoặc một service timeout.

Theo dõi last reconciled time, lag và lỗi nguồn. Khi chưa xác minh dữ liệu ảnh hưởng invariant, không hiển thị Operational/cho Retire như dữ liệu đã đầy đủ. AC-06 hiện chỉ test version cũ, cần thêm test gap, snapshot nhiều trang và service nguồn không khả dụng.

### R7 - Đồng bộ BR với C1, contract và data model cũ

BR-ARCH-05 yêu cầu đồng bộ liên service bằng gRPC, nhưng C1/contract trước có internal REST cho user/department lookup. Phương án cập nhật: Web/Gateway -> API vẫn REST; giữa các service dùng gRPC cho lookup, authorization, registration và reconciliation.

Những điểm schema cần thay đổi theo bản BR đang review:

- Incident: Open -> InProgress -> Resolved -> Closed, phân công là metadata, không có Assigned status; thêm ResolutionAttempt/AcceptedResolutionAttemptId và lịch sử nghiệm thu.
- WorkOrder: Scheduled -> InProgress -> Completed -> Closed, có return-to-work; thay WorkOrderResult một-một bằng các lần thực hiện/WorkOrderAttempt để không ghi đè kết quả cũ.
- SLA policy versioned, hiệu lực thời gian không chồng lấn; unique (Priority, PolicyVersion). Chốt cách áp dụng policy có EffectiveFrom trong tương lai.
- ApplicationUser thêm AuthorizationVersion; audit thay đổi quyền và refresh revocation cùng transaction.
- Equipment hiển thị Operational/UnderMaintenance/Broken/Retired; có thể tách lifecycle nội bộ, không dùng OutOfService như một tên thứ năm không được định nghĩa.
- Thêm registration/reconciliation metadata; reminder theo occurrence nếu chọn R3.
- Bổ sung DepartmentNameSnapshot tại Incident/WorkOrder theo yêu cầu lịch sử; không dùng tên hiện tại để ghi đè snapshot.
- 422 cho semantic validation phải được bổ sung vào HTTP contract; 409 dành cho concurrency/state conflict, 503 dependency unavailable.

Các thay đổi schema nghiệp vụ trên là backlog từ review; không được coi data model cũ đã đáp ứng BR nếu chỉ sửa sơ đồ.

### R8 - Những chi tiết nghiệp vụ còn thiếu

- Duplicate Incident: Staff có bị chặn hay chỉ cảnh báo? Ai xác nhận lỗi độc lập? Không tạo unique index cấm nhiều Incident mở cho cùng Equipment vì AC-05 cho phép nhiều blocking Incident.
- Khi thiết bị chuyển khoa, cảnh báo trùng không được lộ Incident snapshot khoa cũ cho Staff khoa mới. Chỉ thông báo cần Admin kiểm tra nếu không có quyền xem chi tiết.
- Technician không được tạo Incident nhưng có thể phát hiện lỗi khi bảo trì: cần nút yêu cầu Admin tạo Incident, không để workflow không có người nhận xử lý.
- Khi reassign InProgress: giữ FirstRespondedAtUtc, SLA và lịch sử; xác định technician mới cần acknowledgement riêng hay tiếp tục luôn.
- Admin được thao tác thay technician nhưng phải ghi actor thực tế; phải quyết định Admin có thể tự nghiệm thu việc mình thực hiện không. Đây là lựa chọn nghiệp vụ, chưa có yêu cầu bắt buộc tách người.
- Reports “Technician theo quyền”, “Staff trong khoa” cần định nghĩa chính xác KPI, scope, export và timezone; không chỉ mô tả chung.

## 4. Bộ công nghệ và môi trường chung đề xuất

| Thành phần | Baseline chung | Lý do |
|---|---|---|
| Backend | .NET 10 LTS, ASP.NET Core 10, EF Core 10 + SQL Server provider cùng major | Đề cho phép .NET 8+; chọn theo vòng đời hỗ trợ thay vì SDK cá nhân |
| SQL Server | SQL Server 2022 Developer, 5 database độc lập, compatibility level 160 | Đủ rowversion/EF/migrations; không cần tính năng riêng SQL Server 2025 |
| Frontend build | React + TypeScript, Node 24 LTS, package lock | Cùng toolchain trong Docker/CI |
| Gateway / sync | YARP; gRPC/protobuf giữa các service | REST biên ngoài, gRPC nội bộ theo BR |
| Async / workers | Redis Streams, Outbox/Inbox, BackgroundService | Bám đề và xử lý các scanner thực tế |
| Chạy hệ thống | Docker Compose làm cấu hình chuẩn | Không phụ thuộc LocalDB hoặc đường dẫn máy cá nhân |
| Dev/test | Dev Containers tùy chọn; CI build/test cùng image | Checkout sạch có thể khôi phục và kiểm tra được |

Đây là baseline đề xuất cho cả nhóm, chưa phải package/image lock đã kiểm chứng. Trước code cần khóa SDK feature band bằng global.json, NuGet versions/lock, node/image tag và digest; chạy restore/build/test trên CI. Không dùng tag latest cho bản nộp.

Để người dùng chạy demo, SDK/Node có thể nằm trong multi-stage Docker build nên không bắt buộc cài trên host. Để sửa/debug native thì cài đúng SDK chung hoặc dùng Dev Container. Cấu hình chỉ qua env/secrets, service DNS, scripts tương đương PowerShell và shell; không hardcode D:\PRN232, user Windows, instance LocalDB hay Chrome path.

SQL Server container chính thức hỗ trợ x86-64. Thành viên ARM/Apple Silicon dùng SQL Server x86-64 chung từ máy nhóm/cloud qua cấu hình connection string riêng, hoặc truy cập bản demo được host; không hứa mọi máy chạy SQL Server container local. Database/user ownership vẫn tách theo service và từng môi trường. LocalDB chỉ là lựa chọn Windows cá nhân, không phải điều kiện để nhóm chạy dự án.

Ngân sách đề xuất để thử Compose: host 16 GB RAM, dành 6-8 GB cho workload và 15-25 GB trống cho image/build/data; đây không phải yêu cầu tối thiểu đã đo. Đo CI/dev thực tế rồi công bố yêu cầu. Có profile infrastructure-only và full-demo; máy yếu dùng môi trường chung với môi trường/dữ liệu riêng từng người.

## 5. Acceptance tests bổ sung trước Approved

| ID | Tình huống | Kết quả cần chốt |
|---|---|---|
| AC-15 | Resolve trước hạn, nghiệm thu sau hạn | Outcome theo mốc đã chọn ở R1 |
| AC-16 | Resolve bị reject, lần sau qua hạn | Lịch sử lần đầu giữ nguyên; final SLA dùng lần được nhận |
| AC-17 | Cancel hoặc Close calibration failed | Không đưa thiết bị về Operational khi lỗi chưa khắc phục |
| AC-18 | Incident/Schedule/WorkOrder đăng ký đồng thời Retire | Chỉ một phía được đi qua điểm phối hợp; không có hồ sơ mới hợp lệ trên Retired |
| AC-19 | Nguồn crash sau registration trước commit | Pending không tự hết hạn làm lọt Retire; reconciliation không cho late commit bị bỏ sót |
| AC-20 | Đổi khoa trong khi JWT/SignalR còn sống | Quyết định quyền mới theo quy tắc R5; không lộ nội dung khoa cũ |
| AC-21 | Nhắc trước 24 giờ khi chưa có WorkOrder | Một reminder/occurrence; không phụ thuộc WO chưa tồn tại |
| AC-22 | Event gap, reconciliation nhiều trang, có event mới đồng thời | Không làm lùi version hoặc tự xóa blocking do snapshot thiếu |
| AC-23 | Checkout sạch trên môi trường nhóm và CI | Build/run bằng cùng version, không cần LocalDB cá nhân |
| AC-24 | Staff truy cập search/sort/page/export và duplicate warning | Không lộ dữ liệu ngoài department scope |

## 6. Thứ tự làm tiếp

1. Chốt R1..R6 thành câu quy tắc có thể kiểm thử và cập nhật BR v1.1 đề xuất, giữ bản v1.0 nguyên gốc.
2. Đồng bộ state machines, ownership, C1 và hợp đồng protobuf/events theo R7.
3. Chỉnh model Code First, constraints/indexes và audit/attempt tables theo bản BR đã thống nhất.
4. Chuẩn hóa môi trường nhóm và CI; sau đó mới tạo solution, migrations và application code.

## 7. Nguồn kỹ thuật chính thức

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy): .NET 8 hết hỗ trợ 10/11/2026; .NET 10 là LTS.
- [SQL Server EF Core provider](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/): provider và compatibility level cần khớp cấu hình database.
- [SQL Server Linux/container platforms](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/setup?view=sql-server-ver17): container SQL Server x86-64 và giới hạn hỗ trợ emulation.
- [Node releases](https://nodejs.org/en/about/previous-releases): Node 24 LTS làm baseline build đề xuất.
- [Distributed data management](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/distributed-data-management): ranh giới dữ liệu và eventual consistency.
