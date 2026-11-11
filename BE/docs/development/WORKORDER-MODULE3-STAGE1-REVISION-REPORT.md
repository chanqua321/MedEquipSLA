# Module 3 — Stage 1 Revision Report: Retire và D3 Pending

Ngày 05/11/2026. Nguồn: REVIEW REVISION — MODULE 3 STAGE 1 của chủ dự án.
**Phạm vi lượt này chỉ tài liệu.** Stage 0 đã được chấp thuận, không audit lại toàn bộ source hoặc database.

## 1. Vấn đề phát hiện và kết luận revision

Thiết kế cũ chỉ kiểm registration/projection trong Equipment. Pending được lưu ở Maintenance trước Register có thể không hiện diện ở Equipment, nên một Retire chỉ kiểm local DB có thể commit sai thời điểm. Kết luận review thiết kế cũ tại Stage 1 cần được điều chỉnh: khóa Equipment là cần thiết cho Register/Retire, nhưng chưa đủ để bảo vệ Pending ở source.

Bản sửa không dùng query “không thấy Pending” một lần. Đề xuất:
1. Equipment commit RetirementIntent trước, đóng mọi registration mới.
2. Maintenance và Incident đóng durable admission gate tại source, serialize với mọi writer tạo Pending/blocker.
3. Source chỉ phát proof Sealed sau khi kiểm tra không còn blocker trong transaction dưới gate.
4. Equipment chỉ final Retire khi có đủ proof đúng intent/epoch/coverage và local registrations/projection/legacy checks đều đạt.

Lập luận an toàn ở mức thiết kế phụ thuộc coverage đầy đủ của mọi writer, proof bền và gates không tự mở. Với source hiện tại chưa có gate, **chưa bảo đảm invariant trong runtime**. Nếu chỉ triển khai WorkOrder mà không bao phủ Schedule/Incident, final Retire phải giữ disabled/blocked.

Đã cập nhật [Stage 1 design](WORKORDER-MODULE3-STAGE1-DESIGN.md) thay thế phương án Retire cũ. [Stage 0 audit](WORKORDER-MODULE3-STAGE0-AUDIT.md) và STAGE1-REPORT cũ được giữ làm lịch sử; các nhận định review Retire trước đây không được dùng thay bằng chứng của revision này.

## 2. State machine mới

| Owner | State/phase | Ý nghĩa |
|---|---|---|
| Equipment intent | Coordinating | Đóng registration mới, chờ source freeze/readiness và giải quyết blockers |
| Equipment intent | Ready | Đủ Sealed proof, vẫn chưa Retired; còn final local checks |
| Equipment intent | Cancelling | Admin hủy bền; final Retire bị cấm; chờ source tombstone/CancelAck |
| Equipment intent | Cancelled | Đủ CancelAck, mở admission; giữ history/epoch |
| Equipment intent | Completed | Đã Retire trong E final transaction, terminal |
| Source gate | Open | Pending mới được tạo qua gate |
| Source gate | Frozen | Cấm Pending/blocker mới, giữ và giải quyết operation cũ theo luật hiện hành |
| Source gate | Sealed | Zero source blockers bền + cam kết không tạo blocker, đúng intent/epoch |
| Source gate | Open + cancel tombstone | Hủy đúng intent đã bền; delayed Prepare cũ bị chặn |
| Source gate | Retired | Source nhận Equipment Completed, không mở lại |

Blocked/Unknown/NeedsIntervention là thông tin tiến độ, không tự Cancelled/Aborted. Không có TTL mở gate. RetirementEpoch, source gate version, operation FencingToken và SourceVersion có semantics riêng.

Intent không thay đổi Equipment.OperationalStatus. Broken không thành Operational khi start/cancel/restart intent.

## 3. RPC/contract cần bổ sung

Giữ bốn RPC WO RegisterWorkOrder, GetWorkOrderRegistrationByOperation, ConfirmWorkOrderCommitted, VerifyAndAbortWorkOrderRegistration; thêm gate check cho insert registration mới và cùng transition confirm với event consumer.

Bổ sung Equipment gRPC:
- ListRetirementIntents: bounded/keyset discovery theo source/coverage.
- GetRetirementIntent: exact binding, truy vấn cả terminal để phục hồi.
- ReportRetirementSourceState: Frozen/Blocked/Sealed + durable proof/version, không phải client-supplied count=0.
- AcknowledgeRetirementCancellation: xác nhận source cancel tombstone và unfreeze đúng intent.

