# AI20.ENROLLMENT.3 — SuperAdmin Enrollment UI Design

**Type:** Design only. No production code was written or modified to produce this document.

---

## 1. Review of Current Navigation & Placement Recommendation

**File:** `abhyanvaya-ui/src/layouts/MainLayout.tsx`

The sidebar is a flat array of `menuItems`, each with a `visible(ctx)` predicate evaluated against the current user's role/permissions; only the single existing SuperAdmin-only entry follows a pure role check with no permission fallback:

```tsx
{
  text: "Organization",
  icon: <BusinessIcon />,
  path: "/admin-setup",
  visible: ({ role }) => role === "superadmin",
},
```

`OrganizationPage` (`abhyanvaya-ui/src/pages/OrganizationPage.tsx`) itself has **no tabs** — it's a single page with a 2-card grid ("Add university", "Provision new college"). It is a narrow *provisioning* utility, not a feature hub.

### Placement decision

**AI Enrollment gets its own top-level sidebar entry, not a tab inside `OrganizationPage`.** Reasoning:
- `OrganizationPage`'s scope (create universities/colleges) and AI Enrollment's scope (long-running batch jobs, progress monitoring, per-student drill-down) are different enough in shape and interaction model that cramming them into one page's tabs would make both worse — a batch progress dashboard needs polling/live-updating real estate that a "provision a college" form doesn't.
- The user's own prompt suggested exactly this choice explicitly (`"Organization > AI Enrollment"` **or** `"Administration > AI Enrollment"` as top-level placements) rather than a tab.

**Recommended menu entry:**

```tsx
{
  text: "AI Enrollment",
  icon: <FaceRetouchingNaturalIcon />,   // or PersonAddIcon / SmartToyIcon — final icon is a cosmetic choice
  path: "/ai-enrollment",
  visible: ({ role }) => role === "superadmin",
},
```

Placed immediately after the existing "Organization" entry in the sidebar array, so the two SuperAdmin-only items are adjacent — SuperAdmin users see a clearly grouped "admin-only" section at the bottom of their menu, consistent with how the menu is already ordered (feature areas first, admin-only areas last).

**Routing** (mirrors `admin-setup` exactly, per `docs/AI20_ENROLLMENT_ARCHITECTURE.md` §7):

```tsx
<Route
  path="ai-enrollment"
  element={
    <ProtectedRoute allowedRoles={["SuperAdmin"]}>
      <AiEnrollmentPage />
    </ProtectedRoute>
  }
/>
```

`AiEnrollmentPage` is itself a small router/tab-shell for the four screens below (Dashboard, Progress, Failures, Student Detail) — those are views *within* the feature, not separate top-level menu entries.

---

## 2. Student Enrollment Dashboard

Entry screen. Filter bar + summary cards + batch list.

```
┌────────────────────────────────────────────────────────────────────────────────┐
│  AI Enrollment                                                    [+ New Batch] │
├────────────────────────────────────────────────────────────────────────────────┤
│  Filters:  University [▾ All]   College [▾ All]   Academic Year [▾ 2025]  [Search]│
├────────────────────────────────────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐          │
│  │ Total     │  │ Completed │  │ Pending   │  │ Failed    │  │ Retry Req'd│          │
│  │  4,820    │  │  4,102    │  │   210     │  │   380     │  │    128     │          │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  └──────────┘          │
├────────────────────────────────────────────────────────────────────────────────┤
│  Batches                                                                          │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ College        Year   Status      Progress                    Started       │  │
│  │ ABC Engineering 2025  Running     [██████████████░░░░░] 72%    2 min ago      │  │
│  │   1053 students · Downloading 12 · Embedding 4 · Failed 8 · [View] [Cancel]    │  │
│  │ ────────────────────────────────────────────────────────────────────────────  │  │
│  │ XYZ Arts & Sci  2025  Completed   [████████████████████] 100%  1 hour ago     │  │
│  │   640 students · 612 completed · 28 failed · [View] [Bulk Retry Failed]        │  │
│  │ ────────────────────────────────────────────────────────────────────────────  │  │
│  │ ABC Engineering 2024  Cancelled   [████████░░░░░░░░░░░░] 38%    yesterday      │  │
│  │   980 students · 372 completed · [View] [Resume]                                │  │
│  └────────────────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────────────┘
```

**"+ New Batch" dialog:**

```
┌───────────────────────────────────┐
│  New Enrollment Batch                │
├───────────────────────────────────┤
│  University   [▾ Select... ]          │
│  College       [▾ Select... ]          │
│  Academic Year [ 2025 ]                │
│  Students in scope: 1,053 (preview)    │
│  ☐ Only students missing an active     │
│     embedding (recommended)             │
│                     [Cancel]  [Start]   │
└───────────────────────────────────┘
```

