# MedEquipSLA — API Documentation

Audit 23/10/2026. Nguồn: `Program.cs` của năm API, [ServiceSetup](../src/BuildingBlocks/MedEquipSLA.Hosting/ServiceSetup.cs), [Equipment proto](../src/Contracts/MedEquipSLA.Contracts.Grpc/Protos/equipment.proto), [IntegrationEvent](../src/Contracts/MedEquipSLA.Contracts.Events/IntegrationEvent.cs). **Implemented** có thể là transport/scaffold; **Planned** chưa có nghiệp vụ/DTO tương ứng. Không có API Deprecated được xác định.

## 1. Architecture và Gateway

Web → YARP → API → Application Command/Query handler → repository/read adapter → database owner. Gateway không cấp JWT, không truy vấn DB, không thay authorization tại service. CQRS không yêu cầu hai database đọc/ghi.

| Owner | Base URL host | Gateway prefix |
|---|---|---|
| Identity | http://localhost:5101 | `/identity` |
| Equipment | http://localhost:5102 | `/equipment` |
| Maintenance | http://localhost:5104 | `/maintenance` |
| Incident | http://localhost:5105 | `/incident` |
| Notification | http://localhost:5106 | `/notification` |

Gateway http://localhost:5100 xóa prefix: `/incident/api/incidents` → Incident `/api/incidents`. Web xóa `/api` trước Gateway: `/api/incident/api/incidents` → `/incident/api/incidents`. REST không chạy trên gRPC port 5103. Gateway chưa tổng hợp OpenAPI.

## 2. Authentication và authorization

Middleware hiện xác minh JWT RS256, signature, issuer `https://identity.medequip.local`, audience `medequip-api`, lifetime/expiration; clock skew 30 giây. Public PEM lấy từ `Jwt__PublicKeyPath`, mặc định rỗng. Không có login/refresh/revoke endpoint, signing key hoặc tài khoản mặc định. RefreshTokens table không tự tạo token rotation.

Resource GET chỉ `RequireAuthorization()`, **chưa có role/department policy**. Principal có JWT hợp lệ được vào handler skeleton. Thiếu/hỏng token → 401, thường body rỗng. Swagger chưa có security definition/nút Authorize bearer; dùng curl/Postman gửi token khi có issuer thực.

Quyền đích Planned: Admin quản lý toàn hệ thống; Technician đọc toàn hệ thống nhưng chỉ ghi việc được phân công; Staff (`DepartmentStaff`) chỉ trong khoa hiện tại. Scope phải áp dụng trên backend cho list/detail/search/export/command; không tin DepartmentId/ActorId client. Claims UserId/Role/DepartmentId/AuthorizationVersion và revalidation chưa được triển khai. Identity gRPC freshness vẫn là đề xuất, chưa có proto.

## 3. Endpoint thực tế

Path là path tại service. `—` nghĩa không có HTTP parameter/request DTO/body. Các anonymous response hiện là object trực tiếp, không có named response DTO.

| Owner | Method / route | Auth / roles hiện tại | Request | Response / HTTP | BR / mục đích | Status |
|---|---|---|---|---|---|---|
| Mỗi service | `GET /` | Anonymous / mọi người | — | 200 `{service,phase,businessImplemented:false}` | Chẩn đoán | Implemented |
| Mỗi service | `GET /health/live` | Anonymous / mọi người | — | 200 text `Healthy` | Chỉ process; không kiểm tra DB/Redis | Implemented |
| Gateway | `GET /health/live` | Anonymous / mọi người | — | 200 `{status:"live",phase:"scaffold"}` | BR-ARCH-08 | Implemented |
| Identity | `GET /api/users` | JWT / mọi principal hợp lệ | — | 401 hoặc 501 ProblemDetails | BR-AUTH-07/12 | Implemented scaffold; list Planned |
| Equipment | `GET /api/equipment` | JWT / như trên | — | 401 hoặc 501 | BR-AUTH-01/04, BR-EQP-01 | Implemented scaffold; list Planned |
| Maintenance | `GET /api/work-orders` | JWT / như trên | — | 401 hoặc 501 | BR-AUTH-06, BR-MNT-14..24 | Implemented scaffold; list Planned |
| Incident | `GET /api/incidents` | JWT / như trên | — | 401 hoặc 501 | BR-AUTH-03/04, BR-INC-01..28 | Implemented scaffold; list Planned |
| Notification | `GET /api/notifications` | JWT / như trên | — | 401 hoặc 501 | BR-NOTI-03 | Implemented scaffold; list Planned |
| Incident | `GET /api/diagnostics/equipment-grpc` | Anonymous, Development only | — | 200 `{service,phase,businessLookupImplemented:false}`; RPC lỗi hiện thành 500 | BR-ARCH-05 transport | Implemented |
| Maintenance | `GET /api/diagnostics/equipment-grpc` | Anonymous, Development only | — | Như Incident; RPC deadline 3 giây | BR-ARCH-05 transport | Implemented |
| Mỗi service | `GET /swagger/v1/swagger.json` | Anonymous, Development only | — | 200 OpenAPI JSON | Tài liệu REST | Implemented |
| Mỗi service | `GET /swagger/index.html` | Anonymous, Development only | — | 200 HTML/assets | Tài liệu REST | Implemented |