Maintenance/Incident worker poll/report theo chiều source→Equipment hiện có trong C1. Không cần thêm Equipment→Maintenance connection hoặc network call trong Equipment SQL transaction.

Credential scope:
- Maintenance chỉ báo coverage WO/Schedule của Maintenance.
- Incident chỉ báo coverage Incident.
- Source identity không được tự create/Continue/Cancel intent hoặc Retire.
- Admin JWT không thay service credential/proof.
- Intent required-source set do Equipment cấu hình theo protocol, lưu bất biến; không bỏ qua nguồn không có registration.

REST đề xuất: POST retire trả 202 khi tạo intent; GET cho biết progress; chỉ Completed mới được báo Retired thành công. Continue/Cancel intent phải Admin + reason + RowVersion + idempotency. Quyền này cần chủ dự án duyệt trước code.

## 4. Transaction boundaries

| Boundary | Local transaction và durable result | Network |
|---|---|---|
| E1 | Equipment tạo Coordinating intent/epoch + Audit, đóng admission | Trả 202 sau commit |
| S1 | Source khóa gate và ghi Frozen + Audit | Get/List Equipment trước TX; report sau TX |
| S2 | Source kiểm blockers dưới gate, ghi Sealed/proof + Audit | Gửi proof sau commit |
| E2 | Equipment xác thực binding và lưu receipt | Chỉ local E transaction trong RPC nhận report |
| E3 | Equipment final-check intent/proofs/local blockers rồi Retire + Completed + Audit/history | Không gọi source/Redis trong TX |
| E4 | Equipment ghi Admin Cancel thành Cancelling bền, vô hiệu final Retire | Source chỉ xử lý quyết định đã commit |
| S3 | Source ghi terminal cancel tombstone + mở source gate atomically | CancelAck sau commit |
| E5 | Đủ mọi CancelAck, Equipment ghi Cancelled và mở admission | Không suy ra Ack từ timeout |

Pending có thể xuất hiện giữa E1 và S1, nhưng S1/S2 phải capture nó vì Pending writer giữ cùng source gate lock đến commit. Sau S2 không được có Pending mới. Readiness proof bền vẫn hợp lệ khi source mất kết nối vì gate không tự mở.

Không dùng distributed transaction. Không giữ Equipment SQL transaction khi gọi Maintenance/Incident; bản đề xuất còn giữ chiều gọi ngược lại để phù hợp C1.

## 5. Lock order

Source: Gate(EquipmentId) → Schedule → Occurrence → Operation; Incident thay bằng aggregate liên quan. Claim-only transaction giữ operation rồi kết thúc, không đi tiếp lấy gate trong cùng TX. Freeze/seal và mọi path tạo blocker dùng chung gate; bulk khóa EquipmentId theo thứ tự tăng.

Equipment: EquipmentAsset → RetirementIntent → source receipts theo SourceService → registrations/projection/history. Final Retire, Cancel intent và Register đều khóa Equipment trước. Unique epoch và active intent được cấp dưới lock này.

Row thiếu vẫn cần range/application lock hoặc cách tạo gate row atomic tương đương, không coi “không có row” là không cần serialize. SQL snapshot read không thay admission barrier.

## 6. Concurrency matrix A–H

| ID | DB cuối/ổn định khi đang chờ | Recovery và điều kiện an toàn |
|---|---|---|
| A | M Pending, E intent Coordinating, có thể R absent; source Frozen blocked | Pending được xét trong gate scan. Không Sealed, không Retired; xử lý operation hợp lệ hoặc Admin hủy intent |
| B | E intent trước Pending; Pending commit trước source freeze thì bị capture, freeze thắng thì Pending không được insert | Không cần giả định source nhận intent ngay; seal chỉ sau barrier |
| C | Register thắng: R Pending; intent thắng: Register mới bị chặn, M Pending giữ nguyên | Final Retire không thắng khi source/blocker chưa giải quyết |
| D | Caller không rõ RPC outcome; M Pending, R absent hoặc Pending | Query cùng op, Audit Unknown, cả hai khả năng vẫn chặn Retire |
| E | E R Pending/Active, M Pending hoặc Committed+WO+Outbox | Pending và WO mở đều là blocker; recovery không tạo operation/WO mới |
| F | M CancelRequested, E R absent/Pending/Aborted | Không release theo timeout; chỉ M Aborted sau tombstone bền mới hết blocker operation |
| G | Intent/gates/receipts/epochs còn SQL sau restart | Replay cùng proof/intent; Sealed giữ đóng ngay cả khi E Completed chưa báo về source |
| H | Query/report timeout: giữ state durable cuối, không có proof giả | 202/Unknown cho kết quả chưa xác định, bounded retries/intervention; exact GET xác minh nếu E đã commit |

