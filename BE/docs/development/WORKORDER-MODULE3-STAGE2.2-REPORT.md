# Module 3 Stage 2.2 — RegisterWorkOrder + service-only auth
Ngày 06/11/2026. Tiếp tục theo chỉ định hoàn thiện toàn bộ BE; không dừng để xin lại R1–R4.

Implemented:
- Additive protobuf RegisterWorkOrder/GetWorkOrderRegistrationByOperation. Existing field tags/RPCs unchanged.
- EquipmentService authentication scheme only for Maintenance work-order RPC policy. Separate environment Grpc__MaintenanceWorkOrderKey / MAINTENANCE_WORKORDER_KEY, no fallback to user JWT, fail closed.
- API → Application handler → IWorkOrderRegistrationStore → SQL Server, domain canonical v1 request/hash validation.
- Equipment row UPDLOCK,HOLDLOCK, operation/resource uniques; idempotent immutable snapshot replay, monotonic fence observation, Audit service actor only once per creation.
- New reservation blocked by active RetirementIntent, including legacy Incident/Schedule RPC insert paths; replay existing reservation stays available.
- Query distinguishes absent registration from missing equipment and payload/binding mismatch.
- New test project API reference is for hosting real EquipmentGrpcService with production auth/DI, not EF InMemory.
- Test host clears inherited appsettings before binding loopback random HTTP/2 port and fixture SQL connection; never starts Program auto-migration.

Verification:
- Release solution build: exit0, 0 warnings/errors.
- Real gRPC + SQL tests: 4/4 PASS, 0 skipped.
- Foundation/schema/migration/Equipment/gRPC regression: 35/35 PASS, 0 skipped, 25 seconds.
- Evidence: tests/MedEquipSLA.Database.IntegrationTests/TestResults/stage2.2-grpc.trx and stage2.2-regression.trx (gitignored).
- Initial test host failed because API project content supplied extra configured endpoints; fixed isolation by clearing configuration sources, then reran successfully.
- No new migration needed: uses Stage 2.1 schema. No application DB updates, no FE changes, no commit/push.
- Tests use owned TestDatabase catalogs; service secret generated per host, public RSA test key temp-owned and cleaned, private key in memory.

Scope still pending in later work: Confirm/Abort protocol, source fencing/reconciliation, source gates, real retirement coordinator/endpoints, full BE E2E. Stage 2.2 evidence does not claim complete D3 or backend.