“Mỗi service” là năm owner, không gồm Gateway. Route tài nguyên tự tạo `ScaffoldQuery(Guid.Empty)`; không bind ID hoặc search/filter/sort/page. Handler `IQuery<bool>` luôn ném NotImplementedException, chưa có response danh sách 200. `ScaffoldCommand(Guid Id)`/validator `Id.NotEmpty()` được đăng ký nhưng chưa có POST/PUT route gọi command. Không có Controller nghiệp vụ ngoài Minimal API trên.

Ví dụ response hiện có của `GET http://localhost:5102/`:

```json
{"service":"Equipment","phase":"scaffold","businessImplemented":false}
```

Diagnostic response khi Equipment chạy Development:

```json
{"service":"Equipment","phase":"scaffold","businessLookupImplemented":false}
```

Sau JWT hợp lệ, handler skeleton trả 501 theo hình dạng sau (`traceId` minh họa):

```json
{"title":"Feature not implemented in scaffold","status":501,"code":"scaffold.not_implemented","traceId":"<request trace>"}
```

Exception handler chỉ map NotImplementedException → 501 và lỗi khác → 500 (`server.error`). Hiện dùng WriteAsJsonAsync với ProblemDetails; không cam kết đã có `application/problem+json`, `type`, `instance` đầy đủ. 401 từ auth middleware không được tự gán business error JSON.

## 4. REST contract đề xuất — tất cả Planned

Tên DTO, route mới và response dưới đây **chưa phải code đã tồn tại**, cần review khi triển khai feature. `A` = Admin, `T` = Technician, `S` = DepartmentStaff; mọi dòng cần JWT trừ login/refresh. Scope khoa áp dụng đồng thời với role. Update/transition nhận rowVersion base64 dự kiến; actor, timestamp và SLA deadline do server quyết định.

