# MEDEQUIPSLA
## BUSINESS RULES SPECIFICATION — VERSION 1.0

**Tên hệ thống:** MedEquipSLA – Medical Equipment Maintenance & Incident SLA Management System  
**Ngày cập nhật:** 23/10/2026  
**Trạng thái:** Final Proposed Specification – Sẵn sàng phê duyệt trước triển khai  
**Kiến trúc:** Microservices, Database per Service, CQRS, Clean Architecture, Event-Driven Architecture  
**Phạm vi:** Quản lý thiết bị y tế, bảo trì, hiệu chuẩn, báo hỏng, xử lý sự cố, SLA và thông báo.

---

# 1. MỤC TIÊU VÀ NGUYÊN TẮC HỆ THỐNG

## 1.1. Mục tiêu

MedEquipSLA hỗ trợ các cơ sở y tế:

- Quản lý danh mục thiết bị y tế theo khoa/phòng.
- Theo dõi tình trạng hoạt động của thiết bị.
- Ghi nhận và xử lý sự cố thiết bị.
- Phân công kỹ thuật viên và theo dõi tiến độ sửa chữa.
- Quản lý lịch bảo trì và hiệu chuẩn định kỳ.
- Tự động phát hiện công việc đến hạn hoặc quá hạn.
- Theo dõi thời gian phản hồi và xử lý theo SLA.
- Gửi thông báo trong ứng dụng và email.
- Cung cấp dashboard, báo cáo và lịch sử hoạt động.

Hệ thống không quản lý bệnh nhân, bệnh án, đơn thuốc hoặc dữ liệu điều trị.

## 1.2. Nguyên tắc kiến trúc bắt buộc

BR-ARCH-01: Hệ thống gồm năm microservices:

| Service | Database | Trách nhiệm chính |
|---|---|---|
| Identity | MedEquip_Identity | Tài khoản, vai trò, xác thực, phân quyền |
| Equipment | MedEquip_Equipment | Khoa/phòng, thiết bị, loại thiết bị, trạng thái |
| Maintenance | MedEquip_Maintenance | Lịch bảo trì, hiệu chuẩn, WorkOrder |
| Incident | MedEquip_Incident | Báo hỏng, xử lý sự cố, SLA |
| Notification | MedEquip_Notification | Thông báo, email, SignalR |

BR-ARCH-02: Mỗi service sở hữu một database và DbContext riêng.

BR-ARCH-03: Không sử dụng Foreign Key, SQL JOIN, SQL View hoặc transaction xuyên database giữa các service.

BR-ARCH-04: Mỗi service tự quản lý Entity, Fluent API, EF Core migrations và dữ liệu thuộc phạm vi sở hữu.

BR-ARCH-05: Giao tiếp đồng bộ giữa các service sử dụng gRPC khi cần kiểm tra dữ liệu tức thời.

BR-ARCH-06: Giao tiếp bất đồng bộ sử dụng Redis Streams, Transactional Outbox và Inbox.

BR-ARCH-07: Chỉ service sở hữu dữ liệu được phép thay đổi dữ liệu nguồn của mình.

BR-ARCH-08: Gateway không trực tiếp truy cập database nghiệp vụ.

BR-ARCH-09: CQRS phân tách Command và Query trong từng service; không yêu cầu database đọc/ghi riêng hoặc Event Sourcing.

---

# 2. PHÂN QUYỀN VÀ PHẠM VI DỮ LIỆU

## 2.1. Vai trò

Hệ thống có ba vai trò nghiệp vụ chính:

- Admin
- Technician
- Department Staff

## 2.2. Ma trận phân quyền

| Chức năng | Admin | Technician | Staff |
|---|---|---|---|
| Quản lý tài khoản | Có | Không | Không |
| Quản lý khoa/phòng | Có | Không | Không |
| Quản lý thiết bị | Có | Xem | Xem trong khoa |
| Tạo Incident | Có | Không | Trong khoa |
| Xem Incident | Toàn bộ | Toàn bộ | Trong khoa |
| Phân công Technician | Có | Không | Không |
| Tiếp nhận Incident | Có | Được phân công | Không |
| Cập nhật xử lý | Có | Được phân công | Không |
| Resolve Incident | Có | Được phân công | Không |
| Close Incident | Có | Không | Không |
| Quản lý SLA Policy | Có | Không | Không |
| Quản lý Maintenance Schedule | Có | Không | Không |
| Thực hiện WorkOrder | Có | Được phân công | Không |
| Nghiệm thu WorkOrder | Có | Không | Không |
| Xem báo cáo tổng hợp | Có | Theo quyền | Trong khoa |
| Xem thông báo cá nhân | Có | Có | Có |