The "only students missing an active embedding" checkbox (default checked) prevents accidentally re-enrolling everyone every time a batch is created for a college — a SuperAdmin running enrollment monthly for new admissions should, by default, only sweep up students who don't have `StudentFaceEmbedding.IsActive = true` yet, not redo the whole college.

### Fields present, mapped to the request's explicit list

| Requested field | Where shown |
|---|---|
| University / College / Academic Year | Filter bar (list view) + New Batch dialog (creation) |
| Progress | Per-batch progress bar + percentage |
| Statistics | Summary cards row (Total/Completed/Pending/Failed/RetryRequired) |
| Failures | `FailedCount` badge per batch, deep-links to the Failure Screen (§4) |
| Retry / Cancel / Resume | Per-batch action buttons, contextual to `Status` (`Running` → Cancel; `Cancelled` → Resume; `Completed` with failures → Bulk Retry Failed) |
| Current Student / Current Stage | Shown on the **Progress Screen** (§3) for a `Running` batch, not cluttering the dashboard's list view |
| Download Speed / Embedding Speed | Progress Screen (§3) — batch-level aggregate, not meaningful at the dashboard summary level |
| Quality Score | Student Detail Screen (§5) per-student, and as a sortable column in the Failure/browse table (§4) for spotting marginal-but-passed enrollments |
| Search / Filters | Filter bar (University/College/Year) + a student-number/name search box (opens directly to that student's Detail Screen if a single match, or a filtered job list if browsing) |
| Bulk Retry / Bulk Regenerate | Batch-level button (retries all `Failed`/`RetryRequired` jobs in that batch) and Failure Screen row-selection bulk actions (§4) |

---

## 3. Progress Screen

Opened via "View" on a `Running` (or recently-active) batch. Polls the batch status endpoint (`docs/AI20_ENROLLMENT_BACKGROUND.md` §3.2) on an interval, mirroring the existing `AttendanceSessionStatus` polling pattern already used for classroom recognition progress.

```
┌────────────────────────────────────────────────────────────────────────────┐
│  ← Back        ABC Engineering · 2025                         [Cancel Batch] │
├────────────────────────────────────────────────────────────────────────────┤
│  Progress                                                                     │
│  [███████████████████████████░░░░░░░░░░░░] 72%   758 / 1,053                  │
│                                                                                │
│  Pending 210   Downloading 12   Validating 3   Embedding 4                    │
│  Completed 758   Failed 8   Retry Required 58                                  │
├────────────────────────────────────────────────────────────────────────────┤
│  Throughput                                                                    │
│    Download speed:   4.2 students/min      Embedding speed:  6.8 students/min   │
│    Estimated remaining: ~48 min                                                 │
├────────────────────────────────────────────────────────────────────────────┤
│  Currently Processing                                                          │
│  ┌───────────────────────────────────────────────────────────────────────┐   │
│  │ Student            Number          Stage         Elapsed                │   │
│  │ Aditi Sharma        105325405001    Embedding      3s                   │   │
│  │ Rahul Verma         105325405002    Downloading     1s                  │   │
│  │ Priya Nair          105325405004    Validating       2s                 │   │
│  │ Karthik Iyer        105325405007    Downloading      0s                 │   │
│  └───────────────────────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────────────────────┘
```

"Currently Processing" lists jobs whose `Status` is `Downloading`/`Validating`/`Embedding` right now (a small, indexed query per `docs/AI20_ENROLLMENT_BACKGROUND.md` §3.2) — this satisfies the "Current Student / Current Stage" requirement without needing to track a single "currently active student," since multiple jobs process concurrently (parallelism, per the background design).

---

## 4. Failure Screen

Opened via "Failed" stat/badge from either the Dashboard or Progress Screen.

```
┌────────────────────────────────────────────────────────────────────────────────┐
│  ← Back        Failed & Retry-Required — ABC Engineering · 2025                   │
├────────────────────────────────────────────────────────────────────────────────┤
│  Filter: [▾ All reasons]   Search student number/name...       [Bulk Retry Selected]│
├────────────────────────────────────────────────────────────────────────────────┤
│  ☐  Student           Number         Reason                Retries  Last Attempt  │
│  ☐  Ankit Rao          105325405010   Photo Not Found (404)    0      2 min ago     │
│  ☐  Sneha Patil        105325405012   Multiple Faces Detected  0      2 min ago     │
│  ☐  Deepak Kumar       105325405015   Blur Rejected             0      3 min ago     │
│  ☐  Meera Joshi        105325405021   Storage Upload Failed     2      1 min ago     │
│                                                              [Retry] per row ────▶  │
├────────────────────────────────────────────────────────────────────────────────┤
│  Failure breakdown:  Photo Not Found 3 · Multiple Faces 2 · Blur 1 · Storage 2      │
└────────────────────────────────────────────────────────────────────────────────┘
```

- Row-level **Retry** button re-queues that single job (sets `Status = Pending`, per the state machine in `docs/AI20_ENROLLMENT_ARCHITECTURE.md` §6).
- Checkbox selection + **Bulk Retry Selected** for retrying many at once (or "Retry All" for the whole filtered list).
- The **Reason** column filter lets a SuperAdmin isolate, e.g., every "Photo Not Found" to investigate a systemic source-data gap for a given year, separately from a handful of "Multiple Faces Detected" that likely need the source photo itself corrected before retrying will help — directly surfacing the failure taxonomy from `docs/AI20_ENROLLMENT_DATABASE.md`/`docs/AI20_PHOTO_IMPORT.md`.
- "Failure breakdown" summary strip gives an at-a-glance systemic-vs-isolated read without opening each row.

---

## 5. Student Detail Screen

Opened from Progress/Failure/Dashboard search, or directly via a student-number search. Shows the **full history** for one student across every batch they've ever been part of (per `docs/AI20_ENROLLMENT_DATABASE.md`'s `IX_StudentEnrollmentJob_Tenant_Student` index).

