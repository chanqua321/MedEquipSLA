# API contracts index

[API-DOCUMENTATION](../API-DOCUMENTATION.md) là nơi tra cứu REST đã có và contract Planned, tránh lưu hai danh sách khác nhau.

| Contract | Source / trạng thái |
|---|---|
| REST paths hiện có | Program.cs của năm service; resource handler còn skeleton |
| EquipmentLookup proto | [equipment.proto](../../src/Contracts/MedEquipSLA.Contracts.Grpc/Protos/equipment.proto): GetServiceInfo probe chạy được; GetEquipment nghiệp vụ chưa implement |
| IntegrationEvent envelope | [IntegrationEvent.cs](../../src/Contracts/MedEquipSLA.Contracts.Events/IntegrationEvent.cs): có code, Payload là JSON string |
| Business REST DTO / error codes | Đề xuất trong API Documentation, chưa C# DTO/versioned API được duyệt |
| Identity authorization / registration / reconciliation RPC | Planned; chưa có proto |
| Typed events / routing / retry policy | Planned; envelope/schema tables không chứng minh event workflow |

Mỗi PR contract cần owner, auth/scope, DTO/fields, validation, error/status, BR/AC, compatibility/version và test evidence. Không đổi tên/payload event đã có consumer mà không có migration/compatibility plan. Không ghi contract đề xuất là Implemented.
