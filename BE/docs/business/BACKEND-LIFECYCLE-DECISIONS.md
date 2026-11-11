# Backend lifecycle decisions

Approved by project owner on 06/11/2026:

1. Admin may permanently end a maintenance schedule. Preserve history, cancel Planned occurrences without WorkOrder, release its registration only after no unresolved operation remains and all related WorkOrders are Closed/Cancelled. An ended schedule cannot be reactivated.
2. Admin may cancel a Pending Incident/Schedule creation operation that has not committed an aggregate. Require reason, fencing, durable Equipment abort tombstone and Audit. Timeout is still unknown, never successful abort or release.

These permissions supplement R1–R4; they do not authorize deleting history, automatically cancelling committed WorkOrders or bypassing Calibration Failed blocking.