Admin được phép thực hiện các thao tác nghiệp vụ của Technician khi cần, nhưng mọi thao tác phải được ghi audit.

## 2.3. Department Isolation

**BR-AUTH-01:** Staff chỉ được truy cập thiết bị thuộc khoa/phòng hiện tại của mình.

**BR-AUTH-02:** Staff chỉ được tạo Incident cho thiết bị thuộc khoa được gán.

**BR-AUTH-03:** Staff chỉ được xem Incident và lịch sử liên quan đến khoa của mình.

**BR-AUTH-04:** Query phải áp dụng Department Filter ở backend, không phụ thuộc dữ liệu do frontend truyền lên.

**BR-AUTH-05:** Command Handler phải kiểm tra quyền sở hữu khoa/phòng trước khi ghi dữ liệu.

**BR-AUTH-06:** Technician được xem dữ liệu toàn hệ thống nhưng chỉ được cập nhật công việc được phân công.

**BR-AUTH-07:** Admin có quyền quản lý toàn bộ dữ liệu nghiệp vụ trong phạm vi hệ thống.

**BR-AUTH-08:** Không trả dữ liệu thuộc khoa khác thông qua ID guessing, search, pagination, export hoặc API chi tiết.

## 2.4. JWT và thay đổi khoa/phòng

Access Token chứa tối thiểu:

- UserId
- Role
- DepartmentId
- AuthorizationVersion
- Expiration

**BR-AUTH-09:** Access Token có thời gian sống ngắn, đề xuất 15 phút.

**BR-AUTH-10:** Refresh Token được lưu dưới dạng hash và áp dụng token rotation.

**BR-AUTH-11:** Khi người dùng đổi khoa, đổi vai trò hoặc bị khóa, Identity tăng AuthorizationVersion và thu hồi các refresh token còn hiệu lực.

**BR-AUTH-12:** Các service phải có cơ chế kiểm tra AuthorizationVersion hiện hành đối với thao tác cần phân quyền. Không được chỉ dựa vào claim cũ nếu cần hiệu lực thay đổi quyền ngay lập tức.

**BR-AUTH-13:** Khi thông tin phân quyền chưa được xác minh và hệ thống không có dữ liệu quyền còn hiệu lực đáng tin cậy, thao tác nhạy cảm phải bị từ chối tạm thời.

**BR-AUTH-14:** Dữ liệu lịch sử giữ DepartmentId snapshot tại thời điểm phát sinh nghiệp vụ.

---

# 3. QUẢN LÝ KHOA/PHÒNG VÀ THIẾT BỊ

## 3.1. Department Ownership

**BR-DEPT-01:** Equipment Service là nguồn dữ liệu chính của Department.

**BR-DEPT-02:** Identity chỉ lưu DepartmentId tham chiếu và DepartmentName snapshot phục vụ xác thực và hiển thị.

**BR-DEPT-03:** DepartmentId phải được kiểm tra hợp lệ khi Admin phân khoa cho nhân viên.

**BR-DEPT-04:** Không xóa cứng Department đã có thiết bị hoặc lịch sử nghiệp vụ.

**BR-DEPT-05:** Department không còn sử dụng được chuyển sang Inactive.

**BR-DEPT-06:** Không phân công nhân viên hoặc thiết bị mới vào Department Inactive.

**BR-DEPT-07:** Khi thiết bị chuyển khoa, Equipment cập nhật DepartmentId hiện hành và phát sự kiện thay đổi. Các hồ sơ Incident và WorkOrder đã tạo vẫn giữ DepartmentId snapshot.

**BR-DEPT-08:** Staff chỉ được xem hồ sơ lịch sử có DepartmentId snapshot trùng khoa hiện tại, theo phạm vi quyền được cấp. Admin có thể xem lịch sử toàn bộ.

## 3.2. Equipment Lifecycle

Thiết bị có bốn trạng thái vận hành:

| Trạng thái | Ý nghĩa |
|---|---|
| Operational | Sẵn sàng hoạt động |
| UnderMaintenance | Đang bảo trì hoặc hiệu chuẩn |
| Broken | Đang có sự cố ảnh hưởng vận hành |
| Retired | Ngừng sử dụng hoặc thanh lý |

**BR-EQP-01:** Equipment Service là service duy nhất có quyền cập nhật trạng thái vận hành chính thức.

**BR-EQP-02:** Incident và Maintenance không trực tiếp thay đổi Equipment database.

**BR-EQP-03:** Trạng thái vận hành được tính từ trạng thái vòng đời và các bản ghi blocking đang có hiệu lực.

**BR-EQP-04:** Thứ tự ưu tiên:

