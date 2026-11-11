# MedEquipSLA — API hiện hành

Cập nhật 07/11/2026. Bảng route sinh từ OpenAPI thu trực tiếp ở 5 Docker API hosts sau build. File JSON chứa parameters, schema request/response và metadata quyền; không suy ra mọi HTTP response từ tài liệu Swagger thay cho runtime tests. [Evidence](development/BACKEND-COMPLETION-TRACKER.md), [runbook](development/BACKEND-RUNBOOK.md).

## Kết nối và quyền

Gateway localhost:5100 xóa prefix /identity, /equipment, /maintenance, /incident, /notification. REST trực tiếp 5101/5102/5104/5105/5106; Equipment gRPC HTTP/2 riêng 5103. Swagger /swagger/index.html chỉ Development. JWT RS256 từ Identity, access 15 phút, refresh family 7 ngày cố định. Bấm Authorize dùng Bearer token sau login/MFA. Service-only key không đưa frontend.

401: token/phiên không hiện hành hoặc dependency freshness không xác minh được; 403: role; 404: không tồn tại/ngoài scope; 409: stale RowVersion/idempotency payload/state; 503: dependency chưa xác định. Error bất ngờ trả ProblemDetails với traceId, không trả SQL/secrets. RowVersion dùng base64 8 byte. Các API create D3 cần Idempotency-Key UUID; retry giữ nguyên key và payload, không đổi key để né unknown. Begin/Continue retirement có thể 202, không đồng nghĩa Retired.

Staff scope khoa áp dụng trước filter/page/detail. Technician chỉ ghi việc được phân công. Admin nghiệm thu; báo cáo Admin-only. Multipart repair/calibration report cần ảnh PNG/JPEG ≤5 MB; Calibration có measurements. Chi tiết payload chuẩn và schema tham chiếu ở OpenAPI từng owner.

## Identity

[OpenAPI JSON](api/openapi/Identity.json)

| Method | Route | Request content |
|---|---|---|
| GET | `/` | parameters / — |
| POST | `/api/auth/activate` | application/json |
| POST | `/api/auth/login` | application/json |
| POST | `/api/auth/logout` | parameters / — |
| GET | `/api/auth/me` | parameters / — |
| POST | `/api/auth/mfa/confirm` | application/json |
| POST | `/api/auth/mfa/disable` | application/json |
| POST | `/api/auth/mfa/email-code` | application/json |
| POST | `/api/auth/mfa/setup` | application/json |
| POST | `/api/auth/password-reset/confirm` | application/json |
| POST | `/api/auth/password-reset/request` | application/json |
| POST | `/api/auth/refresh` | application/json |
| GET | `/api/internal/notification-email/{id}` | parameters / — |
| POST | `/api/internal/notification-recipients` | application/json |
| GET | `/api/internal/security-mail/{user}/{ticket}` | parameters / — |
| GET | `/api/internal/technician-access/{id}` | parameters / — |
| POST | `/api/users` | application/json |
| GET | `/api/users` | parameters / — |
| POST | `/api/users/import` | multipart/form-data |
| GET | `/api/users/technicians/{id}/eligibility` | parameters / — |
| PUT | `/api/users/{id}/access` | application/json |
| POST | `/api/users/{id}/activation/resend` | application/json |
| PUT | `/api/users/{id}/profile` | application/json |

## Equipment

[OpenAPI JSON](api/openapi/Equipment.json)

| Method | Route | Request content |
|---|---|---|
| GET | `/` | parameters / — |
| GET | `/api/departments` | parameters / — |
| POST | `/api/departments` | application/json |
| GET | `/api/departments/{id}` | parameters / — |
| PUT | `/api/departments/{id}` | application/json |
| DELETE | `/api/departments/{id}` | parameters / — |
| GET | `/api/equipment` | parameters / — |
| POST | `/api/equipment` | application/json |
| GET | `/api/equipment-categories` | parameters / — |
| POST | `/api/equipment-categories` | application/json |
| GET | `/api/equipment-categories/{id}` | parameters / — |
| PUT | `/api/equipment-categories/{id}` | application/json |
| DELETE | `/api/equipment-categories/{id}` | parameters / — |
| GET | `/api/equipment-reconciliation` | parameters / — |
| GET | `/api/equipment/retirement-candidates` | parameters / — |
| GET | `/api/equipment/{equipment}/retirement-intents` | parameters / — |
| POST | `/api/equipment/{equipment}/retirement-intents` | application/json |
| GET | `/api/equipment/{equipment}/retirement-intents/{intent}` | parameters / — |
| POST | `/api/equipment/{equipment}/retirement-intents/{intent}/cancel` | application/json |
| POST | `/api/equipment/{equipment}/retirement-intents/{intent}/continue` | application/json |
| GET | `/api/equipment/{id}` | parameters / — |
| POST | `/api/equipment/{id}/department-transfer` | application/json |
| GET | `/api/equipment/{id}/profile` | parameters / — |
| PUT | `/api/equipment/{id}/profile` | application/json |
| GET | `/api/reports/equipment` | parameters / — |

