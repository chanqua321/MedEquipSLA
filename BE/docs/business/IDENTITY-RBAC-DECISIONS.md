# Quyết định nghiệp vụ Identity và RBAC

Chốt ngày 05/11/2026 theo bản IAM-01 đến IAM-19 và phần rà soát tiếp theo. Đây là căn cứ triển khai và nghiệm thu. Khôi phục tài khoản Admin, kể cả khi mất phương thức MFA, nằm ngoài phạm vi.

| Vai trò | Phạm vi đọc | Thao tác |
|---|---|---|
| Admin | Toàn hệ thống | Quản lý tài khoản, khoa, thiết bị, SLA và lịch. Phân công, xử lý và nghiệm thu theo quy trình. |
| Technician | Thiết bị và hồ sơ công việc toàn hệ thống | Tiếp nhận và cập nhật Incident hoặc WorkOrder được giao. Quyền tạo Incident thuộc Admin và Staff theo phạm vi khoa. Quyền quản lý lịch và nghiệm thu thuộc Admin. |
| Department Staff | Thiết bị theo khoa hiện tại của thiết bị. Hồ sơ lịch sử theo khoa lưu trên hồ sơ. | Tạo Incident cho thiết bị thuộc khoa hiện tại của nhân viên. Backend áp phạm vi trước khi đọc chi tiết, lọc hoặc phân trang. |

Admin được xem và xuất báo cáo toàn hệ thống. Technician và Staff dùng các màn hình tra cứu theo quyền trong bảng. Xuất báo cáo dành cho hai vai trò này nằm ngoài phạm vi hiện tại.

## Tài khoản và xác thực

- Mỗi tài khoản có đúng một vai trò nghiệp vụ. Bootstrap chỉ tạo một Admin, dùng email đã xác nhận và secret triển khai. Chặn tạo thêm Admin, tự hạ quyền và tự vô hiệu hóa Admin qua các thao tác quản lý thông thường. Các yêu cầu tạo đồng thời cũng phải giữ được giới hạn này.
- Admin tạo nhân viên từng người hoặc import Excel. Import cho phép thành công từng dòng, báo lỗi từng dòng và dùng email chuẩn hóa để tránh tạo trùng khi chạy lại. Gửi lại lời mời là thao tác riêng. Department Staff bắt buộc có khoa đang hoạt động. Technician có thể lưu khoa để quản lý nhân sự, còn quyền xử lý dựa trên phân công.
- Lời mời kích hoạt dùng một lần và hết hạn sau 24 giờ. Người dùng xác minh email và đặt mật khẩu trước khi chuyển từ PendingActivation sang Active. Gửi lại lời mời thay thế liên kết cũ. Tài khoản Inactive cần Admin kích hoạt lại và vẫn phải đáp ứng điều kiện kích hoạt ban đầu.
- Mật khẩu tối thiểu 12 ký tự, cho phép cụm mật khẩu, từ chối mật khẩu phổ biến và kiểm tra mật khẩu đã lộ nếu có dịch vụ phù hợp. Dùng bộ băm mật khẩu chuyên dụng và không yêu cầu đổi định kỳ.
- Admin phải thiết lập TOTP trước khi vào chức năng quản trị. Recovery Code dùng một lần có thể thay TOTP trong lần đăng nhập đã xác thực mật khẩu. Email của Admin dùng để xác minh địa chỉ và gửi thông báo bảo mật. Technician và Staff được chọn TOTP hoặc Email OTP, và phải xác thực lại khi đổi hoặc tắt MFA.
- Sau 5, 6, 7 và từ 8 lần sai liên tiếp, khóa đăng nhập lần lượt 15, 30, 60 và 120 phút. Yêu cầu trong thời gian khóa không tăng bộ đếm hoặc kéo dài khóa. Hết khóa giữ số lần sai, xác thực thành công đầy đủ mới đặt lại. Áp dụng giới hạn theo tài khoản và IP. Khóa tạm thời là trạng thái đăng nhập, tách khỏi vòng đời tài khoản và không đổi phân công.
- Technician và Staff có thể đặt lại mật khẩu bằng liên kết dùng một lần, hết hạn sau 30 phút. Phản hồi yêu cầu khôi phục dùng cùng một thông điệp cho mọi email. Sau khi đổi mật khẩu, thu hồi mọi phiên và yêu cầu đăng nhập lại.

## Phiên và thay đổi quyền

- Mỗi lần đăng nhập tạo một SessionId. Access Token có hiệu lực tối đa 15 phút. Phiên hết hạn cố định 7 ngày từ lần đăng nhập, refresh chỉ thay token trong phiên đó. Cho phép nhiều phiên độc lập và lưu Refresh Token dưới dạng hash.
- Rotation cập nhật token cũ và token mới trong cùng giao dịch. Khi phát hiện token đã xoay bị dùng lại, thu hồi phiên tương ứng và ghi sự kiện bảo mật. Client gửi tối đa một yêu cầu refresh đồng thời cho mỗi phiên.
- API có phân quyền kiểm tra chữ ký, thời hạn, SessionId, trạng thái tài khoản và AuthorizationVersion hiện hành trước khi kiểm tra role và phạm vi dữ liệu. Khi không xác minh được quyền hiện hành, từ chối yêu cầu. Đăng xuất một thiết bị thu hồi phiên đó. Đăng xuất tất cả, đổi mật khẩu, đổi vai trò, đổi khoa hoặc vô hiệu hóa thu hồi mọi phiên và tăng AuthorizationVersion.
- Khi Technician mất quyền xử lý, API từ chối ngay quyền cũ. Incident và Maintenance xử lý thông báo từ Identity để gỡ phân công các việc còn mở mà người đó không còn đủ quyền. Mỗi service chỉ cập nhật hồ sơ vẫn giao cho đúng người tại thời điểm xử lý, giữ lịch sử, trạng thái nghiệp vụ và mốc SLA.
- Audit ghi người thực hiện, đối tượng, thời điểm, hành động và kết quả. Thao tác đổi quyền ghi giá trị cũ và mới. Không ghi mật khẩu, OTP, Refresh Token hoặc Recovery Code vào log.

## Đối chiếu dữ liệu và nghiệm thu

Đối chiếu ERD hiện tại trước khi làm migration cho phiên, lời mời, MFA và sự kiện thay đổi quyền. Có thể dùng FamilyId hiện có làm định danh phiên nếu mô hình đáp ứng được kiểm tra và thu hồi từng phiên.

Nghiệm thu các ca tạo Admin đồng thời, kích hoạt sau khi bị vô hiệu hóa, hết thời gian khóa đăng nhập, refresh đồng thời, dùng lại token cũ, đăng xuất một thiết bị, đổi khoa khi JWT còn hạn và gỡ phân công sau khi Technician mất quyền.