Retired > Broken > UnderMaintenance > Operational.

**BR-EQP-05:** Nếu tồn tại ít nhất một Incident blocking chưa Closed, thiết bị ở trạng thái Broken.

**BR-EQP-06:** Nếu không còn Incident blocking nhưng có WorkOrder blocking đang InProgress hoặc Completed chờ nghiệm thu, thiết bị ở trạng thái UnderMaintenance.

**BR-EQP-07:** Nếu không có blocking Incident, không có blocking WorkOrder và thiết bị chưa Retired, trạng thái là Operational.

**BR-EQP-08:** Thiết bị Retired không được tự động chuyển về trạng thái khác bởi event đến muộn.

**BR-EQP-09:** Equipment lưu projection các Incident và WorkOrder có ảnh hưởng trạng thái, gồm SourceId, trạng thái, BlockingFlag và Version.

**BR-EQP-10:** Event trùng hoặc có Version cũ hơn trạng thái đã áp dụng không được ghi đè projection mới.

**BR-EQP-11:** Event bị thiếu phiên bản hoặc không thể xác định thứ tự phải được giữ để retry/reconciliation; không tự suy đoán trạng thái.

**BR-EQP-12:** Sau khi khôi phục kết nối broker, Equipment phải có cơ chế đối soát projection với các service nguồn.

## 3.3. Retired Equipment

**BR-EQP-13:** Chỉ Admin được Retire thiết bị.

**BR-EQP-14:** Không được Retire khi còn Incident chưa Closed hoặc WorkOrder chưa Closed/Cancelled.

**BR-EQP-15:** Thiết bị Retired không được tạo Incident mới.

**BR-EQP-16:** Thiết bị Retired không được tạo Maintenance Schedule mới hoặc WorkOrder mới.

**BR-EQP-17:** Các lịch bảo trì chưa thực hiện phải được kết thúc/hủy hợp lệ trước khi Retire.

**BR-EQP-18:** Không xóa cứng thiết bị đã có lịch sử nghiệp vụ.

**BR-EQP-19:** Incident/Maintenance phải kiểm tra trạng thái thiết bị qua Equipment gRPC trước khi tạo mới.

**BR-EQP-20:** Nếu gRPC timeout hoặc không khả dụng, Command trả lỗi tạm thời, không mặc định thiết bị hợp lệ.

**BR-EQP-21:** Để tránh race condition giữa tạo Incident/WorkOrder và Retire, hệ thống áp dụng cơ chế phối hợp vòng đời thiết bị có version/authorization. gRPC lookup đơn thuần không được xem là bảo đảm tuyệt đối. Trước khi triển khai phải chốt contract phối hợp hoặc cơ chế xác nhận và bù trừ tương ứng.

---

# 4. INCIDENT MANAGEMENT

## 4.1. Tạo Incident

**BR-INC-01:** Staff chỉ được báo sự cố cho thiết bị thuộc khoa của mình.

**BR-INC-02:** Admin có thể tạo Incident cho thiết bị thuộc bất kỳ khoa nào.

**BR-INC-03:** Thiết bị Retired bị từ chối tạo Incident.

**BR-INC-04:** Khi tạo Incident, bắt buộc có:

- EquipmentId
- Title
- Description
- Priority
- ReportedByUserId
- DepartmentId snapshot
- CreatedAtUtc

**BR-INC-05:** Incident lưu snapshot EquipmentCode, EquipmentName và DepartmentName tại thời điểm tạo.

**BR-INC-06:** Hệ thống kiểm tra Incident chưa Closed của cùng thiết bị để cảnh báo trùng lặp.

**BR-INC-07:** Nếu nghi ngờ trùng, frontend hiển thị Incident hiện có. Admin có thể tạo thêm Incident nếu xác nhận đây là lỗi độc lập.

**BR-INC-08:** Việc kiểm tra trùng không được tự động gộp hai lỗi khác nhau thành một Incident.

## 4.2. Incident Lifecycle

Trạng thái:

Open → InProgress → Resolved → Closed.

**BR-INC-09:** Incident mới có trạng thái Open.

**BR-INC-10:** Admin có quyền phân công hoặc phân công lại Technician.

**BR-INC-11:** Technician được phân công có thể tiếp nhận Incident.

**BR-INC-12:** Khi Technician tiếp nhận lần đầu, Incident chuyển sang InProgress và ghi FirstRespondedAtUtc.

**BR-INC-13:** Technician chỉ được Resolve Incident được phân công.

**BR-INC-14:** Khi Resolve, bắt buộc nhập biên bản xử lý, kết quả và thời gian hoàn tất.

**BR-INC-15:** Incident chuyển sang Resolved nhưng vẫn chờ Admin nghiệm thu.