## Maintenance

[OpenAPI JSON](api/openapi/Maintenance.json)

| Method | Route | Request content |
|---|---|---|
| GET | `/` | parameters / — |
| GET | `/api/diagnostics/equipment-grpc` | parameters / — |
| GET | `/api/maintenance-schedule-operations` | parameters / — |
| GET | `/api/maintenance-schedule-operations/{id}` | parameters / — |
| POST | `/api/maintenance-schedule-operations/{id}/cancel` | application/json |
| POST | `/api/maintenance-schedule-operations/{id}/retry` | application/json |
| GET | `/api/maintenance-schedules` | parameters / — |
| POST | `/api/maintenance-schedules` | application/json |
| GET | `/api/maintenance-schedules/{id}` | parameters / — |
| PUT | `/api/maintenance-schedules/{id}` | application/json |
| POST | `/api/maintenance-schedules/{id}/end` | application/json |
| POST | `/api/maintenance-schedules/{id}/end/retry-release` | application/json |
| GET | `/api/maintenance-schedules/{id}/occurrences` | parameters / — |
| GET | `/api/maintenance/equipment/{id}` | parameters / — |
| GET | `/api/reports/maintenance` | parameters / — |
| GET | `/api/work-order-operations` | parameters / — |
| GET | `/api/work-order-operations/{id}` | parameters / — |
| POST | `/api/work-order-operations/{id}/cancel` | application/json |
| POST | `/api/work-order-operations/{id}/retry` | application/json |
| GET | `/api/work-orders` | parameters / — |
| GET | `/api/work-orders/{id}` | parameters / — |
| PUT | `/api/work-orders/{id}/assignment` | application/json |
| GET | `/api/work-orders/{id}/attempts` | parameters / — |
| POST | `/api/work-orders/{id}/attempts` | multipart/form-data |
| GET | `/api/work-orders/{id}/attempts/{attempt}/measurements` | parameters / — |
| GET | `/api/work-orders/{id}/attempts/{attempt}/photo` | parameters / — |
| POST | `/api/work-orders/{id}/cancel` | application/json |
| POST | `/api/work-orders/{id}/progress` | application/json |
| POST | `/api/work-orders/{id}/review` | application/json |
| POST | `/api/work-orders/{id}/start` | application/json |

## Incident

[OpenAPI JSON](api/openapi/Incident.json)

| Method | Route | Request content |
|---|---|---|
| GET | `/` | parameters / — |
| GET | `/api/diagnostics/equipment-grpc` | parameters / — |
| GET | `/api/incident-operations` | parameters / — |
| GET | `/api/incident-operations/{id}` | parameters / — |
| POST | `/api/incident-operations/{id}/cancel` | application/json |
| POST | `/api/incident-operations/{id}/retry` | application/json |
| GET | `/api/incidents` | parameters / — |
| POST | `/api/incidents` | application/json |
| GET | `/api/incidents/search` | parameters / — |
| GET | `/api/incidents/{id}` | parameters / — |
| PUT | `/api/incidents/{id}/assignment` | application/json |
| GET | `/api/incidents/{id}/attempts` | parameters / — |
| POST | `/api/incidents/{id}/attempts` | multipart/form-data |
| POST | `/api/incidents/{id}/attempts/{attemptId}/accept` | application/json |
| GET | `/api/incidents/{id}/attempts/{attemptId}/photo` | parameters / — |
| POST | `/api/incidents/{id}/attempts/{attemptId}/reject` | application/json |
| POST | `/api/incidents/{id}/start` | application/json |
| GET | `/api/reports/incident` | parameters / — |
| GET | `/api/sla-policies` | parameters / — |
| PUT | `/api/sla-policies/{priority}` | application/json |

## Notification

[OpenAPI JSON](api/openapi/Notification.json)

| Method | Route | Request content |
|---|---|---|
| GET | `/` | parameters / — |
| GET | `/api/email-deliveries` | parameters / — |
| GET | `/api/email-deliveries/{id}/attempts` | parameters / — |
| POST | `/api/email-deliveries/{id}/retry` | application/json |
| GET | `/api/notification-preferences` | parameters / — |
| PUT | `/api/notification-preferences/{kind}` | application/json |
| GET | `/api/notifications` | parameters / — |
| PUT | `/api/notifications/{id}/read` | parameters / — |

## SignalR, gRPC và events

Notification hub /hubs/notifications hỗ trợ query access_token chỉ trên hub; đúng token được kiểm freshness, không lấy Authorization header khác thay thế. Hint notificationsChanged chứa notificationId; reconnect đọc REST. Logout one thu hồi đúng session, access change/logout all thu hồi version cũ. REST feed là nguồn bền.

[gRPC riêng](development/GRPC.md), [broker/background](development/MESSAGING.md), [D3 state/recovery](development/D3-SNAPSHOT-RECONCILIATION.md), [nghiệp vụ](BUSINESS-DOCUMENTATION.md). Không dùng Swagger health/probe để kết luận business RPC hay toàn bộ BE hoàn tất.