Thiết kế đầy đủ thêm Cancel-vs-final hai thứ tự, late Prepare/Cancel epoch cũ, source unfreeze trước Ack mất, missing coverage, Inactive trước/sau generation. Không cho Ready proof cũ của intent đã hủy dùng lại cho intent mới.

## 7. Failure recovery và state mapping WO

State mapping chi tiết ở §7 design, bao phủ:
- Pending source đã bền nhưng chưa có registration.
- Remote commit nhưng response timeout/local rollback.
- Local Committed + WO.EquipmentUseRegistrationId + WorkOrderCreated Outbox cùng TX.
- Confirm RPC trước event và event trước RPC.
- Inbox duplicate, stale version/fence, tombstone conflict.
- CancelRequested/remote Aborted/local chưa xác nhận và local Aborted sau đó.

WO registration field không được ghi Guid.Empty để giả một registration khi chưa có WO. Confirm RPC và event dùng cùng verification/transition dưới Equipment lock. Consumer vẫn lưu Inbox nếu RPC đã confirm, rồi ACK sau commit; RPC không giả Inbox hoặc ACK.
Same binding/version là no-op; không hạ SourceVersion/fence. Released chỉ historical no-op khi chứng minh đúng committed proof, không hồi sinh. Aborted nhận Created là lỗi đối soát/DLQ, không revive.

Final Retire source proof không bỏ qua local R Pending/Active hoặc blocking projection. Pending Schedule/Incident cũng cần coverage; Schedule Inactive vẫn không chứng minh release.
Retire Cancel không Cancel operation. Option A WO hiện hữu không tự mở quyền Cancel Incident/Schedule.

## 8. Audit Retry và lịch sử

LastError chỉ là current diagnostic. AuditEntries phải lưu:
- Mỗi auto attempt Started và Result với AttemptId, phase Query/Register/Abort, fence/epoch, actor service, thời gian, outcome và retry time.
- Crash thiếu Result được recovery ghi Unknown, không coi RPC chắc chắn chưa chạy.
- Admin Retry có CommandId/actor/reason/version; reset retry budget không xóa cumulative history hoặc hạ fence.
- Cancel request lưu cùng TX intent bền; Abort result chỉ Confirmed khi remote tombstone được xác minh và local Aborted commit.
- Timeout/unknown từng lần, stale response bị bỏ qua có Audit riêng nhưng không sửa metadata mới.
- Retirement request/freeze/seal/query/continue/cancel/complete cùng local mutation tương ứng.

Idempotent Audit dùng ID/unique outcome binding phù hợp để duplicate command/result không sinh hành động nghiệp vụ lặp. Audit worker không dùng user GUID giả; không ghi secret. Query-only attempts cũng có lịch sử.

## 9. Migration changes dự kiến

| Owner | Dự kiến |
|---|---|
| Equipment | Mở rộng registration hash/payload/snapshot/fence/tombstone; thêm RetirementIntents và RetirementSourceReceipts |
| Maintenance | Operation EquipmentId/immutable payload/retry/lease/fence/attempt linkage; thêm source retirement gate, bao phủ cả Schedule |
| Incident | Thêm source retirement gate; kiểm tra/hoàn thiện operation equipment linkage để source readiness đúng; cần duyệt phạm vi trước implementation |

Bảng gate/receipt mới có lý do invariant cụ thể: serialize source Pending creation và giữ authoritative multi-source proof; không thay bằng ReconciliationCheckpoint snapshot cũ.

Không sửa InitialCreate, không reset hoặc fabricated backfill. Giữ NULL/default tương thích legacy; legacy chưa rõ không được tự seal. Down guard khi **bất kỳ protocol data mới** còn tồn tại, kể cả terminal tombstone/Cancelled/Completed, không chỉ active rows. Không xóa data để qua guard. Up/Down/reapply và guard phải test trên SQL Server.

