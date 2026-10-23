# Database index

Nguồn tổng quan là [DATABASE-DOCUMENTATION](../DATABASE-DOCUMENTATION.md); không tạo dictionary trùng lặp.

- [ERD review v0.1 đã duyệt](ERD-REVIEW.md), [approval](APPROVAL.md), [erd-model.json](erd-model.json).
- [Identity ERD/data dictionary](services/Identity.md).
- [Equipment ERD/data dictionary](services/Equipment.md).
- [Maintenance ERD/data dictionary](services/Maintenance.md).
- [Incident ERD/data dictionary](services/Incident.md).
- [Notification ERD/data dictionary](services/Notification.md).
- [Technical tables dictionary](TECHNICAL-TABLES.md), [technical ERD](images/TECHNICAL-TABLES-ERD.svg), [all-services ERD](images/ALL-SERVICES-ERD.svg).
- [Setup và migrations](SETUP.md), [SQL exports](migrations/), [SQL verification](VERIFICATION.md).

47 bảng ứng dụng + 5 history đã tồn tại trên SQL Server LocalDB trong lần kiểm chứng. Compose SQL Server 2022 chưa được chạy thực; không coi schema implement là workflow đã implement. Renderer `render.cjs` chỉ sinh ảnh từ JSON, không kết nối SQL hoặc chạy migration; dùng dependency theo ghi chú đầu file nếu cần vẽ lại, không commit node_modules.