**BR-INC-16:** Chỉ Admin được chuyển Resolved sang Closed.

**BR-INC-17:** Admin chỉ được Closed khi đã xác nhận nghiệm thu.

**BR-INC-18:** Không được chuyển trực tiếp Open sang Closed.

**BR-INC-19:** Không được sửa trạng thái của Incident đã Closed.

**BR-INC-20:** Incident đã Closed là hồ sơ kết thúc và không được Reopen.

**BR-INC-21:** Nếu nghiệm thu không đạt, Admin có thể trả Incident từ Resolved về InProgress, kèm lý do. Đây là tiếp tục xử lý hồ sơ chưa đóng, không phải Reopen Incident đã Closed.

**BR-INC-22:** Việc trả lại xử lý không xóa FirstRespondedAtUtc hoặc lịch sử Resolve trước đó.

**BR-INC-23:** Mỗi lần chuyển trạng thái phải lưu lịch sử gồm ActorId, FromStatus, ToStatus, Timestamp và Reason nếu cần.

## 4.3. Sự cố tái phát

**BR-INC-24:** Sự cố đã Closed không được mở lại.

**BR-INC-25:** Nếu thiết bị hỏng lại, tạo Incident mới.

**BR-INC-26:** Incident mới có thể lưu RelatedIncidentId để liên kết với sự cố cũ.

**BR-INC-27:** RelatedIncidentId phải thuộc cùng EquipmentId.

**BR-INC-28:** RelatedIncidentId được kiểm tra trong Incident Service, không tạo phụ thuộc database khác.

---

# 5. SLA MANAGEMENT

## 5.1. SLA Policy

Mỗi Priority có chính sách riêng:

- Critical
- High
- Medium
- Low

Mỗi chính sách gồm:

- ResponseDurationMinutes
- ResolutionDurationMinutes
- EffectiveFromUtc
- IsActive
- PolicyVersion

**BR-SLA-01:** Chỉ Admin được tạo, sửa hoặc ngừng áp dụng SLA Policy.

**BR-SLA-02:** Mỗi Priority chỉ có một chính sách có hiệu lực tại một thời điểm.

**BR-SLA-03:** ResponseDuration và ResolutionDuration phải lớn hơn 0.

**BR-SLA-04:** ResolutionDuration không được nhỏ hơn ResponseDuration.

**BR-SLA-05:** Chính sách được quản lý theo phiên bản, không ghi đè dữ liệu lịch sử đã áp dụng.

## 5.2. SLA Snapshot

**BR-SLA-06:** Khi tạo Incident, hệ thống lấy SLA Policy đang có hiệu lực theo Priority.

**BR-SLA-07:** Incident lưu snapshot:

- SlaPolicyId
- SlaPolicyVersion
- ResponseDurationMinutes
- ResolutionDurationMinutes
- ResponseDeadlineAtUtc
- ResolutionDeadlineAtUtc

**BR-SLA-08:** Sửa SLA Policy không thay đổi deadline của Incident đã tạo.

**BR-SLA-09:** Đổi Priority sau khi Incident đã tạo không tự động tính lại SLA. MVP chỉ cho phép Admin chỉnh Priority hiển thị kèm lý do, giữ nguyên SLA snapshot.

## 5.3. Cách tính SLA

**BR-SLA-10:** SLA tính liên tục 24/7, không loại trừ ngày nghỉ hoặc ngoài giờ.

**BR-SLA-11:** Mọi thời điểm được lưu theo UTC.

**BR-SLA-12:** Frontend hiển thị theo Asia/Ho_Chi_Minh.

**BR-SLA-13:** ResponseDeadlineAtUtc = CreatedAtUtc + ResponseDurationMinutes.

**BR-SLA-14:** ResolutionDeadlineAtUtc = CreatedAtUtc + ResolutionDurationMinutes.

**BR-SLA-15:** Response SLA đạt nếu FirstRespondedAtUtc không muộn hơn ResponseDeadlineAtUtc.

**BR-SLA-16:** Resolution SLA đạt nếu thời điểm hoàn tất xử lý được Admin nghiệm thu không muộn hơn ResolutionDeadlineAtUtc.

**BR-SLA-17:** Nếu Admin trả lại xử lý do nghiệm thu không đạt, lần Resolve trước không được tính là hoàn tất Resolution SLA.

**BR-SLA-18:** SLA không tạm dừng khi Incident chờ vật tư, chờ Technician hoặc chờ xử lý.

**BR-SLA-19:** Incident Closed không bị thay đổi kết quả SLA sau khi kết thúc.

