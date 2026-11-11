# Mục lục tài liệu

Đọc [README dự án](../README.md) → [Getting Started](development/GETTING-STARTED.md) → ba tài liệu chính → workflow của service mình phụ trách. Audit: 23/10/2026.

## Ba tài liệu chính

- [BUSINESS-DOCUMENTATION](BUSINESS-DOCUMENTATION.md): actor, vòng đời, SLA, D1–D4, flow/error/acceptance criteria và quyết định còn mở.
- [API-DOCUMENTATION](API-DOCUMENTATION.md): route/response thật, auth, gRPC/events; contract Planned tách riêng.
- [DATABASE-DOCUMENTATION](DATABASE-DOCUMENTATION.md): ownership, entity/quan hệ, constraints, Code First và schema thực tế.

## Kiến trúc và database

- [C0 System Context](architecture/C0-System-Context.md), [C1 Container](architecture/C1-Container.md), [Solution Structure](architecture/SOLUTION-STRUCTURE.md).
- [Database index](database/README.md), [ERD v0.1](database/ERD-REVIEW.md), [Approval D1–D4](database/APPROVAL.md), [JSON model](database/erd-model.json).
- ERD/dictionary: [Identity](database/services/Identity.md), [Equipment](database/services/Equipment.md), [Maintenance](database/services/Maintenance.md), [Incident](database/services/Incident.md), [Notification](database/services/Notification.md).
- [Technical tables](database/TECHNICAL-TABLES.md), [Setup/migrations](database/SETUP.md), [Database verification](database/VERIFICATION.md).

## Nghiệp vụ, contracts và phát triển

- [Business Rules v1.0 gốc — Proposed](business/BUSINESS-RULES-v1.0-PROPOSED.md), [review lịch sử](business/REVIEW-v1.0.md).
- [API contracts index](api-contracts/README.md).
- [Getting Started](development/GETTING-STARTED.md), [Environment Setup](development/ENVIRONMENT-SETUP.md).
- [Development Workflow](development/DEVELOPMENT-WORKFLOW.md), [Coding Conventions](development/CODING-CONVENTIONS.md), [Git Workflow](development/GIT-WORKFLOW.md).
- [gRPC guide](development/GRPC.md), [Messaging guide](development/MESSAGING.md), [Team Responsibilities](development/TEAM-RESPONSIBILITIES.md).
- Service README: [Identity](../src/Services/Identity/README.md), [Equipment](../src/Services/Equipment/README.md), [Maintenance](../src/Services/Maintenance/README.md), [Incident](../src/Services/Incident/README.md), [Notification](../src/Services/Notification/README.md).

- [BR D1–D4 Approved addendum](business/BUSINESS-RULES-D1-D4-APPROVED.md), [database finalization audit](database/FINALIZATION-AUDIT.md), [EF integration tests](../tests/MedEquipSLA.Database.IntegrationTests/README.md).

## Học phần và evidence

- [PRN232 Final Assignment](assignment-compliance/PRN232%20-%20Final%20Assignment.pdf): nguồn yêu cầu học phần chính thức.
- [Compliance Matrix](assignment-compliance/PRN232-CHECKLIST.md).
- [Documentation verification](assignment-compliance/DOCUMENTATION-VERIFICATION.md): audit/build/test của lượt hoàn thiện này.
- [Scaffold verification](assignment-compliance/SCAFFOLD-VERIFICATION.md): snapshot foundation ban đầu, không thay thế evidence DB mới.

Proposed ≠ Approved ≠ Implemented. BR gốc vẫn Proposed, ERD/D1–D4 Approved, schema/transport scaffold Implemented trong phạm vi evidence. Kiến trúc đích không có nghĩa mọi luồng đã chạy.