| Owner | Method / route đề xuất | Allowed roles / scope | Request DTO / parameters | Response DTO / HTTP dự kiến | BR |
|---|---|---|---|---|---|
| Identity | `POST /api/auth/login` | Anonymous | LoginRequest | TokenResponse, 200/400/401 | AUTH-09..13 |
| Identity | `POST /api/auth/refresh` | Refresh credential | RefreshRequest | TokenResponse, 200/401 | AUTH-10/11 |
| Identity | `POST /api/auth/logout` | Self | RevokeRefreshRequest | 204/401 | AUTH-11 |
| Identity | `GET /api/users` | A | ListQuery | `Page<UserDto>`, 200 | AUTH-07 |
| Identity | `GET /api/users/{id}` | A | Guid id | UserDto, 200/404 | AUTH-07 |
| Identity | `POST /api/users` | A | CreateUserRequest | UserDto, 201/422 | DEPT-03/06 |
| Identity | `PUT /api/users/{id}` | A | id, UpdateUserRequest | UserDto, 200/409 | AUTH-11 |
| Equipment | `GET /api/departments` | A,T; S scope | ListQuery | `Page<DepartmentDto>`, 200 | AUTH-01/04 |
| Equipment | `GET /api/departments/{id}` | A,T; S scope | Guid id | DepartmentDto, 200/404 | DEPT-01 |
| Equipment | `POST /api/departments` | A | DepartmentWriteRequest | DepartmentDto, 201/422 | DEPT-01/05 |
| Equipment | `PUT /api/departments/{id}` | A | id, DepartmentWriteRequest + rowVersion | DepartmentDto, 200/409 | DEPT-05/06 |
| Equipment | `GET /api/equipment-categories` | A,T,S | ListQuery | `Page<CategoryDto>`, 200 | ARCH-07 |
| Equipment | `GET /api/equipment-categories/{id}` | A,T,S | Guid id | CategoryDto, 200/404 | ARCH-07 |
| Equipment | `POST /api/equipment-categories` | A | CategoryWriteRequest | CategoryDto, 201/422 | ARCH-07 |
| Equipment | `PUT /api/equipment-categories/{id}` | A | id, CategoryWriteRequest + rowVersion | CategoryDto, 200/409 | CON-01..04 |
| Equipment | `GET /api/equipment` | A,T; S scope | EquipmentListQuery | `Page<EquipmentDto>`, 200 | AUTH-01/04/08 |
| Equipment | `GET /api/equipment/{id}` | A,T; S scope | Guid id | EquipmentDto, 200/404 | AUTH-08 |
| Equipment | `POST /api/equipment` | A | EquipmentWriteRequest | EquipmentDto, 201/422 | DEPT-06 |
| Equipment | `PUT /api/equipment/{id}` | A | id, EquipmentWriteRequest + rowVersion | EquipmentDto, 200/409 | DEPT-07, CON-03 |
| Equipment | `POST /api/equipment/{id}/retire` | A | id, ReasonVersionRequest | EquipmentDto, 200/409/422/503 | EQP-13..21, D3 |
| Incident | `GET /api/incidents` | A,T; S snapshot scope | IncidentListQuery | `Page<IncidentDto>`, 200 | AUTH-03/04 |
| Incident | `GET /api/incidents/{id}` | A,T; S snapshot scope | Guid id | IncidentDto, 200/404 | DEPT-08 |
| Incident | `POST /api/incidents` | A,S scope | CreateIncidentRequest | IncidentDto, 201/422/503 | INC-01..08, D3 |
| Incident | `PUT /api/incidents/{id}` | A | id, UpdateIncidentRequest | IncidentDto, 200/409/422 | SLA-09, INC-19 |
| Incident | `POST /api/incidents/{id}/assign` | A | id, AssignRequest | IncidentDto, 200/409/422 | INC-10 |
| Incident | `POST /api/incidents/{id}/accept` | A,T assigned | id, VersionRequest | IncidentDto, 200/409 | INC-11/12 |
| Incident | `POST /api/incidents/{id}/resolve` | A,T assigned | id, ResolveRequest | ResolutionAttemptDto, 200/409/422 | INC-13..15, D1 |
| Incident | `POST /api/incidents/{id}/review` | A | id, ReviewRequest | IncidentDto, 200/409/422 | INC-16..22, D1 |
| Incident | `GET /api/sla-policies` | A,T đọc hệ thống | ListQuery | `Page<SlaPolicyDto>`, 200 | SLA-01..05 |
| Incident | `GET /api/sla-policies/{id}` | A,T đọc hệ thống | Guid id | SlaPolicyDto, 200/404 | SLA-05 |
| Incident | `POST /api/sla-policies` | A | SlaPolicyRequest | SlaPolicyDto, 201/422 | SLA-01..05 |
| Incident | `PUT /api/sla-policies/{id}` | A | id, SlaPolicyRequest + rowVersion | SlaPolicyDto, 200/409/422; thêm version | SLA-05/08 |
| Maintenance | `GET /api/maintenance-schedules` | A,T đọc hệ thống | ListQuery | `Page<ScheduleDto>`, 200 | MNT-01..06 |
| Maintenance | `GET /api/maintenance-schedules/{id}` | A,T đọc hệ thống | Guid id | ScheduleDto, 200/404 | MNT-01 |
| Maintenance | `POST /api/maintenance-schedules` | A | ScheduleWriteRequest | ScheduleDto, 201/422/503 | MNT-02..05, D3 |
| Maintenance | `PUT /api/maintenance-schedules/{id}` | A | id, ScheduleWriteRequest + rowVersion | ScheduleDto, 200/409/422 | MNT-05/06, D2 |
| Maintenance | `GET /api/work-orders` | A,T đọc hệ thống | WorkOrderListQuery | `Page<WorkOrderDto>`, 200 | AUTH-06 |
| Maintenance | `GET /api/work-orders/{id}` | A,T đọc hệ thống | Guid id | WorkOrderDto, 200/404 | AUTH-06 |
| Maintenance | `POST /api/work-orders` | A | CreateWorkOrderRequest | WorkOrderDto, 201/422/503 | EQP-16/19, D3 |
| Maintenance | `POST /api/work-orders/{id}/assign` | A | id, AssignRequest | WorkOrderDto, 200/409 | AUTH-06 |
| Maintenance | `POST /api/work-orders/{id}/start` | A,T assigned | id, VersionRequest | WorkOrderDto, 200/409 | MNT-15/16 |
| Maintenance | `POST /api/work-orders/{id}/complete` | A,T assigned | id, CompleteWorkOrderRequest | WorkOrderAttemptDto, 200/409/422 | MNT-17/18, D4 |
| Maintenance | `POST /api/work-orders/{id}/review` | A | id, ReviewRequest | WorkOrderDto, 200/409/422 | MNT-19/20, D4 |
| Maintenance | `POST /api/work-orders/{id}/cancel` | A, Scheduled only | id, ReasonVersionRequest | WorkOrderDto, 200/409/422 | D4 giới hạn MNT-21 |
| Notification | `GET /api/notifications` | Self | NotificationListQuery | `Page<NotificationDto>`, 200 | NOTI-02/03 |
| Notification | `GET /api/notifications/{id}` | Self | Guid id | NotificationDto, 200/404 | NOTI-03 |
| Notification | `PUT /api/notifications/{id}/read` | Self | id, VersionRequest | NotificationDto, 200/409 | NOTI-03 |