**BR-SLA-20:** Background scanner phát hiện deadline đã quá hạn, lưu trạng thái vi phạm và phát event qua Outbox.

**BR-SLA-21:** Mỗi loại vi phạm SLA chỉ phát một sự kiện nghiệp vụ duy nhất cho mỗi Incident, sử dụng unique key.

**BR-SLA-22:** Các cảnh báo quá hạn và kết quả SLA phải phân biệt rõ trạng thái tạm tính với kết quả cuối cùng.

---

# 6. MAINTENANCE MANAGEMENT

## 6.1. Maintenance Schedule

**BR-MNT-01:** Chỉ Admin được tạo, chỉnh sửa hoặc ngừng Maintenance Schedule.

**BR-MNT-02:** Schedule phải tham chiếu EquipmentId hợp lệ.

**BR-MNT-03:** Không được tạo Schedule mới cho thiết bị Retired.

**BR-MNT-04:** Schedule gồm loại công việc Maintenance hoặc Calibration, chu kỳ, ngày đến hạn tiếp theo và trạng thái.

**BR-MNT-05:** Mỗi Schedule phải có quy tắc xác định kỳ tiếp theo nhất quán, dựa trên lịch gốc thay vì thời điểm job thực thi.

**BR-MNT-06:** Schedule bị vô hiệu hóa không tạo thêm WorkOrder mới.

## 6.2. Tự động tạo WorkOrder

**BR-MNT-07:** BackgroundService kiểm tra lịch đến hạn mỗi phút.

**BR-MNT-08:** Mỗi kỳ đến hạn chỉ tạo một WorkOrder.

**BR-MNT-09:** Idempotency Key sử dụng ScheduleId + ScheduledOccurrenceAtUtc.

**BR-MNT-10:** Database có unique index trên khóa kỳ công việc.

**BR-MNT-11:** Khi nhiều worker cùng xử lý một kỳ, chỉ một transaction được tạo WorkOrder thành công.

**BR-MNT-12:** Nếu worker restart, hệ thống tiếp tục xử lý các kỳ chưa được tạo mà không tạo trùng.

**BR-MNT-13:** Job phải hỗ trợ xử lý các kỳ bị bỏ lỡ sau downtime.

## 6.3. WorkOrder Lifecycle

Trạng thái:

Scheduled → InProgress → Completed → Closed.

Ngoài ra có trạng thái Cancelled.

**BR-MNT-14:** WorkOrder mới được tạo ở trạng thái Scheduled.

**BR-MNT-15:** Technician được phân công có thể bắt đầu công việc.

**BR-MNT-16:** Khi bắt đầu, WorkOrder chuyển sang InProgress.

**BR-MNT-17:** Technician phải nhập kết quả thực hiện trước khi Completed.

**BR-MNT-18:** WorkOrder Calibration phải lưu kết quả hiệu chuẩn và kết luận đạt/không đạt.

**BR-MNT-19:** Chỉ Admin được nghiệm thu và Closed WorkOrder.

**BR-MNT-20:** Nếu nghiệm thu không đạt, Admin có thể trả Completed về InProgress và ghi lý do.

**BR-MNT-21:** Chỉ Admin được Cancelled WorkOrder chưa Closed, kèm lý do.

**BR-MNT-22:** WorkOrder Closed hoặc Cancelled không được tiếp tục cập nhật tiến độ.

**BR-MNT-23:** WorkOrder đang thực hiện hoặc đã Completed chờ nghiệm thu có thể được đánh dấu BlockingFlag để ảnh hưởng trạng thái thiết bị.

**BR-MNT-24:** Kết quả hiệu chuẩn không đạt phải giữ thiết bị ở trạng thái không sẵn sàng, thông qua blocking condition, cho đến khi có hành động khắc phục được nghiệm thu.

## 6.4. Maintenance Reminder

**BR-MNT-25:** Thời điểm nhắc mặc định là 24 giờ trước ScheduledOccurrenceAtUtc.

**BR-MNT-26:** ReminderLeadTime có thể cấu hình.

**BR-MNT-27:** Mỗi kỳ và loại nhắc hạn có một unique key.

**BR-MNT-28:** Maintenance ghi yêu cầu nhắc hạn và Outbox event trong cùng transaction.

**BR-MNT-29:** Không đánh dấu email đã gửi thành công tại thời điểm tạo reminder.

**BR-MNT-30:** Notification Service chịu trách nhiệm theo dõi quá trình gửi.

## 6.5. Xung đột với Incident

**BR-MNT-31:** Thiết bị Broken vẫn có thể thực hiện công việc sửa chữa, bảo trì hoặc hiệu chuẩn phù hợp.

