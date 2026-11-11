# Equipment gRPC độc lập REST

Cập nhật 07/11/2026. Contract: [equipment.proto](../../src/Contracts/MedEquipSLA.Contracts.Grpc/Protos/equipment.proto). Equipment REST cổng 5102, gRPC HTTP/2 cổng 5103. Maintenance/Incident là client, không thay phần gRPC bằng REST.

| RPC | Xác thực/mục đích |
|---|---|
| GetEquipment | JWT hiện hành, scope khoa; trả thiết bị thật từ SQL |
| RegisterIncidentUse / RegisterScheduleUse | JWT hiện hành + key riêng source; operation idempotent, thiết bị chưa Retired/không intent khóa mới |
| RegisterWorkOrder / GetWorkOrderRegistrationByOperation | Maintenance worker-only key, binding/hash/fence; không dùng JWT Admin thay key |
| ConfirmWorkOrderCommitted | Cùng identity/payload/registration; idempotent với Created event |
| VerifyAndAbortWorkOrderRegistration | Durable cancel intent/fence; tombstone ngăn Register muộn |
| ListRetirementIntents / GetRetirementIntent / ReportRetirementSourceState | Key ràng buộc Maintenance hoặc Incident; source chủ động poll/report Equipment |
| AbortResourceUse / ReleaseScheduleUse | Source retirement credential, proof binding; không cho người dùng tự release |
| GetServiceInfo | Development probe; không chứng minh business RPC đạt |

## Kiểm thử riêng

```powershell
dotnet test tests/MedEquipSLA.Database.IntegrationTests -c Release --filter 'FullyQualifiedName~GrpcTests|FullyQualifiedName~EquipmentRetirementProtocolTests|FullyQualifiedName~RetirementSourceCoordinatorTests'
```

Cần SQLSERVER_TEST_CONNECTION cho database test riêng; thiếu môi trường là SKIP/BLOCKED, không PASS. Tests tạo Kestrel HTTP/2, dùng generated Protobuf client, RPC thật + SQL transaction, credential sai/đúng, retired, duplicate/stale fence, mất reply, restart và source gates. Xem [test guide](../../tests/MedEquipSLA.Database.IntegrationTests/README.md).

Có thể dùng grpcurl với proto và thư mục import Google Protobuf đã cài; host hiện không bật server reflection. Ví dụ GetEquipment: đưa token vào biến GRPC_TOKEN ở terminal riêng, không ghi vào file/log/repo.

```powershell
grpcurl -plaintext -import-path src/Contracts/MedEquipSLA.Contracts.Grpc/Protos -proto equipment.proto -H "authorization: Bearer $env:GRPC_TOKEN" -d '{"equipment_id":"ID_THIET_BI"}' localhost:5103 medequip.equipment.v1.EquipmentLookup/GetEquipment
```

Chỉ môi trường development nội bộ dùng h2c. Deployment công khai cần TLS/reverse proxy phù hợp; service key không đưa FE/Swagger công khai. [Runbook](BACKEND-RUNBOOK.md), [protocol retirement](WORKORDER-MODULE3-STAGE1-DESIGN.md).