BR ID ở bảng viết tắt tiền tố `BR-`. Tất cả route bảo vệ còn có 401/403 và lỗi phụ thuộc/server. Quyền đọc WorkOrder của Staff chưa được định nghĩa đủ trong BR; không tự suy ra quyền từ DepartmentIdSnapshot. Reports/export, SignalR hub và quản trị delivery/retry chưa có route được chốt.

**DELETE: Planned, semantics chưa chốt** cho từng core entity. Catalog chưa sử dụng có thể xóa có kiểm tra; hồ sơ có lịch sử giữ/archive theo BR. Không có DELETE nào trong source; Retire/Cancel không tự chứng minh CRUD Delete của đề. Cần quyết định và acceptance tests trước code.

### DTO/validation catalog đề xuất

| DTO | Trường / validation đích |
|---|---|
| LoginRequest / RefreshRequest / RevokeRefreshRequest | username/password hoặc refreshToken; không log credential |
| CreateUserRequest / UpdateUserRequest | userName, email, fullName, role, departmentId; update có isActive/rowVersion; password policy chưa chốt |
| DepartmentWriteRequest / CategoryWriteRequest | code ≤50, name ≤200, isActive; category description tùy chọn |
| EquipmentWriteRequest | assetCode ≤50, name ≤200, categoryId, departmentId, serialNumber ≤100?; không nhận operationalStatus tùy ý |
| CreateIncidentRequest | equipmentId, title ≤200, description ≤4000, priority, relatedIncidentId? |
| UpdateIncidentRequest | title/description/priority hiển thị, reason, rowVersion; không sửa SLA snapshot |
| AssignRequest / VersionRequest / ReasonVersionRequest | technicianId + rowVersion / rowVersion / reason + rowVersion |
| ResolveRequest | resolutionNotes ≤4000, resultSummary ≤2000, rowVersion; CompletedAtUtc do server tạo |
| ReviewRequest | attemptId, decision Accepted/Rejected, reason (bắt buộc khi reject), rowVersion |
| SlaPolicyRequest | priority/name, responseDurationMinutes >0, resolutionDurationMinutes ≥ response, effectiveFromUtc/effectiveToUtc?; không overlap |
| ScheduleWriteRequest | equipmentId, workType, anchorAtUtc, intervalDays >0, reminderLeadMinutes ≥0 (default 1440), completionWindowMinutes ≥0 (default 0), isActive |
| CreateWorkOrderRequest | equipmentId, workType, scheduledForUtc, dueAtUtc, assignedTechnicianId?; không giả OccurrenceId |
| CompleteWorkOrderRequest | resultStatus, notes, cost? ≥0, currency?, measurements[], rowVersion |
| Measurement | parameterCode/name, unit, measuredValue, min/max (ít nhất một); Passed đúng threshold |

