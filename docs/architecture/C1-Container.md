# C1 - Microservices Containers - MedEquipSLA

Ngày cập nhật: 23/10/2026. Kiến trúc đích và trạng thái scaffold/Code First. [Tổng quan](../../README.md).

![C1 - Microservices và database riêng](./images/C1-Container.png)

## Database ownership

| Service | Database sở hữu | Công nghệ |
|---|---|---|
| Identity | MedEquip_Identity | ASP.NET Core 10 / Identity / EF Core 10 Code First |
| Equipment | MedEquip_Equipment | ASP.NET Core 10 / REST + gRPC / EF Core 10 Code First |
| Maintenance & Calibration | MedEquip_Maintenance | ASP.NET Core 10 / workers / EF Core 10 Code First |
| Incident & SLA | MedEquip_Incident | ASP.NET Core 10 / workers / EF Core 10 Code First |
| Notification | MedEquip_Notification | ASP.NET Core 10 / SignalR / EF Core 10 Code First |

Tất cả là SQL Server databases riêng. Baseline nhóm dùng Docker Compose với SQL Server 2022 Developer; không phụ thuộc LocalDB cá nhân. Mỗi service có connection string, runtime user, DbContext và migrations riêng. Không truy cập database service khác. Gateway không có database nghiệp vụ.

## Mermaid source

```mermaid
flowchart LR
    admin[Clinic Administrator]
    tech[Biomedical Technician]
    staff[Department Staff]
    subgraph MedEquipSLA
        web[Web - React / TypeScript]
        gateway[Gateway - YARP / .NET 10]
        identity[Identity - JWT / Identity]
        equipment[Equipment - REST + gRPC]
        maintenance[Maintenance - REST + workers]
        incident[Incident - REST + SLA workers]
        notify[Notification - REST + workers + SignalR]
        identityDb[(MedEquip_Identity)]
        equipmentDb[(MedEquip_Equipment)]
        maintenanceDb[(MedEquip_Maintenance)]
        incidentDb[(MedEquip_Incident)]
        notificationDb[(MedEquip_Notification)]
        broker[Redis Streams - consumer groups / persistence]
        web -->|HTTPS REST / SignalR| gateway
        gateway -->|HTTP REST| identity
        gateway -->|HTTP REST| equipment
        gateway -->|HTTP REST| maintenance
        gateway -->|HTTP REST| incident
        gateway -->|HTTP REST / SignalR| notify
        identity -->|EF Core / SQL TDS| identityDb
        equipment -->|EF Core / SQL TDS| equipmentDb
        maintenance -->|EF Core / SQL TDS| maintenanceDb
        incident -->|EF Core / SQL TDS| incidentDb
        notify -->|EF Core / SQL TDS| notificationDb
        maintenance -->|gRPC HTTP2| equipment
        incident -->|gRPC HTTP2| equipment
        identity -->|gRPC department lookup| equipment
        maintenance -->|gRPC user lookup| identity
        incident -->|gRPC user lookup| identity
        notify -->|gRPC recipient lookup| identity
        equipment -.->|Outbox / RESP| broker
        maintenance -.->|Outbox / RESP| broker
        incident -.->|Outbox / RESP| broker
        broker -.->|Consumer group / Inbox| equipment
        broker -.->|Consumer group / Inbox| notify
    end
    email[Email System - External SMTP]
    admin -->|HTTPS| web
    tech -->|HTTPS| web
    staff -->|HTTPS| web
    notify -->|SMTP| email
```

Mermaid thể hiện đầy đủ gRPC internal lookups. PNG là tổng quan các luồng REST/gRPC/SQL/event chính; gRPC internal lookups được ghi chú để giữ ảnh dễ đọc. SignalR do Web khởi tạo qua Gateway; server push dùng cùng kết nối hai chiều.

## Architectural rules

- Gateway proxy HTTP; nghiệp vụ/CQRS nằm trong từng service. CQRS dùng cùng database cho đọc/ghi trong service.
- Equipment gRPC chạy độc lập với REST API gọi nó ở Maintenance/Incident; REST và gRPC của Equipment có thể host cùng process với endpoint riêng.
- Domain -> Outbox -> Redis Streams -> Inbox -> consumer data. ACK sau commit, bounded retry và dead-letter.
- Equipment sở hữu trạng thái thiết bị; event Incident/Maintenance cập nhật projection cục bộ có aggregate version.
- Notification lưu thông báo bền; SignalR chỉ báo có dữ liệu mới và REST phục hồi khi reconnect.
- Workers nằm trong service sở hữu dữ liệu ở cấu hình nhỏ; JWT và quyền database được kiểm tra tại từng service.

## Trạng thái scaffold và contract

Đây là kiến trúc đích, không phải mọi cạnh đã implement. REST Gateway và Equipment gRPC transport probe đã có source; authentication freshness, lifecycle registration/reconciliation, workers và notifications còn pending. [Compliance Matrix](../assignment-compliance/PRN232-CHECKLIST.md) và [review BR](../business/REVIEW-v1.0.md).

Incident/Maintenance/Equipment/Notification sẽ cần Identity gRPC authorization checks theo BR-AUTH-12. GetServiceInfo là probe development, không chứng minh lookup hoặc phân quyền nghiệp vụ đã hoàn thành.

## Database design proposal

[ERD v0.1 đã được duyệt](../database/ERD-REVIEW.md). Đã sinh/apply Code First migrations cho 5 database và kiểm chứng schema trên SQL Server LocalDB, compatibility 160; baseline Compose của nhóm vẫn là SQL Server 2022. Xem [database verification](../database/VERIFICATION.md). Các protocol/nghiệp vụ mô tả trong C1 vẫn cần được triển khai tại Application/Domain.
