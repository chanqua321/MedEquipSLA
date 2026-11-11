# Bảng kỹ thuật — SQL Server Code First đã duyệt

![Định nghĩa các bảng kỹ thuật; mỗi service giữ bản riêng](./images/TECHNICAL-TABLES-ERD.png)

Mỗi bảng được tạo trong database của service sở hữu. Không có database shared/common. Không tính các bảng kỹ thuật là core CRUD entity của đề PRN232.

| Service | Bảng kỹ thuật owner-local đã mapping/tạo |
|---|---|
| Identity | AuditEntries, IdempotencyRequests, OutboxMessages |
| Equipment | AuditEntries, IdempotencyRequests, OutboxMessages, InboxMessages |
| Maintenance | AuditEntries, IdempotencyRequests, OutboxMessages, InboxMessages |
| Incident | AuditEntries, IdempotencyRequests, OutboxMessages, InboxMessages |
| Notification | AuditEntries, IdempotencyRequests, InboxMessages |

Identity phát email-ticket reference và UserAccessChanged qua Outbox; Maintenance/Incident nhận thay đổi quyền qua Inbox, gỡ phân công theo version và current lookup. Notification vẫn không có Outbox vì không publish integration event. Cập nhật 07/11/2026, additive technical migrations; không sửa InitialCreate.

## AuditEntries

Audit append-only từng database, không gom thành DB dùng chung.

| Cột | SQL type | Khóa | NULL |
|---|---|---|---|
| Id | uniqueidentifier | PK | Không |
| ActorUserId | uniqueidentifier | REF | Có |
| ActorService | nvarchar(50) | — | Có |
| Action | nvarchar(100) | — | Không |
| EntityType | nvarchar(100) | — | Không |
| EntityId | nvarchar(128) | — | Không |
| OccurredAtUtc | datetimeoffset | — | Không |
| CorrelationId | nvarchar(100) | — | Không |
| ChangesJson | nvarchar(max) | — | Có |

- Index(EntityType,EntityId,OccurredAtUtc). ChangesJson IS NULL OR ISJSON=1; không chứa secret. Cùng transaction dữ liệu.

## IdempotencyRequests

Chống request/job lặp và tombstone abort cho phối hợp vòng đời.

| Cột | SQL type | Khóa | NULL |
|---|---|---|---|
| OperationId | uniqueidentifier | PK | Không |
| ActorScope | nvarchar(128) | UK | Không |
| OperationName | nvarchar(80) | UK | Không |
| IdempotencyKey | nvarchar(128) | UK | Không |
| RequestHash | binary(32) | — | Không |
| ResourceId | uniqueidentifier | REF | Có |
| State | nvarchar(20) | — | Không |
| ResponseStatusCode | int | — | Có |
| ResponseJson | nvarchar(max) | — | Có |
| CreatedAtUtc | datetimeoffset | — | Không |
| ExpiresAtUtc | datetimeoffset | — | Không |
| RowVersion | rowversion | — | Không |

- Unique(ActorScope,OperationName,IdempotencyKey); State Pending/Committed/Aborted. ActorScope hỗ trợ user hoặc worker.
- Không xóa Pending hoặc abort tombstone còn có registration/replay chưa đối soát. Sau Aborted, source không được commit muộn.

## OutboxMessages

Sự kiện đã commit, dispatcher publish ít nhất một lần.

| Cột | SQL type | Khóa | NULL |
|---|---|---|---|
| EventId | uniqueidentifier | PK | Không |
| EventType | nvarchar(150) | — | Không |
| SchemaVersion | int | — | Không |
| AggregateType | nvarchar(100) | — | Không |
| AggregateId | uniqueidentifier | — | Không |
| AggregateVersion | bigint | — | Không |
| OccurredAtUtc | datetimeoffset | — | Không |
| CorrelationId | nvarchar(100) | — | Không |
| PayloadJson | nvarchar(max) | — | Không |
| Status | nvarchar(20) | — | Không |
| AttemptCount | int | — | Không |
| NextAttemptAtUtc | datetimeoffset | — | Có |
| LeaseOwner | nvarchar(100) | — | Có |
| LeaseUntilUtc | datetimeoffset | — | Có |
| PublishedAtUtc | datetimeoffset | — | Có |
| LastError | nvarchar(2000) | — | Có |

- ISJSON(PayloadJson)=1; index(Status,NextAttemptAtUtc,OccurredAtUtc). EventId không đổi khi retry.
- Không FK từ AggregateId tới nhiều loại bảng; aggregate + Outbox cùng transaction local.

## InboxMessages

Dedupe consumer cùng transaction local với dữ liệu đã nhận.

| Cột | SQL type | Khóa | NULL |
|---|---|---|---|
| ConsumerName | nvarchar(100) | PK | Không |
| EventId | uniqueidentifier | PK | Không |
| EventType | nvarchar(150) | — | Không |
| SchemaVersion | int | — | Không |
| ProcessedAtUtc | datetimeoffset | — | Không |

- PK(ConsumerName,EventId). Ghi sau xử lý trong cùng transaction, ACK broker sau commit. Không tạo FK sang Outbox ở DB khác.

## Hạ tầng EF

## Source operation linkage trong Maintenance và Incident

Hai owner này bổ sung EquipmentId uniqueidentifier NULL và RequestPayloadJson nvarchar(max) NULL vào IdempotencyRequests qua AddSourceRetirementGate. JSON có constraint ISJSON; index EquipmentId, State, OperationName phục vụ blocker scan. Legacy NULL giữ nguyên và được coi unknown khi chưa xác minh; không suy ra ownership từ hash. Equipment, Identity và Notification giữ technical shape cũ.

EquipmentRetirementGates là bảng local của từng source: EquipmentId PK logical reference, CurrentIntentId, HighestRetirementEpoch, Phase, SourceGateVersion, ReadyProofId, CancelAcknowledgementId, CreatedAtUtc, UpdatedAtUtc, RowVersion. Không FK xuyên service. Phase Open/Frozen/Sealed/Retired, cancellation tombstone và proof có check constraints; SourceGateVersion phân biệt quyết định, RowVersion chống stale SQL writes.

Query, retry action, unknown result và NeedsIntervention được ghi thành từng AuditEntry append-only theo service actor, correlation, intent và retry series. Không dùng LastError hoặc TTL để thay lịch sử hay tự release.

Mỗi database có `__EFMigrationsHistory` do EF Core quản lý. Không vẽ nó như business entity. Không dùng EnsureCreated cùng migrations. `RowVersion` là binary concurrency token SQL Server tự sinh, khác `AggregateVersion` tăng số nguyên cho event ordering.