`?` nghĩa optional. Response DTO dự kiến là projection theo scope, không trả passwordHash/token hash/secret. TokenResponse đề xuất accessToken/refreshToken/expiresIn; cookie/body refresh transport, claim naming và token policy cần review. SQL lengths là giới hạn lưu trữ, không chứng minh validator đã chạy.

## 5. Search/filter/sort/page và lỗi Planned

ListQuery đề xuất `page=1`, `pageSize=20` (1..100), `search`, `sortBy`, `sortDirection=asc|desc`. Equipment filter status/categoryId/departmentId; Incident status/priority/equipmentId/fromUtc/toUtc; WorkOrder status/workType/assignedTechnicianId; Notification unread. Allowlist sort, thêm Id tie-breaker, scope trước count/page. Chưa có query parameters này trong endpoints hiện tại.

Ví dụ Planned `POST /api/incidents`; UUID/code chỉ minh họa:

```json
{"equipmentId":"11111111-1111-1111-1111-111111111111","title":"Thiết bị không khởi động","description":"Đã kiểm tra nguồn điện","priority":"High","relatedIncidentId":null}
```

Ví dụ `Page<IncidentDto>` Planned, không phải response live:

```json
{"items":[{"id":"22222222-2222-2222-2222-222222222222","code":"INC-DEMO","status":"Open"}],"page":1,"pageSize":20,"totalCount":1}
```

| HTTP đích | Ý nghĩa | Business error code đề xuất, chưa có code |
|---|---|---|
| 400 | JSON/parameter sai | `validation.invalid_request` |
| 401 / 403 | Token sai / quyền đã xác minh không đủ | `auth.unauthenticated` / `auth.forbidden` |
| 404 | Không có resource trong scope | `resource.not_found` |
| 409 | Version/state conflict | `concurrency.conflict`, `workflow.invalid_transition` |
| 422 | Vi phạm nghiệp vụ | `equipment.retired`, `sla.policy_missing`, `calibration.not_accepted` |
| 503 | Phụ thuộc/quyền chưa xác minh | `dependency.unavailable` |

Đích dùng ProblemDetails có code/traceId và validation errors khi phù hợp. Hiện chưa map DbUpdateConcurrencyException →409, validator →400/422, RpcException →503. Duplicate warning không lộ hồ sơ khoa khác; không unique riêng Incident.EquipmentId.

## 6. gRPC contracts

Owner Equipment, service `medequip.equipment.v1.EquipmentLookup`, host `http://localhost:5103` HTTP/2 h2c (container `equipment:8081`). C# namespace `MedEquipSLA.Contracts.Grpc.Equipment.V1`. Incident/Maintenance có generated client; chưa gửi service credential cho lookup nghiệp vụ.

| RPC | Request / response fields trong proto | Auth / roles thực tế | Status |
|---|---|---|---|
| GetServiceInfo | ServiceInfoRequest `{}` → ServiceInfoReply `service:string`, `phase:string` | Anonymous; ngoài Development PermissionDenied | Implemented probe |
| GetEquipment | GetEquipmentRequest `equipment_id:string` → EquipmentReply `equipment_id,asset_code,department_id,status:string`, `aggregate_version:int64` | `[Authorize]`, chưa role/department policy | Proto Implemented; lookup Planned |