```
┌────────────────────────────────────────────────────────────────────────────┐
│  ← Back    Aditi Sharma · 105325405001 · ABC Engineering                     │
├────────────────────────────────────────────────────────────────────────────┤
│  ┌─────────┐   Current photo: uploaded 2025-06-12                            │
│  │  [face]   │   Active embedding: v2 · Quality: Good (0.81)                 │
│  │  photo    │   Source: exambranch.com/PHOTOS/1053/2025/105325405001.jpg    │
│  └─────────┘   [Regenerate]  [Upload Manually]                               │
├────────────────────────────────────────────────────────────────────────────┤
│  Enrollment History                                                            │
│  Batch (2025)          Status      Stage timeline                             │
│  ─────────────────────────────────────────────────────────────────────────    │
│  ABC Engineering·2025  Completed   Pending→Downloading (1s)→Downloaded(0.4s)  │
│                                    →Validating(0.2s)→Embedding(1.1s)→Completed │
│                                    Quality Score: 0.81 · Embedding v2           │
│  ─────────────────────────────────────────────────────────────────────────    │
│  ABC Engineering·2024  Failed      Pending→Downloading→Failed                  │
│                                    Reason: Photo Not Found (404)                │
│                                    [Retry this attempt]                        │
└────────────────────────────────────────────────────────────────────────────┘
```

- Displays the actual enrolled photo (via the **existing** `/media/students/{tenantId}/{studentId}/thumbnail.webp` URL — no new media endpoint needed, per `docs/AI20_ENROLLMENT_ARCHITECTURE.md` §2's deliberate reuse of the existing student-photo storage key).
- "Regenerate" triggers a single-student re-enrollment (creates a one-student ad-hoc batch, or attaches a new job to an existing open batch — an implementation detail deferred to build time, not a UX difference visible here).
- "Upload Manually" deep-links to the **existing** manual student-photo-upload flow (`StudentPhotoService`/`StudentsPage`) — enrollment does not reinvent manual upload, it only automates the bulk case.
- Per-attempt stage timeline directly visualizes `StudentEnrollmentJob`'s stage timestamp columns (`docs/AI20_ENROLLMENT_DATABASE.md` §3.2) — this is a UI rendering of existing data, not a new tracking mechanism.

---

## 6. Cross-Screen Consistency Notes

- All four screens are reachable only through the SuperAdmin-only `/ai-enrollment` route tree — no screen is independently linkable from a non-SuperAdmin context.
- Polling cadence for Progress Screen should follow the same interval already used for `AttendanceSessionStatus` polling elsewhere in the app (consistency of "how fresh is live data" expectations across the product) rather than introducing a different cadence convention just for this feature.
- Icons/colors for `RecognitionQueueStatus`-style stage badges should reuse the same visual language (chip colors for Pending/Processing/Completed/Failed) already established by `AIStatusChip` (`abhyanvaya-ui/src/components/common/AIStatusChip.tsx`) for classroom recognition status, rather than inventing a new color system — same underlying concept (a background AI job's lifecycle), same visual vocabulary.

---

## Constraints Confirmed

No React component, route, or page was created to produce this document. All wireframes above are ASCII mockups only, not implemented UI.