**BR-MNT-32:** Việc hoàn tất WorkOrder không tự động chuyển thiết bị thành Operational nếu vẫn còn Incident blocking.

**BR-MNT-33:** Nếu một công việc bảo trì phát hiện lỗi mới, người có quyền phải tạo Incident tương ứng; không tự tạo hồ sơ không có xác nhận nghiệp vụ.

---

# 7. NOTIFICATION MANAGEMENT

## 7.1. Các loại thông báo

Hệ thống hỗ trợ:

- IncidentCreated
- IncidentAssigned
- IncidentResolved
- IncidentClosed
- SlaResponseBreached
- SlaResolutionBreached
- MaintenanceDueSoon
- MaintenanceOverdue
- WorkOrderAssigned
- WorkOrderCompleted

## 7.2. Quy tắc thông báo

**BR-NOTI-01:** Notification Service sở hữu dữ liệu thông báo.

**BR-NOTI-02:** Mỗi thông báo có RecipientUserId xác định.

**BR-NOTI-03:** Người dùng chỉ được xem và đánh dấu đã đọc thông báo của mình.

**BR-NOTI-04:** Event được xử lý idempotent bằng EventId và Inbox.

**BR-NOTI-05:** Thông báo trong ứng dụng được lưu bền vững trước khi gửi SignalR.

**BR-NOTI-06:** SignalR chỉ thông báo có dữ liệu mới; frontend sử dụng REST để lấy danh sách và đồng bộ trạng thái.

**BR-NOTI-07:** Sau khi reconnect, frontend phải tải lại thông báo chưa đọc hoặc thay đổi mới nhất.

**BR-NOTI-08:** Email sử dụng hàng đợi gửi với các trạng thái Pending, Processing, Sent và Failed.

**BR-NOTI-09:** Email lỗi tạm thời được retry có giới hạn.

**BR-NOTI-10:** Email lỗi vĩnh viễn được ghi nhận để Admin kiểm tra.

**BR-NOTI-11:** Không cam kết exactly-once delivery đối với SMTP.

**BR-NOTI-12:** Thông báo quan trọng về SLA và phân công không được mất chỉ vì SignalR hoặc email tạm thời không khả dụng.

---

# 8. EVENT-DRIVEN CONSISTENCY

## 8.1. Transactional Outbox

**BR-EVT-01:** Khi nghiệp vụ thay đổi, service ghi dữ liệu và OutboxMessage trong cùng transaction SQL cục bộ.

**BR-EVT-02:** Outbox dispatcher chỉ phát event đã commit.

**BR-EVT-03:** Nếu Redis không khả dụng, nghiệp vụ đã commit vẫn được giữ nguyên.

**BR-EVT-04:** Dispatcher retry các event chưa phát thành công.

**BR-EVT-05:** Không đánh dấu event đã phát trước khi broker xác nhận nhận message.

## 8.2. Inbox và Idempotency

**BR-EVT-06:** Mỗi consumer lưu EventId đã xử lý.

**BR-EVT-07:** Inbox record và thay đổi dữ liệu consumer phải được commit trong cùng transaction.

**BR-EVT-08:** Duplicate event không được tạo dữ liệu hoặc thay đổi trạng thái lần thứ hai.

**BR-EVT-09:** Consumer phải xử lý event đến sai thứ tự theo AggregateVersion.

**BR-EVT-10:** Event có version cũ không được ghi đè dữ liệu mới.

**BR-EVT-11:** Event thiếu phiên bản hoặc lỗi liên tục phải được retry có giới hạn và đưa vào cơ chế xử lý lỗi/dead-letter.

**BR-EVT-12:** Mỗi service nhận event sử dụng consumer group phù hợp để không làm mất event của service khác.

## 8.3. Event Contract

Integration Event tối thiểu gồm:

- EventId
- EventType
- SchemaVersion
- AggregateId
- AggregateVersion
- OccurredAtUtc
- CorrelationId
- Payload

**BR-EVT-13:** Event Contract phải có version để hỗ trợ thay đổi schema.

**BR-EVT-14:** Consumer không được giả định event luôn đến đúng thứ tự hoặc chỉ đến một lần.

**BR-EVT-15:** Mọi event quan trọng phải có log và correlation ID phục vụ truy vết.

---

# 9. CONCURRENCY, VALIDATION VÀ ERROR HANDLING

## 9.1. Optimistic Concurrency

**BR-CON-01:** Aggregate có cập nhật đồng thời sử dụng SQL Server rowversion.

**BR-CON-02:** Command cập nhật phải kiểm tra version hiện hành.

**BR-CON-03:** Khi có xung đột, API trả HTTP 409 Conflict.

**BR-CON-04:** Không tự ghi đè thay đổi của người khác khi xảy ra concurrency conflict.

