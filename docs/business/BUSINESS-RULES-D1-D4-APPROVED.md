# Phụ lục Business Rules — D1–D4 đã được duyệt

Ngày ghi nhận: 23/10/2026. Nguồn phê duyệt là [ERD approval](../database/APPROVAL.md), lời người dùng “oke tôi duyệt hãy tạo db này”. Phụ lục đồng bộ các quyết định đã duyệt vào bộ tài liệu nghiệp vụ; **không tạo quyết định mới** và không đổi toàn bộ [BR v1.0 gốc](BUSINESS-RULES-v1.0-PROPOSED.md) từ Proposed thành Approved.

| Quyết định | BR được làm rõ/giới hạn | Nội dung đã duyệt | Trạng thái triển khai |
|---|---|---|---|
| D1 | BR-SLA-16/17/20..22; BR-INC-14/21/22 | Resolution SLA theo CompletedAtUtc của ResolutionAttempt cuối cùng được Admin chấp nhận, không lấy ReviewedAtUtc/ClosedAtUtc thay thế. Giữ các attempt bị reject. Deadline alert là tạm tính; outcome cuối khóa khi Closed | Schema/constraints có; tính outcome/nghiệm thu còn Planned |
| D2 | BR-MNT-05/07..13/25..30 | Materialize ScheduleOccurrence trước cửa sổ reminder (mặc định 1440 phút); reminder gắn occurrence, WO chỉ tạo khi đến kỳ. Unique kỳ và OccurrenceId chống trùng. DueAt = occurrence + CompletionWindowMinutes, default 0; inactive không tự hủy WO đã sinh, catch-up giữ kỳ gốc | Schema/unique có; scheduler/reminder còn Planned |
| D3 | BR-EQP-14/17/19/21; BR-EVT-09..11 | Source giữ operation Pending trước register; Register/Retire serialize trên cùng Equipment row. Pending/Active chặn Retire. Source commit aggregate + operation Committed + Outbox local; chỉ release khi hồ sơ kết thúc hoặc Aborted bền và không thể commit muộn. Không release theo TTL. Có reconciliation checkpoint/snapshot đầy đủ | **Mô hình dữ liệu Approved; protocol chi tiết BLOCKED** |
| D4 | BR-MNT-19..24 | Calibration Failed không Close để giải phóng blocking; reject về InProgress, attempt mới, chỉ Passed được nghiệm thu mới Closed. Cancel trực tiếp giới hạn Scheduled chưa bắt đầu; không áp dụng cách hiểu “mọi WO chưa Closed đều Cancel được” của BR-MNT-21 gốc | Schema lưu kết quả/review có; transitions/blocking workflow còn Planned |

## D3 — phần chưa được chốt và không được tự triển khai

- gRPC register/confirm/release/abort contract, fencing token và cách buộc source không commit sau Aborted.
- Snapshot pagination ổn định, watermark/tombstone, kết hợp event đồng thời và điều kiện hoàn tất reconciliation.
- Recovery khi source crash/mất kết nối, bootstrap dữ liệu cũ, idempotency/retention của operation tombstone.

Chưa có proto hoặc test E2E chứng minh các điều kiện trên. Vì vậy không tuyên bố “đã giải quyết race Retire” chỉ từ EquipmentUseRegistrations/IdempotencyRequests/ReconciliationCheckpoints. Không thêm bảng/cột hoặc viết workflow bù trừ để tự quyết định thay owner. Những kiểm tra schema, EF concurrency và rollback migration độc lập với protocol được phép tiếp tục trên DB test riêng.

## Ví dụ nghiệm thu đã được D1/D4 làm rõ

- Hoàn tất 09:50, deadline 10:00, được accept 10:30: Resolution Met theo thời điểm hoàn tất của attempt được nhận.
- Lần 09:50 bị reject, lần sau hoàn tất 10:20 và được accept: Resolution Breached, giữ cả hai attempts.
- Calibration Failed: vẫn blocking khi Completed và khi trả lại InProgress; không dùng Close/Cancel để đưa thiết bị về Operational.

Các ví dụ mô tả yêu cầu đã duyệt, không phải behavior tests đã PASS. [Audit/database finalization](../database/FINALIZATION-AUDIT.md) phân biệt evidence schema với behavior còn thiếu.