GetEquipment thiếu token → UNAUTHENTICATED; sau auth → UNIMPLEMENTED. Method chưa kiểm tra UUID/business equipment. Diagnostic REST gọi GetServiceInfo với deadline 3 giây/cancellation. Identity authorization/user lookup, Equipment department lookup/registration, Incident/Maintenance snapshot reconciliation đều **Planned, chưa có proto**. REST không thay demo gRPC. h2c chỉ dùng development network; TLS cho môi trường ngoài máy dev.

## 7. Integration Events

Envelope có code: `IntegrationEvent(EventId:Guid, EventType:string, SchemaVersion:int, Producer:string, AggregateId:Guid, AggregateVersion:long, OccurredAtUtc:DateTimeOffset, CorrelationId:string, Payload:string)`. Payload là **string chứa JSON**. RedisEventPublisher XADD một field `event=JsonSerializer.Serialize(message)`, mặc định PascalCase; stream name do caller truyền. Chưa có stream nghiệp vụ hardcode, typed payload, dispatcher hay consumer hoạt động.

Ví dụ hình dạng envelope hiện có; event type/payload là Planned:

```json
{"EventId":"33333333-3333-3333-3333-333333333333","EventType":"IncidentCreated","SchemaVersion":1,"Producer":"Incident","AggregateId":"22222222-2222-2222-2222-222222222222","AggregateVersion":1,"OccurredAtUtc":"2026-10-23T00:00:00+00:00","CorrelationId":"demo-only","Payload":"{\"equipmentId\":\"11111111-1111-1111-1111-111111111111\"}"}
```

| Event names/family đích | Producer | Consumers đích | Status |
|---|---|---|---|
| IncidentCreated/Assigned/Resolved/Closed | Incident | Notification; Equipment nhận state-relevant events | Planned payload/routing |
| SlaResponseBreached/SlaResolutionBreached | Incident scanner | Notification | Planned; cảnh báo tạm tính D1, không tự thành final breach |
| MaintenanceDueSoon/MaintenanceOverdue | Maintenance | Notification | Planned; occurrence trước khi có WorkOrder |
| WorkOrderAssigned/Completed | Maintenance | Notification; Equipment cần projection | Planned |
| Equipment status/department change | Equipment | Consumers theo use case | Planned names/payload |
| WorkOrder start/close/cancel; source commit/release | Maintenance/Incident | Equipment registration/projection | Planned bổ sung; chưa typed events |

Danh sách notification types trong BR không tự đủ state events cho Equipment. Cần resource/source type, full snapshot/version, recipient resolution và department snapshot theo từng contract. SchemaVersion khác AggregateVersion. Outbox/Inbox đã mapping; atomic write, lease/retry/backoff, duplicate/out-of-order/ACK/DLQ cần implementation. [Messaging guide](development/MESSAGING.md).

## 8. Swagger và kiểm thử

| Owner | Swagger UI | OpenAPI JSON |
|---|---|---|
| Identity | http://localhost:5101/swagger/index.html | http://localhost:5101/swagger/v1/swagger.json |
| Equipment | http://localhost:5102/swagger/index.html | http://localhost:5102/swagger/v1/swagger.json |
| Maintenance | http://localhost:5104/swagger/index.html | http://localhost:5104/swagger/v1/swagger.json |
| Incident | http://localhost:5105/swagger/index.html | http://localhost:5105/swagger/v1/swagger.json |
| Notification | http://localhost:5106/swagger/index.html | http://localhost:5106/swagger/v1/swagger.json |

Khởi động process theo [Getting Started](development/GETTING-STARTED.md), dùng Development profile:

```sh
curl -i http://localhost:5102/health/live
curl -i http://localhost:5102/api/equipment
curl http://localhost:5100/equipment/health/live
curl http://localhost:5105/api/diagnostics/equipment-grpc
grpcurl -plaintext -import-path src/Contracts/MedEquipSLA.Contracts.Grpc/Protos -proto equipment.proto -d '{}' localhost:5103 medequip.equipment.v1.EquipmentLookup/GetServiceInfo
```

Kỳ vọng: 200, 401, 200, diagnostic 200 có false, gRPC service/phase. Chưa có test login/refresh/role/scope thành công. Khi triển khai Planned, thêm tests JWT hết hạn/revoked version, scope list/detail, trạng thái sai, validation, concurrency, gRPC failure và duplicate event. Không tạo default key/user để vượt auth.