Tình trạng database giữ đúng lịch sử Stage 0:
- Database ứng dụng: chỉ apply InitialCreate ở cả năm owner.
- Source/fixture: đã có AddIncidentOperationPhoto và AddOccurrenceReminderWindow; fixture baseline từng chạy migrations mới.
- Revision này không truy vấn lại database, không apply migration và không kết luận app đã upgrade.

## 10. Test cases bổ sung

§13 design liệt kê RC-A..Q, MAP, AUD, MIG và regression. Trọng tâm mới:
1. Pending không registration trước intent.
2. Intent-before-Pending cả hai thứ tự source gate lock.
3. Register-vs-intent cả hai thứ tự Equipment lock.
4. Missing/delayed/duplicate/wrong-epoch proof và thiếu một source.
5. Không tạo Pending/reactivate/import bypass sau Sealed, gồm kiểm inventory mọi writer/replica.
6. Restart từng local boundary, timeout từng RPC/report/Ack.
7. Cancel-vs-final, delayed Prepare sau cancel, delayed Cancel sau epoch mới.
8. Source gate mở nhưng Ack mất và Pending mới trong khoảng E gate còn đóng.
9. Schedule/Incident Pending và Schedule Inactive R Pending/Active.
10. RPC/event confirm ordering, atomic Inbox, duplicate/tombstone behavior.
11. Audit retry đầy đủ, crash missing result và Admin budget reset không mất history.
12. Migration legacy upgrade/rollback/reapply và guard cả terminal protocol data.
13. Regression Module 1/2/Gateway/Identity/Redis thật sau implementation.

**Tất cả runtime tests mới: NOT TESTED.** Không chạy build/test hoặc SQL trong lượt chỉ sửa tài liệu; không dùng lại 61/61 baseline để đánh dấu revision runtime PASS.

## 11. Những quyết định nghiệp vụ/phạm vi cần chủ dự án duyệt

- R1: Intent và source freeze chặn tiếp nhận Pending/registration mới của cả ba resource, kể cả reactivation tạo blocker; không tự hết hạn.
- R2: Admin Continue/Cancel RetirementIntent với reason/version; Cancel chỉ hoàn tất sau source acknowledgments, độc lập Option A của operation.
- R3: Pending cũ chưa Register giữ blocked, không có ngoại lệ đăng ký mới xuyên intent; có thể hủy intent để tiếp tục, hoặc xử lý WO theo Option A. Incident/Schedule không tự có thêm quyền Cancel.
- R4: Mở rộng implementation sang admission gate của MaintenanceSchedule và Incident; không chỉ Equipment/WO. Giữ chiều kết nối gRPC nguồn→Equipment.

Nếu phạm vi này không được duyệt, thiết kế chưa thể bảo đảm invariant trên hệ thống hiện tại và final Retire phải chưa được bật. Chưa có phương án Retire completion an toàn chỉ bằng Equipment registration query hiện hữu.

## 12. File, kiểm tra và giới hạn lượt này

Chỉ cập nhật:
- docs/development/WORKORDER-MODULE3-STAGE1-DESIGN.md.
- Tạo docs/development/WORKORDER-MODULE3-STAGE1-REVISION-REPORT.md.

Đã đọc hai tài liệu được chỉ định, không audit lại toàn bộ source. Không sửa code/entity/proto, không tạo hoặc apply migration, không commit/push. Kiểm tra tài liệu ở mức cấu trúc, links và diff; kết quả không phải bằng chứng SQL/gRPC/concurrency.

Đã chạy kiểm tra tài liệu bằng script: đủ matrix A–H, các mục mapping bắt buộc, code fences cân bằng, local Markdown links tồn tại và không trailing whitespace. Kết quả đạt ở mức tài liệu; không đánh dấu runtime PASS. Hai file hiện chưa được Git track, vì vậy git diff thông thường không thể hiện nội dung của chúng; đã kiểm trực tiếp nội dung file.

Stage 2 chờ review revision và các quyết định R1–R4. Bản thiết kế giải thích cơ chế và điều kiện an toàn, không tuyên bố đã triển khai hoặc xác minh invariant trong mọi race runtime.
