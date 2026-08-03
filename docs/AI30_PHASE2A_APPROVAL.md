# AI30.2A.2 — Approval Workflow

**Flow:** Draft → Coordinator Review → Academic Admin Approval → (Approved) → Publish (separate) → Archive  

**Entities:** TimetableApprovalRequest, TimetableApprovalStep, TimetableApprovalHistory  
**API:** `api/scheduling/approvals`  
**Permissions:** Scheduling.Review, Scheduling.Approve  

Decisions: Approved | Rejected | Returned. No Attendance integration.
