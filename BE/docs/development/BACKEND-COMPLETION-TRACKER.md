# Backend — bàn giao kiểm tra

Cập nhật 07/11/2026. Phần Backend đã triển khai và qua các kiểm tra dưới đây trên checkout duy nhất D:/PRN232/ASS/MedEquipSLA. Chưa nghiệm thu FE hoặc Final Assignment; không commit/push và không apply migration vào application databases trong lượt này.

| Kiểm tra thực tế | Kết quả |
|---|---|
| Release solution | PASS, 0 warnings, 0 errors |
| SQL Server 2022 CU27 + Redis 8.2.10 regression | **157/157 PASS, 0 SKIP**, backend-final-regression-20261107.trx |
| Database ownership/migrations | 54 app tables, 649 columns, 31 local NoAction FK; 20 migrations, exact EF/SQL schema/index/check/RowVersion, Up/Down/reapply và history guards |
| Runtime credential isolation | 5 owner login tests PASS: own data R/W, DDL/cross-database denied |
| Identity | Bootstrap singleton concurrency, Admin TOTP, optional MFA/recovery, invitation/Excel/activation/suspension/resend/reset, rotation/reuse/revoke, version freshness |
| Equipment | Department/Category CRUD logical archive, asset/profile/transfer/history, independent gRPC, source-bound auth, registration/retirement/fencing |
| Maintenance | Schedule/Occurrence/Reminder, concurrency/catch-up, due=scheduled+CompletionWindowMinutes, durable D3 WO generation, lifecycle/photo/calibration/review, permanent end |
| Incident | Staff scope, D3 create/idempotency, assignment/start/report/photo/accept/reject, attempts/history, SLA scanner/final outcome |
| Notification | Redis Inbox/retry/duplicate, own feed/preferences, SMTP capture ACK/retry/unknown/admin retry, authenticated SignalR and per-session/version revocation |
| D3 recovery | Source gates, poll/report, controlled Pending cancel, tombstone/fence, WO retry and full source snapshot/checkpoint; unknown/absence never releases |
| Docker build | PASS all 6 backend images, Linux locked restore/publish |
| Docker HTTP E2E | PASS exit 0: 5 APIs + Gateway on owned Compose network with SQL2022/Redis; all Swagger, JWT/MFA, invitation/import, catalog CRUD, Incident review, Schedule restart, Scheduler→D3→WO→Calibration/photo→Closed, reports/CSV/RBAC/session revoke |
| OpenAPI | Captured from live latest Docker hosts; public login and internal credential metadata asserted |
| ERD | Editable draw.io regenerated: 54 app + 5 history tables, 649 app columns, 31 independent FK connectors; geometry validation, preview inspected |

TRX/logs are ignored local artifacts, not GitHub CI evidence. Integration tests use only fixture-owned MedEquip_<Owner>_Test_<GUID> catalogs. Docker E2E creates ephemeral API containers/catalogs and cleans them after verification; it does not leave Swagger running. SMTP tests use loopback capture, never external mail.

## Scope still outside this handoff

- Application DBs remain InitialCreate. Operator must review/backup and apply all migrations before starting latest binaries against them; tests already applied all 20 migrations to isolated SQL Server catalogs.
- No FE changes/review in this turn, no public deployment, no Git commit/push/PR or fresh CI claim. Current branch feature/database-code-first; existing changes by others preserved.
- External SMTP credentials/TLS/domain, production ingress/TLS/service-key rotation and load/soak testing are environment work, not verified by local tests. Compose runs one Notification replica; SignalR multi-replica backplane is not implemented.
- Pending Incident/Schedule create resumes with original request/current session or controlled Admin cancel; no worker impersonation/automatic create without user authorization. Snapshot Completed is inventory progress, not retirement eligibility.
- Formal course presentation/demo and team contribution evidence must be recorded by the team. Full-system Docker/FE requirements stay IN PROGRESS in PRN232 matrix where applicable.

[Runbook](BACKEND-RUNBOOK.md), [API](../API-DOCUMENTATION.md), [Business](../BUSINESS-DOCUMENTATION.md), [Database](../DATABASE-DOCUMENTATION.md), [D3 snapshot](D3-SNAPSHOT-RECONCILIATION.md), [PRN232 matrix](../assignment-compliance/PRN232-CHECKLIST.md).