**BR-CON-05:** Frontend thông báo dữ liệu đã thay đổi và cho phép người dùng tải lại.

## 9.2. Validation

**BR-VAL-01:** Mọi Command phải validate dữ liệu đầu vào.

**BR-VAL-02:** Business Rule phải được kiểm tra ở Application/Domain, không chỉ frontend.

**BR-VAL-03:** Query sử dụng DTO projection, pagination và AsNoTracking khi phù hợp.

**BR-VAL-04:** Không cho phép client tự gửi các trường hệ thống như CreatedAt, ActorId hoặc SLA Deadline để thay thế giá trị do server tính.

**BR-VAL-05:** Tất cả timestamp nghiệp vụ được tạo bởi backend.

## 9.3. HTTP Error Contract

| HTTP Status | Trường hợp |
|---|---|
| 400 | Request không hợp lệ |
| 401 | Chưa xác thực hoặc token không hợp lệ |
| 403 | Không đủ quyền |
| 404 | Không tìm thấy tài nguyên trong phạm vi được phép |
| 409 | Xung đột trạng thái hoặc concurrency |
| 422 | Dữ liệu hợp lệ về cấu trúc nhưng vi phạm quy tắc nghiệp vụ |
| 503 | Service phụ thuộc tạm thời không khả dụng |

**BR-ERR-01:** API sử dụng ProblemDetails với mã lỗi nghiệp vụ ổn định.

**BR-ERR-02:** Không trả stack trace hoặc thông tin nội bộ nhạy cảm cho frontend.

**BR-ERR-03:** gRPC timeout phải có deadline và cancellation.

**BR-ERR-04:** Retry chỉ áp dụng tự động cho thao tác an toàn hoặc thao tác có idempotency bảo đảm.

---

# 10. AUDIT, LỊCH SỬ VÀ BÁO CÁO

**BR-AUD-01:** Ghi audit đối với các thao tác quan trọng:

- Thay đổi vai trò và khoa của người dùng.
- Tạo, cập nhật, Retire thiết bị.
- Thay đổi SLA Policy.
- Phân công và phân công lại Technician.
- Resolve và Close Incident.
- Nghiệm thu hoặc hủy WorkOrder.

**BR-AUD-02:** Audit lưu ActorId, Action, EntityId, Timestamp và thông tin thay đổi cần thiết.

**BR-AUD-03:** Audit nghiệp vụ quan trọng phải được ghi cùng transaction với thay đổi tương ứng.

**BR-AUD-04:** Không xóa cứng Incident hoặc WorkOrder đã có lịch sử xử lý.

**BR-AUD-05:** Báo cáo sử dụng dữ liệu do service sở hữu cung cấp qua API hoặc read model đồng bộ bằng event.

**BR-AUD-06:** Dashboard phải thể hiện trạng thái dữ liệu đang đồng bộ nếu read model chưa cập nhật kịp.

**BR-AUD-07:** Không thực hiện cross-database JOIN để xây dựng dashboard.

**BR-AUD-08:** Thông tin báo cáo phải phân biệt số liệu hiện tại, lịch sử và snapshot.

---

# 11. QUY TẮC THỜI GIAN VÀ ĐỊNH DANH

**BR-SYS-01:** Lưu timestamp theo UTC.

**BR-SYS-02:** UI sử dụng timezone Asia/Ho_Chi_Minh.

**BR-SYS-03:** Các service sử dụng thời gian hệ thống được đồng bộ và không tin tưởng timestamp do client gửi.

**BR-SYS-04:** Các entity nghiệp vụ sử dụng ID duy nhất ổn định, đề xuất Guid.

**BR-SYS-05:** ID liên service là tham chiếu logic, không phải database foreign key.

**BR-SYS-06:** Các snapshot lịch sử không tự thay đổi khi dữ liệu nguồn được cập nhật.

**BR-SYS-07:** Mọi job định kỳ phải có cơ chế chống chạy trùng.

**BR-SYS-08:** Các operation có thể được client retry nên hỗ trợ Idempotency Key khi cần.

---

# 12. ACCEPTANCE CRITERIA — ĐIỀU KIỆN NGHIỆM THU

## AC-01: Department Isolation

Given Staff thuộc khoa A,  
When truy cập thiết bị hoặc Incident thuộc khoa B,  
Then API không trả dữ liệu và không cho phép thao tác.

## AC-02: Thay đổi khoa

Given Staff chuyển từ khoa A sang khoa B,  
When quyền mới có hiệu lực,  
Then token/quyền cũ không tiếp tục cho phép truy cập khoa A; dữ liệu lịch sử vẫn giữ DepartmentId snapshot.

## AC-03: SLA Snapshot

Given Incident được tạo với SLA Policy Version 1,  
When Admin sửa chính sách thành Version 2,  
Then deadline Incident cũ không thay đổi.

## AC-04: SLA Response

Given Incident được tạo tại T0,  
When Technician tiếp nhận tại T1,  
Then FirstRespondedAtUtc = T1 và Response SLA được tính theo deadline snapshot.

## AC-05: Multiple Blocking Incidents

Given thiết bị có hai Incident blocking,  
When một Incident được Closed,  
Then thiết bị vẫn Broken.

## AC-06: Event Out-of-Order

Given Equipment đã xử lý event Version 5,  
When event Version 4 đến sau,  
Then trạng thái không bị ghi đè.

## AC-07: Retired Equipment

Given thiết bị đã Retired,  
When Staff tạo Incident hoặc Admin tạo Schedule mới,  
Then Command bị từ chối.

## AC-08: Incident Reoccurrence

Given Incident cũ đã Closed,  
When cùng lỗi xuất hiện trở lại,  
Then tạo Incident mới và liên kết RelatedIncidentId; không Reopen hồ sơ cũ.

## AC-09: WorkOrder Idempotency

Given hai worker cùng xử lý một kỳ bảo trì,  
When cả hai cùng yêu cầu tạo WorkOrder,  
Then database chỉ có một WorkOrder cho kỳ đó.

## AC-10: Broker Failure

Given Incident đã commit cùng Outbox,  
When Redis tạm thời ngắt kết nối,  
Then Incident vẫn tồn tại; sau khi Redis phục hồi, event được phát lại.

## AC-11: Duplicate Event

Given consumer đã xử lý EventId X,  
When nhận lại EventId X,  
Then không tạo thêm thông báo hoặc cập nhật nghiệp vụ trùng.

## AC-12: SignalR Reconnect

Given người dùng mất kết nối SignalR,  
When kết nối lại,  
Then frontend tải được các thông báo đã lưu mà chưa nhận trong thời gian mất kết nối.

## AC-13: Concurrency Conflict

Given hai người cùng sửa một Incident,  
When người thứ hai lưu với rowversion cũ,  
Then API trả 409 và không ghi đè thay đổi trước đó.

## AC-14: Maintenance Calibration Failure

Given WorkOrder hiệu chuẩn có kết quả không đạt,  
When Technician hoàn tất và Admin kiểm tra,  
Then thiết bị không được đánh dấu Operational khi điều kiện blocking chưa được xử lý.

---

# 13. QUYẾT ĐỊNH KỸ THUẬT ĐƯỢC CHỐT

| Thành phần | Quyết định |
|---|---|
| Architecture | Microservices |
| Database | SQL Server, 5 database độc lập |
| ORM | EF Core Code First |
| Pattern | CQRS + Clean Architecture + Vertical Slice |
| Inter-service Sync | gRPC |
| Async Messaging | Redis Streams |
| Reliability | Outbox + Inbox + Idempotency |
| Concurrency | SQL Server rowversion |
| Authentication | JWT + Refresh Token Rotation |
| Authorization | RBAC + Department Isolation |
| Background Jobs | BackgroundService |
| Realtime | SignalR |
| Frontend | React + TypeScript |
| Gateway | YARP |
| Deployment | Docker Compose |
| Timezone Storage | UTC |
| Timezone Display | Asia/Ho_Chi_Minh |

Framework target .NET 8 hoặc .NET 10 LTS sẽ được xác nhận trong bước kiểm tra môi trường và dependency trước khi tạo solution.

---

# 14. ĐIỀU KIỆN KHÓA SPECIFICATION TRƯỚC KHI CODE

Các quy tắc nghiệp vụ trên được dùng làm baseline cho MVP. Trước khi triển khai, cần xác nhận ba quyết định kỹ thuật có ảnh hưởng trực tiếp đến tính đúng đắn:

1. **Retire coordination:** Cơ chế ngăn tạo Incident/WorkOrder đồng thời với Retire, không chỉ dựa vào một lần gRPC lookup.
2. **Authorization freshness:** Cơ chế kiểm tra AuthorizationVersion khi người dùng đổi khoa, đổi quyền hoặc bị khóa.
3. **Equipment reconciliation:** Cơ chế đối soát trạng thái thiết bị khi event bị thiếu, đến muộn hoặc broker downtime.

Sau khi ba quyết định này được cụ thể hóa trong API/Event Contract và được kiểm chứng bằng test, tài liệu có thể chuyển trạng thái sang Approved v1.0.

**Kết thúc Business Rules Specification — MedEquipSLA v1.0.**