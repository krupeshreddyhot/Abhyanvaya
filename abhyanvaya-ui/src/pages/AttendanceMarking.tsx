import { useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate, useSearchParams } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  Chip,
  CircularProgress,
  FormControl,
  FormControlLabel,
  FormLabel,
  MenuItem,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import useMediaQuery from "@mui/material/useMediaQuery";
import { useTheme } from "@mui/material/styles";
import { AiAttendancePanel } from "../components/attendance/AiAttendancePanel";
import { CombinedSectionClassBanner } from "../components/attendance/CombinedSectionClassBanner";
import { OperationalTimetableContextPanel } from "../components/attendance/OperationalTimetableContextPanel";
import { PERIOD_OPTIONS } from "../constants/attendanceConstants";
import {
  type AttendanceContext,
  type AttendanceMethodMode,
} from "../types/attendanceContext";
import {
  editAttendance,
  getCourses,
  getGroups,
  getStudentsForMarking,
  getSubjects,
  markAttendance,
  type AttendanceStudentDto,
  type CourseDto,
  type GroupDto,
  type SubjectDto,
} from "../services/attendanceService";
import {
  AcademicContextBreadcrumb,
  AcademicHelpHint,
  academicChipSx,
  academicPageShellSx,
} from "../components/academic";
import { useAcademicUi } from "../context/AcademicUiContext";
import { listSections, type SectionDto } from "../services/sectionService";
import { filterSemestersForScope, listSemesters, type SemesterRow } from "../services/setupService";
import { listAcademicYears, resolveAttendanceSession } from "../services/schedulingService";
import {
  buildSectionListParams,
  buildAttendanceWritePayload,
  buildStudentsForMarkingParams,
  hasTimetableAcademicDrift,
  normalizeSectionIds,
  resolveAuthoritativeAcademicYear,
  resolveAttendanceMarkingMode,
  snapshotFromTimetableResolution,
  type AcademicYearAuthority,
  type AttendanceMarkingScopeMode,
  type AttendanceResolutionLike,
  type AttendanceTimetableSnapshot,
} from "../utils/attendanceMarkingScope";
import { getApiErrorMessage } from "../utils/apiErrorMessage";
import { ACADEMIC_UI_PAGE_SIZES, isAbortError, replaceAbortController } from "../utils/academicRequest";
import { describeAttendancePopulation } from "../utils/attendanceSectionBehavior";
import { buildCombinedSectionClassView } from "../utils/combinedSectionClass";
import {
  buildOperationalTimetableContextView,
  sectionOrGroupLabel,
} from "../utils/operationalTimetableContext";
import { safeMultiSelectValues, safePeriodValue, safeSelectValue } from "../utils/safeSelectValue";
import { useAuth } from "../context/AuthContext";
import {
  getCoursesCache,
  getGroupsCache,
  getSectionsCache,
  getSemestersCache,
  getSubjectsCache,
  isScopedSemesterCache,
  readPersistedSelection,
  sectionsCacheKey,
  setCoursesCache,
  setSemestersCache,
  subjectsCacheKey,
  writePersistedSelection,
} from "../utils/attendanceMarkingPersistence";

type RosterCacheEntry = {
  students: AttendanceStudentDto[];
  totalCount: number;
  fetchedAt: number;
};

/** Short-lived full-roster cache so Mark-all + Save share one paged fetch per scope. */
const rosterCache = new Map<string, RosterCacheEntry>();
const ROSTER_CACHE_TTL_MS = 30_000;

/** Stable empty list — `sectionScopeEnabled ? selected : []` must not allocate a new [] each render
 *  (that identity change re-triggers the roster effect and exceeds React's update depth). */
const EMPTY_SECTION_IDS: number[] = [];

const rosterCacheKey = (parts: {
  courseId: number;
  groupId: number;
  semesterId: number;
  subjectId: number;
  date: string;
  sectionIds: number[];
}) =>
  [
    parts.courseId,
    parts.groupId,
    parts.semesterId,
    parts.subjectId,
    parts.date,
    parts.sectionIds.slice().sort((a, b) => a - b).join(","),
  ].join(":");

const resolveMethodFromNavigation = (
  searchParams: URLSearchParams,
  locationState: { switchToManual?: boolean; attendanceMethod?: AttendanceMethodMode } | null,
  persistedMethod?: AttendanceMethodMode
): AttendanceMethodMode => {
  // Coming back from "View attendance" / "Return" after finalizing an AI photo session should land
  // on the Manual Attendance grid (to review the just-generated records), not stay on AI Photo mode.
  if (locationState?.switchToManual) return "manual";
  if (locationState?.attendanceMethod === "manual" || locationState?.attendanceMethod === "aiPhoto") {
    return locationState.attendanceMethod;
  }

  // Faculty Workspace (and other entry points) pass intent via ?ai=1 / ?ai=0.
  // Button intent must win over a previously persisted Manual/AI selection.
  const aiParam = searchParams.get("ai");
  if (aiParam === "1" || aiParam === "true") return "aiPhoto";
  if (aiParam === "0" || aiParam === "false" || aiParam === "manual") return "manual";

  return persistedMethod ?? "manual";
};

const AttendanceMarking = () => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const location = useLocation();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const academicUi = useAcademicUi();
  const { user } = useAuth();
  const authUserId = user?.userId ?? 0;
  const authTenantId = user?.tenantId ?? 0;

  // Per-user sessionStorage — never restore another faculty member's Course/Group/Subject.
  const initialSelection = readPersistedSelection(authUserId, authTenantId);
  const locationState = (location.state as {
    switchToManual?: boolean;
    attendanceMethod?: AttendanceMethodMode;
  } | null) ?? null;
  const initialMethod = resolveMethodFromNavigation(
    searchParams,
    locationState,
    initialSelection.attendanceMethod
  );

  const coursesCache = getCoursesCache();
  const semestersCache = getSemestersCache();
  const groupsCache = getGroupsCache();
  const subjectsCache = getSubjectsCache();
  const sectionsCache = getSectionsCache();

  const [courses, setCourses] = useState<CourseDto[]>(coursesCache ?? []);
  const [groups, setGroups] = useState<GroupDto[]>(() =>
    initialSelection.courseId ? (groupsCache.get(initialSelection.courseId) ?? []) : []
  );
  const [semesters, setSemesters] = useState<SemesterRow[]>(semestersCache ?? []);
  const [subjects, setSubjects] = useState<SubjectDto[]>(() =>
    initialSelection.courseId && initialSelection.groupId && initialSelection.semesterId
      ? (subjectsCache.get(
          subjectsCacheKey(initialSelection.courseId, initialSelection.groupId, initialSelection.semesterId)
        ) ?? [])
      : []
  );
  const [subjectsLoading, setSubjectsLoading] = useState(false);

  const [courseId, setCourseId] = useState(() => initialSelection.courseId ?? 0);
  const [groupId, setGroupId] = useState(() => initialSelection.groupId ?? 0);
  const [semesterId, setSemesterId] = useState(() => initialSelection.semesterId ?? 0);
  const [subjectId, setSubjectId] = useState(() => initialSelection.subjectId ?? 0);
  const [periodNumber, setPeriodNumber] = useState(() => initialSelection.periodNumber ?? 1);
  const [academicYearAuthority, setAcademicYearAuthority] = useState<AcademicYearAuthority>({
    status: "None",
    academicYearId: null,
    message: "Current academic year is not configured.",
  });
  const academicYearId = academicYearAuthority.academicYearId;
  const sectionScopeEnabled = academicYearAuthority.status === "ExactlyOne";
  const [selectedSectionIds, setSelectedSectionIds] = useState<number[]>(() =>
    normalizeSectionIds(initialSelection.selectedSectionIds),
  );
  /** Never send section filters when Academic Year authority is not ExactlyOne. */
  const effectiveSectionIds = sectionScopeEnabled ? selectedSectionIds : EMPTY_SECTION_IDS;
  const effectiveSectionIdsKey = effectiveSectionIds.join(",");
  const [sections, setSections] = useState<SectionDto[]>([]);
  const [scopeMode, setScopeMode] = useState<AttendanceMarkingScopeMode>("Manual");
  const [roomName, setRoomName] = useState<string | null>(null);
  const [resolvedSectionCodes, setResolvedSectionCodes] = useState<string[]>([]);
  const timetableSnapshotRef = useRef<AttendanceTimetableSnapshot | null>(null);
  const applyingTimetableRef = useRef(false);
  const [attendanceMethod, setAttendanceMethod] = useState<AttendanceMethodMode>(() =>
    initialMethod === "aiPhoto" || initialMethod === "manual" ? initialMethod : "manual",
  );
  /** YYYY-MM-DD in the user's local calendar (date input); never use toISOString().slice(0,10) here — that is UTC date. */
  const [date, setDate] = useState(() => {
    if (initialSelection.date) return initialSelection.date;
    const d = new Date();
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, "0");
    const day = String(d.getDate()).padStart(2, "0");
    return `${y}-${m}-${day}`;
  });

  const attendanceDateIsoUtc = () => new Date(`${date}T00:00:00`).toISOString();
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");

  const [students, setStudents] = useState<AttendanceStudentDto[]>([]);
  const [isLocked, setIsLocked] = useState(false);
  const [alreadyMarked, setAlreadyMarked] = useState(false);
  const [totalCount, setTotalCount] = useState(0);
  /** Total students in class for selected filters (ignores search) — used for save eligibility */
  const [fullClassTotal, setFullClassTotal] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const pageSize = ACADEMIC_UI_PAGE_SIZES.attendanceRosterPage;
  /** Full roster pages use API max page size for fewer round trips */
  const rosterPageSize = ACADEMIC_UI_PAGE_SIZES.attendanceRosterFetch;
  const studentsAbortRef = useRef<AbortController | null>(null);
  const rosterAbortRef = useRef<AbortController | null>(null);
  const [statusMap, setStatusMap] = useState<Record<string, number>>({});

  const [loadingMeta, setLoadingMeta] = useState(() => !(coursesCache && semestersCache));
  const [loadingStudents, setLoadingStudents] = useState(false);
  const [bulkUpdating, setBulkUpdating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<"card" | "table">(isMobile ? "card" : "table");
  const [subjectAccessHint, setSubjectAccessHint] = useState<string | null>(null);
  const [sessionResolution, setSessionResolution] = useState<AttendanceResolutionLike | null>(null);
  const [rosterCombinedMeta, setRosterCombinedMeta] = useState<{
    isCombinedClass?: boolean;
    participatingSectionIds?: number[];
    participatingSectionCodes?: string[];
    operationalClassLabel?: string | null;
  }>({});

  useEffect(() => {
    setViewMode(isMobile ? "card" : "table");
  }, [isMobile]);

  // Prompt 11B — fail-closed Academic Year authority for Section options (never guess first year).
  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const res = await listAcademicYears();
        if (cancelled) return;
        const authority = resolveAuthoritativeAcademicYear(res.data ?? []);
        setAcademicYearAuthority(authority);
        if (authority.status !== "ExactlyOne") {
          setSelectedSectionIds((prev) => (prev.length === 0 ? prev : []));
          setResolvedSectionCodes((prev) => (prev.length === 0 ? prev : []));
        }
      } catch {
        if (!cancelled) {
          setAcademicYearAuthority({
            status: "None",
            academicYearId: null,
            message: "Current academic year is not configured.",
          });
          setSelectedSectionIds((prev) => (prev.length === 0 ? prev : []));
          setResolvedSectionCodes((prev) => (prev.length === 0 ? prev : []));
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (sectionScopeEnabled) return;
    if (selectedSectionIds.length === 0 && resolvedSectionCodes.length === 0) return;
    setSelectedSectionIds((prev) => (prev.length === 0 ? prev : []));
    setResolvedSectionCodes((prev) => (prev.length === 0 ? prev : []));
  }, [sectionScopeEnabled, selectedSectionIds.length, resolvedSectionCodes.length]);

  // Optional Phase 2B resolver: pre-fill from today's timetable when available.
  // Never removes legacy Course → Group → Semester → Subject → Period workflow.
  // AI29.1D Prompt 11/11A: Timetable prefills Section(s)/Room; Manual keeps Section optional.
  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const res = await resolveAttendanceSession({ date });
        if (cancelled) return;
        setSessionResolution(res.data);
        const mode = resolveAttendanceMarkingMode(res.data);
        setScopeMode(mode);
        if (mode === "Timetable") {
          applyingTimetableRef.current = true;
          const snap = snapshotFromTimetableResolution(res.data);
          timetableSnapshotRef.current = snap;
          if (res.data.courseId) setCourseId(res.data.courseId);
          if (res.data.groupId) setGroupId(res.data.groupId);
          if (res.data.semesterId) setSemesterId(res.data.semesterId);
          if (res.data.subjectId) setSubjectId(res.data.subjectId);
          if (res.data.periodNumber) setPeriodNumber(res.data.periodNumber);
          setSelectedSectionIds(snap.sectionIds);
          setResolvedSectionCodes(snap.sectionCodes);
          setRoomName(snap.roomName);
          queueMicrotask(() => {
            applyingTimetableRef.current = false;
          });
        } else {
          // Graceful fallback — never block attendance when timetable is unavailable.
          timetableSnapshotRef.current = null;
          setResolvedSectionCodes((prev) => (prev.length === 0 ? prev : []));
          setRoomName((prev) => (prev == null ? prev : null));
        }
      } catch {
        if (!cancelled) {
          setSessionResolution(null);
          timetableSnapshotRef.current = null;
          setScopeMode("Manual");
          setResolvedSectionCodes((prev) => (prev.length === 0 ? prev : []));
          setRoomName((prev) => (prev == null ? prev : null));
        }
      }
    })();
    return () => {
      cancelled = true;
    };
    // selectedSectionIds intentionally omitted — manual Section choice updates hint separately.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [date]);

  // Prompt 11A — changing a timetable-resolved academic field drops stale Section/Room context.
  useEffect(() => {
    if (applyingTimetableRef.current) return;
    if (scopeMode !== "Timetable") return;
    if (
      !hasTimetableAcademicDrift(timetableSnapshotRef.current, {
        courseId,
        groupId,
        semesterId,
        subjectId,
        periodNumber,
      })
    ) {
      return;
    }
    timetableSnapshotRef.current = null;
    setScopeMode("Manual");
    setSelectedSectionIds((prev) => (prev.length === 0 ? prev : []));
    setResolvedSectionCodes((prev) => (prev.length === 0 ? prev : []));
    setRoomName((prev) => (prev == null ? prev : null));
  }, [scopeMode, courseId, groupId, semesterId, subjectId, periodNumber]);

  // Apply method from Faculty Workspace / deep-link (?ai=1|0) or one-shot location.state.
  // Button intent always wins over a previously persisted Manual/AI selection.
  useEffect(() => {
    const next = resolveMethodFromNavigation(searchParams, locationState, initialSelection.attendanceMethod);
    setAttendanceMethod((current) => (current === next ? current : next));

    // Consume one-time switchToManual so it does not re-apply on browser back to this entry.
    if (locationState?.switchToManual) {
      navigate(`${location.pathname}${location.search}`, { replace: true, state: null });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams, location.key]);

  // Keep ?ai= in sync when the faculty toggles method on the page.
  useEffect(() => {
    const desired = attendanceMethod === "aiPhoto" ? "1" : "0";
    if (searchParams.get("ai") === desired) return;
    const next = new URLSearchParams(searchParams);
    next.set("ai", desired);
    setSearchParams(next, { replace: true });
  }, [attendanceMethod, searchParams, setSearchParams]);

  // Remember the current selection so it survives navigating away and back (e.g. AI photo
  // attendance → recognition review → back to this page) instead of resetting every dropdown.
  // Scoped by signed-in user — never leak Course/Group/Subject to the next faculty login.
  useEffect(() => {
    if (!authUserId || !authTenantId) return;
    writePersistedSelection(authUserId, authTenantId, {
      courseId,
      groupId,
      semesterId,
      subjectId,
      periodNumber,
      attendanceMethod,
      date,
      selectedSectionIds,
    });
  }, [
    authUserId,
    authTenantId,
    courseId,
    groupId,
    semesterId,
    subjectId,
    periodNumber,
    attendanceMethod,
    date,
    selectedSectionIds,
  ]);

  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  useEffect(() => {
    const cachedCourses = getCoursesCache();
    const cachedSemesters = getSemestersCache();
    if (cachedCourses && isScopedSemesterCache(cachedSemesters)) {
      setCourses(cachedCourses);
      setSemesters(cachedSemesters);
      setLoadingMeta(false);
      return;
    }
    const loadMeta = async () => {
      setLoadingMeta(true);
      setError(null);
      try {
        // Full semester rows include courseId/groupId so the Semester dropdown can be scoped —
        // unscoped /master/semesters let faculty pick another course's "Semester III" and subjects stayed empty.
        const [cRes, sRes] = await Promise.all([getCourses(), listSemesters()]);
        setCoursesCache(cRes.data);
        setSemestersCache(sRes.data ?? []);
        setCourses(cRes.data);
        setSemesters(sRes.data ?? []);
      } catch {
        setError("Failed to load course/semester data.");
      } finally {
        setLoadingMeta(false);
      }
    };
    void loadMeta();
  }, []);

  const scopedSemesters = useMemo(
    () => filterSemestersForScope(semesters, courseId, groupId),
    [semesters, courseId, groupId],
  );

  // Drop a persisted/global semester that is not valid for the selected Course + Group.
  useEffect(() => {
    if (semesterId <= 0 || scopedSemesters.length === 0) return;
    if (scopedSemesters.some((s) => s.id === semesterId)) return;
    setSemesterId(0);
    setSubjectId(0);
    setSubjects((prev) => (prev.length === 0 ? prev : []));
  }, [semesterId, scopedSemesters]);

  useEffect(() => {
    if (!courseId) {
      setGroups((prev) => (prev.length === 0 ? prev : []));
      setGroupId((prev) => (prev === 0 ? prev : 0));
      return;
    }
    const cached = groupsCache.get(courseId);
    if (cached) {
      setGroups((prev) => (prev === cached ? prev : cached));
      return;
    }
    const loadGroups = async () => {
      try {
        const res = await getGroups(courseId);
        groupsCache.set(courseId, res.data);
        setGroups(res.data);
      } catch {
        setGroups((prev) => (prev.length === 0 ? prev : []));
      }
    };
    void loadGroups();
  }, [courseId]);

  // Subjects = Course + Group + Semester only (never Section / Academic Year). Own abort signal.
  useEffect(() => {
    if (!courseId || !groupId || !semesterId) {
      setSubjectsLoading(false);
      setSubjects((prev) => (prev.length === 0 ? prev : []));
      setSubjectId((prev) => (prev === 0 ? prev : 0));
      return;
    }

    const key = subjectsCacheKey(courseId, groupId, semesterId);
    if (subjectsCache.has(key)) {
      const cached = subjectsCache.get(key)!;
      setSubjects((prev) => (prev === cached ? prev : cached));
      setSubjectsLoading(false);
      return;
    }

    const controller = new AbortController();
    setSubjectsLoading(true);
    void getSubjects(courseId, groupId, semesterId, { signal: controller.signal })
      .then((res) => {
        if (controller.signal.aborted) return;
        const rows = res.data ?? [];
        // Cache non-empty only — empty results may be from a stale wrong Semester id.
        if (rows.length > 0) subjectsCache.set(key, rows);
        else subjectsCache.delete(key);
        setSubjects(rows);
      })
      .catch((e) => {
        if (isAbortError(e)) return;
        setSubjects((prev) => (prev.length === 0 ? prev : []));
      })
      .finally(() => {
        if (!controller.signal.aborted) setSubjectsLoading(false);
      });
    return () => controller.abort();
  }, [courseId, groupId, semesterId]);

  // Sections require Academic Year → C/G/S. Separate from subjects so AY load cannot abort subject fetch.
  useEffect(() => {
    if (!courseId || !groupId || !semesterId) {
      setSections((prev) => (prev.length === 0 ? prev : []));
      return;
    }

    const sectionParams = buildSectionListParams({ academicYearId, courseId, groupId, semesterId });
    if (!sectionParams) {
      setSections((prev) => (prev.length === 0 ? prev : []));
      return;
    }

    const secKey = sectionsCacheKey(
      sectionParams.academicYearId,
      sectionParams.courseId,
      sectionParams.groupId,
      sectionParams.semesterId,
    );
    if (sectionsCache.has(secKey)) {
      const secCached = sectionsCache.get(secKey)!;
      setSections((prev) => (prev === secCached ? prev : secCached));
      return;
    }

    const controller = new AbortController();
    void listSections(sectionParams, { signal: controller.signal })
      .then((res) => {
        if (controller.signal.aborted) return;
        const rows = res.data ?? [];
        sectionsCache.set(secKey, rows);
        setSections(rows);
      })
      .catch((e) => {
        if (!isAbortError(e)) setSections((prev) => (prev.length === 0 ? prev : []));
      });
    return () => controller.abort();
  }, [academicYearId, courseId, groupId, semesterId]);

  // Timetable pre-fill can select a subject the faculty user is not assigned to.
  // Master /subjects already filters by StaffSubjectAssignment for Faculty — if the
  // resolved subject is missing from that list, clear it so we never call
  // students-for-marking for an unauthorized subject.
  useEffect(() => {
    if (subjectId <= 0 || !courseId || !groupId || !semesterId) return;
    const key = subjectsCacheKey(courseId, groupId, semesterId);
    const list = subjectsCache.get(key);
    if (!list) return; // still loading this scope
    if (list.some((s) => s.id === subjectId)) return;
    setSubjectId(0);
    setSubjectAccessHint("Selected subject is not in your assigned list. Pick a subject you teach.");
  }, [subjects, subjectId, courseId, groupId, semesterId]);

  const canLoadStudents = courseId > 0 && groupId > 0 && semesterId > 0 && subjectId > 0 && !!date;

  const loadStudents = async (targetPage = 1, append = false) => {
    if (!canLoadStudents) return;
    const controller = replaceAbortController(studentsAbortRef.current);
    studentsAbortRef.current = controller;
    setLoadingStudents(true);
    setError(null);
    setMessage(null);
    try {
      const res = await getStudentsForMarking(
        buildStudentsForMarkingParams({
          courseId,
          groupId,
          semesterId,
          subjectId,
          date: attendanceDateIsoUtc(),
          search: search || undefined,
          pageNumber: targetPage,
          pageSize,
          selectedSectionIds: effectiveSectionIds,
        }),
        { signal: controller.signal },
      );
      if (controller.signal.aborted) return;
      setStudents((prev) => (append ? [...prev, ...res.data.students] : res.data.students));
      if (!append) {
        setRosterCombinedMeta({
          isCombinedClass: res.data.isCombinedClass,
          participatingSectionIds: res.data.participatingSectionIds,
          participatingSectionCodes: res.data.participatingSectionCodes,
          operationalClassLabel: res.data.operationalClassLabel,
        });
      }
      setIsLocked(res.data.isLocked);
      setAlreadyMarked(res.data.alreadyMarked);
      setTotalCount(res.data.totalCount);
      if (!search) {
        setFullClassTotal(res.data.totalCount);
      }
      setPageNumber(res.data.pageNumber);
      setStatusMap((prev) => {
        const next = append ? { ...prev } : {};
        for (const s of res.data.students) {
          if (!(s.studentNumber in next)) {
            next[s.studentNumber] = s.status;
          }
        }
        return next;
      });
    } catch (e) {
      if (isAbortError(e)) return;
      // Faculty denied via FacultySubjectAccess returns HTTP 403 — server remains authoritative.
      setError(
        getApiErrorMessage(e, "Failed to load students for attendance.", {
          forbiddenFallback:
            "You are not assigned to this subject. Choose a subject from your teaching assignments.",
        }),
      );
    } finally {
      if (!controller.signal.aborted) setLoadingStudents(false);
    }
  };

  useEffect(() => {
    if (!canLoadStudents) {
      studentsAbortRef.current?.abort();
      setStudents((prev) => (prev.length === 0 ? prev : []));
      setStatusMap((prev) => (Object.keys(prev).length === 0 ? prev : {}));
      setAlreadyMarked((prev) => (prev ? false : prev));
      setIsLocked((prev) => (prev ? false : prev));
      setTotalCount((prev) => (prev === 0 ? prev : 0));
      setFullClassTotal((prev) => (prev === 0 ? prev : 0));
      setPageNumber((prev) => (prev === 1 ? prev : 1));
      setRosterCombinedMeta((prev) =>
        prev.isCombinedClass == null &&
        prev.participatingSectionIds == null &&
        prev.participatingSectionCodes == null &&
        prev.operationalClassLabel == null
          ? prev
          : {},
      );
      return;
    }
    void loadStudents(1, false);
    return () => {
      studentsAbortRef.current?.abort();
    };
    // Use a stable string key for section ids — array identity must not re-fire this effect.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [courseId, groupId, semesterId, subjectId, date, search, effectiveSectionIdsKey]);

  const hasMore = students.length < totalCount;

  const handleLoadMore = async () => {
    if (loadingStudents || !hasMore) return;
    await loadStudents(pageNumber + 1, true);
  };

  const hasRoster = fullClassTotal > 0;

  const canSave = useMemo(
    () =>
      canLoadStudents &&
      hasRoster &&
      !loadingStudents &&
      !bulkUpdating &&
      !saving &&
      !isLocked,
    [canLoadStudents, hasRoster, loadingStudents, bulkUpdating, saving, isLocked]
  );

  /** Full class list for this course/group/semester/subject/date — ignores search (search is view-only). */
  const fetchFullRoster = async () => {
    const dateIso = attendanceDateIsoUtc();
    const key = rosterCacheKey({
      courseId,
      groupId,
      semesterId,
      subjectId,
      date: dateIso,
      sectionIds: effectiveSectionIds,
    });
    const hit = rosterCache.get(key);
    if (hit && Date.now() - hit.fetchedAt < ROSTER_CACHE_TTL_MS) {
      return hit.students;
    }

    const controller = replaceAbortController(rosterAbortRef.current);
    rosterAbortRef.current = controller;
    const all: AttendanceStudentDto[] = [];
    let page = 1;
    let total = 0;

    do {
      const res = await getStudentsForMarking(
        buildStudentsForMarkingParams({
          courseId,
          groupId,
          semesterId,
          subjectId,
          date: dateIso,
          pageNumber: page,
          pageSize: rosterPageSize,
          selectedSectionIds: effectiveSectionIds,
        }),
        { signal: controller.signal },
      );
      all.push(...res.data.students);
      total = res.data.totalCount;
      page += 1;
    } while (all.length < total);

    rosterCache.set(key, { students: all, totalCount: total, fetchedAt: Date.now() });
    return all;
  };

  const resolveStatus = (s: AttendanceStudentDto, map: Record<string, number>) =>
    Object.prototype.hasOwnProperty.call(map, s.studentNumber) ? map[s.studentNumber] : s.status;

  /** Present first within the current page/list, then name — keeps toggled rows sorted too */
  const sortedStudents = useMemo(() => {
    const map = statusMap;
    const rank = (s: AttendanceStudentDto) =>
      Object.prototype.hasOwnProperty.call(map, s.studentNumber) ? map[s.studentNumber] : s.status;
    return [...students].sort((a, b) => {
      const ra = rank(a);
      const rb = rank(b);
      if (ra !== rb) return rb - ra;
      return a.name.localeCompare(b.name, undefined, { sensitivity: "base" });
    });
  }, [students, statusMap]);

  const setAllStatuses = async (status: number) => {
    if (!canLoadStudents || isLocked || loadingStudents) return;
    setBulkUpdating(true);
    setError(null);
    try {
      const all = await fetchFullRoster();
      setStatusMap((prev) => {
        const next = { ...prev };
        for (const st of all) next[st.studentNumber] = status;
        return next;
      });
    } catch {
      setError("Could not load the full class list for bulk update.");
    } finally {
      setBulkUpdating(false);
    }
  };

  const handleSave = async () => {
    if (!canSave) return;
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      // Server-loaded roster for the selected scope only — do not filter eligibility in React.
      const allStudents = await fetchFullRoster();
      const map = statusMap;
      const payload = buildAttendanceWritePayload({
        subjectId,
        date: attendanceDateIsoUtc(),
        students: allStudents,
        getStatus: (s) => resolveStatus(s, map),
        selectedSectionIds: effectiveSectionIds,
        operation: alreadyMarked ? "edit" : "mark",
      });

      if (alreadyMarked) {
        await editAttendance(payload);
        setMessage("Attendance updated successfully.");
      } else {
        await markAttendance(payload);
        setMessage("Attendance marked successfully.");
      }

      rosterCache.clear();
      await loadStudents(1, false);
    } catch (e) {
      setError(
        getApiErrorMessage(e, "Failed to save attendance.", {
          forbiddenFallback: "You are not authorized to mark or edit attendance for this subject/scope.",
        }),
      );
    } finally {
      setSaving(false);
    }
  };

  const isManualMode = attendanceMethod === "manual";
  const isAiPhotoMode = attendanceMethod === "aiPhoto";

  const sectionCodesForDisplay = useMemo(() => {
    if (rosterCombinedMeta.participatingSectionCodes?.length) {
      return rosterCombinedMeta.participatingSectionCodes;
    }
    if (resolvedSectionCodes.length > 0) return resolvedSectionCodes;
    return selectedSectionIds
      .map((id) => sections.find((s) => s.id === id)?.sectionCode)
      .filter((c): c is string => Boolean(c));
  }, [
    rosterCombinedMeta.participatingSectionCodes,
    resolvedSectionCodes,
    selectedSectionIds,
    sections,
  ]);

  const combinedClassView = useMemo(
    () =>
      buildCombinedSectionClassView({
        sectionIds: rosterCombinedMeta.participatingSectionIds?.length
          ? rosterCombinedMeta.participatingSectionIds
          : effectiveSectionIds,
        sectionCodes: sectionCodesForDisplay,
        operationalClassLabel: rosterCombinedMeta.operationalClassLabel,
        isCombinedClass: rosterCombinedMeta.isCombinedClass,
      }),
    [
      rosterCombinedMeta.participatingSectionIds,
      rosterCombinedMeta.operationalClassLabel,
      rosterCombinedMeta.isCombinedClass,
      effectiveSectionIds,
      sectionCodesForDisplay,
    ],
  );

  const showStudentSectionColumn =
    combinedClassView.isCombined ||
    students.some((s) => Boolean(s.sectionCode)) ||
    effectiveSectionIds.length > 0;

  /** Optional Program context only — never required for Manual attendance. */
  const programContextLabel = useMemo(() => {
    if (!academicUi.enablePrograms || courseId <= 0) return null;
    const courseRow = academicUi.catalogs.courses.find((c) => c.id === courseId);
    const programId = courseRow?.programId;
    if (programId == null || programId <= 0) return null;
    const program = academicUi.catalogs.programs.find((p) => p.id === programId);
    return program ? `${program.programCode} — ${program.programName}` : null;
  }, [academicUi.enablePrograms, academicUi.catalogs.courses, academicUi.catalogs.programs, courseId]);

  const operationalContextView = useMemo(
    () =>
      buildOperationalTimetableContextView({
        resolution: sessionResolution,
        driftedToManual: scopeMode === "Manual",
        labels: {
          programName: programContextLabel,
          courseName: courses.find((c) => c.id === courseId)?.name,
          groupName: groups.find((g) => g.id === groupId)?.name,
          semesterName: semesters.find((s) => s.id === semesterId)?.name,
          subjectName:
            subjects.find((s) => s.id === subjectId)?.name ?? sessionResolution?.subjectName ?? null,
          sectionLabel:
            sectionOrGroupLabel(sectionCodesForDisplay) ??
            (effectiveSectionIds.length ? `${effectiveSectionIds.length} section(s)` : null),
          periodLabel: periodNumber > 0 ? `Period ${periodNumber}` : null,
          roomName: scopeMode === "Timetable" ? roomName : null,
          dateLabel: date || null,
        },
      }),
    [
      sessionResolution,
      scopeMode,
      programContextLabel,
      courses,
      courseId,
      groups,
      groupId,
      semesters,
      semesterId,
      subjects,
      subjectId,
      sectionCodesForDisplay,
      effectiveSectionIds,
      periodNumber,
      roomName,
      date,
    ],
  );

  // MUI Select crashes (blank page) when value is not among MenuItems — common on faculty
  // cold start with persisted Course/Group/Semester/Subject before lists return.
  const selectCourseId = safeSelectValue(courseId, courses);
  const selectGroupId = safeSelectValue(groupId, groups);
  const selectSemesterId = safeSelectValue(semesterId, scopedSemesters);
  const selectSubjectId = safeSelectValue(subjectId, subjects);
  const selectPeriodNumber = safePeriodValue(periodNumber, PERIOD_OPTIONS);
  const selectSectionIds = useMemo(() => {
    if (!sectionScopeEnabled) return EMPTY_SECTION_IDS;
    const next = safeMultiSelectValues(selectedSectionIds, sections);
    return next.length === 0 ? EMPTY_SECTION_IDS : next;
  }, [sectionScopeEnabled, selectedSectionIds, sections]);

  const attendanceContext = useMemo<AttendanceContext>(
    () => ({
      courseId,
      groupId,
      semesterId,
      subjectId,
      attendanceDate: date,
      periodNumber,
      attendanceMethod,
      courseName: courses.find((c) => c.id === courseId)?.name,
      groupName: groups.find((g) => g.id === groupId)?.name,
      semesterName: semesters.find((s) => s.id === semesterId)?.name,
      subjectName: subjects.find((s) => s.id === subjectId)?.name,
      sectionIds: effectiveSectionIds,
      sectionCodes: sectionScopeEnabled ? sectionCodesForDisplay : [],
      roomName: roomName ?? undefined,
      scopeMode,
    }),
    [
      courseId,
      groupId,
      semesterId,
      subjectId,
      date,
      periodNumber,
      attendanceMethod,
      courses,
      groups,
      semesters,
      subjects,
      effectiveSectionIds,
      sectionCodesForDisplay,
      sectionScopeEnabled,
      roomName,
      scopeMode,
    ],
  );

  return (
    <Stack
      spacing={1.25}
      component="main"
      aria-label="Mark attendance"
      sx={{ ...academicPageShellSx, pb: isMobile ? 22 : 2 }}
    >
      <Stack direction="row" spacing={0.5} sx={{ alignItems: "center", flexWrap: "wrap" }}>
        <Typography variant={isMobile ? "h5" : "h4"} sx={{ fontWeight: 800, flex: 1 }}>
          Mark Attendance
        </Typography>
        <AcademicHelpHint
          title="Attendance workflow"
          body="Mobile prioritizes marking and save actions. Desktop supports full administrative filters. Section scope is optional; timetable resolution remains authoritative when present."
        />
      </Stack>
      <AcademicContextBreadcrumb
        context={{
          courseId: courseId || null,
          groupId: groupId || null,
          semesterId: semesterId || null,
          sectionId: effectiveSectionIds.length === 1 ? effectiveSectionIds[0] : null,
          sectionIds: effectiveSectionIds,
          subjectId: subjectId || null,
        }}
      />

      <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 0.75 }}>
        <Chip
          size="small"
          color={scopeMode === "Timetable" ? "primary" : "default"}
          label={scopeMode === "Timetable" ? "Timetable context" : "Manual attendance"}
          sx={academicChipSx}
        />
        {programContextLabel ? (
          <Chip size="small" variant="outlined" label={`Program ${programContextLabel}`} sx={academicChipSx} />
        ) : null}
        {sectionScopeEnabled && academicYearId != null ? (
          <Chip size="small" variant="outlined" label={`AY #${academicYearId}`} sx={academicChipSx} />
        ) : null}
        {roomName && scopeMode === "Timetable" ? (
          <Chip size="small" variant="outlined" label={`Room ${roomName}`} sx={academicChipSx} />
        ) : null}
        {sectionCodesForDisplay.length > 1 ? (
          <Chip
            size="small"
            color="secondary"
            variant="outlined"
            label={`Combined: ${sectionCodesForDisplay.join(" + ")}`}
            sx={academicChipSx}
          />
        ) : null}
        {sectionCodesForDisplay.length === 1 ? (
          <Chip size="small" variant="outlined" label={`Section ${sectionCodesForDisplay[0]}`} sx={academicChipSx} />
        ) : null}
      </Box>

      {loadingMeta && (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
          <CircularProgress size={22} />
          <Typography variant="body2" color="text.secondary">
            Loading courses and semesters…
          </Typography>
        </Box>
      )}

      <OperationalTimetableContextPanel view={operationalContextView} />
      <CombinedSectionClassBanner view={combinedClassView} />
      {!sectionScopeEnabled && academicYearAuthority.message && (
        <Alert severity="warning">{academicYearAuthority.message}</Alert>
      )}
      {subjectAccessHint && <Alert severity="warning">{subjectAccessHint}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}
      {isLocked && isManualMode && <Alert severity="warning">Attendance is locked for this date and subject.</Alert>}
      {!isLocked && canLoadStudents && isManualMode && (
        <Alert severity="info" sx={{ py: 0.75 }}>
          Search only filters the list. Save sends every student in this class for the selected subject and date
          (present and absent)
          {selectedSectionIds.length > 0 ? " within the selected section(s)" : ""}.
        </Alert>
      )}

      <Card>
        <CardContent>
          <Stack spacing={2}>
            <TextField
              select
              label="Course"
              value={selectCourseId}
              onChange={(e) => {
                setCourseId(Number(e.target.value));
                setGroupId(0);
                setSemesterId(0);
                setSubjectId(0);
                setSelectedSectionIds([]);
                setResolvedSectionCodes([]);
              }}
              fullWidth
              disabled={loadingMeta}
            >
              <MenuItem value={0}>Select course</MenuItem>
              {courses.map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {c.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Group"
              value={selectGroupId}
              onChange={(e) => {
                setGroupId(Number(e.target.value));
                setSemesterId(0);
                setSubjectId(0);
                setSelectedSectionIds([]);
                setResolvedSectionCodes([]);
              }}
              fullWidth
              disabled={loadingMeta || !courseId}
            >
              <MenuItem value={0}>Select group</MenuItem>
              {groups.map((g) => (
                <MenuItem key={g.id} value={g.id}>
                  {g.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Semester"
              value={selectSemesterId}
              onChange={(e) => {
                setSemesterId(Number(e.target.value));
                setSubjectId(0);
                setSelectedSectionIds([]);
                setResolvedSectionCodes([]);
              }}
              fullWidth
              disabled={loadingMeta || !courseId || !groupId}
              helperText={
                courseId && groupId && scopedSemesters.length === 0
                  ? "No semesters configured for this Course + Group."
                  : "Semesters are filtered to the selected Course + Group."
              }
            >
              <MenuItem value={0}>Select semester</MenuItem>
              {scopedSemesters.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Section (optional)"
              value={selectSectionIds}
              onChange={(e) => {
                if (!sectionScopeEnabled) return;
                const raw = e.target.value;
                const next = typeof raw === "string" ? raw.split(",").map(Number) : (raw as number[]);
                setSelectedSectionIds(normalizeSectionIds(next));
                setResolvedSectionCodes([]);
              }}
              fullWidth
              disabled={loadingMeta || !sectionScopeEnabled || !courseId || !groupId || !semesterId}
              helperText={
                !sectionScopeEnabled
                  ? academicYearAuthority.message
                  : describeAttendancePopulation(
                      selectedSectionIds,
                      selectedSectionIds.map(
                        (id) => sections.find((s) => s.id === id)?.sectionCode ?? "",
                      ),
                    )
              }
              slotProps={{
                // displayEmpty + empty multi value overlaps the floating label unless shrink is forced.
                inputLabel: { shrink: true },
                select: {
                  multiple: true,
                  displayEmpty: true,
                  renderValue: (selected) => {
                    const ids = selected as number[];
                    if (!ids.length) return "All students (no section filter)";
                    const labels = ids.map(
                      (id) => sections.find((s) => s.id === id)?.sectionCode ?? `Section ${id}`,
                    );
                    return labels.join(" + ");
                  },
                },
              }}
            >
              {/* Never use MenuItem value="" here — MUI multiple Select + string option crashes React (blank page). */}
              {sections.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  <Checkbox checked={selectSectionIds.includes(s.id)} size="small" sx={{ mr: 1 }} />
                  {s.sectionCode} — {s.sectionName}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Subject"
              value={selectSubjectId}
              onChange={(e) => setSubjectId(Number(e.target.value))}
              fullWidth
              disabled={loadingMeta || subjectsLoading || !courseId || !groupId || !semesterId}
              helperText={
                subjectsLoading
                  ? "Loading subjects…"
                  : subjects.length === 0 && courseId && groupId && semesterId
                    ? "No subjects assigned for this Course + Group + Semester (or none in your teaching assignments)."
                    : "Subject Master = Course + Group + Semester. Section does not redefine subjects."
              }
            >
              <MenuItem value={0}>{subjectsLoading ? "Loading subjects…" : "Select subject"}</MenuItem>
              {subjects.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Period"
              value={selectPeriodNumber}
              onChange={(e) => setPeriodNumber(Number(e.target.value))}
              fullWidth
            >
              {PERIOD_OPTIONS.map((period) => (
                <MenuItem key={period.value} value={period.value}>
                  {period.label}
                </MenuItem>
              ))}
            </TextField>

            <FormControl component="fieldset" fullWidth>
              <FormLabel component="legend" sx={{ mb: 1 }}>
                Attendance Method
              </FormLabel>
              <ToggleButtonGroup
                exclusive
                fullWidth
                value={attendanceMethod}
                onChange={(_, value: AttendanceMethodMode | null) => {
                  if (value) setAttendanceMethod(value);
                }}
                aria-label="Attendance method"
                sx={{
                  display: "flex",
                  "& .MuiToggleButton-root": {
                    flex: 1,
                    py: 1.25,
                    textTransform: "none",
                    fontWeight: 600,
                  },
                }}
              >
                <ToggleButton value="manual">Manual Attendance</ToggleButton>
                <ToggleButton value="aiPhoto">AI Photo Attendance</ToggleButton>
              </ToggleButtonGroup>
            </FormControl>

            <TextField
              label="Attendance Date"
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
            />

            {isManualMode && (
              <TextField
                label="Search (student no / name / mobile)"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                fullWidth
              />
            )}
          </Stack>
        </CardContent>
      </Card>

      {isAiPhotoMode && (
        <AiAttendancePanel context={attendanceContext} totalStudents={fullClassTotal} />
      )}

      {isManualMode && (
        <>
      <Box
        sx={{
          display: "flex",
          flexWrap: "wrap",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 1.5,
          rowGap: 1,
        }}
      >
        <Typography variant="body2" color="text.secondary" sx={{ flex: "1 1 auto", minWidth: 0 }}>
          {bulkUpdating
            ? "Applying to full class..."
            : loadingStudents
              ? "Loading students..."
              : `Showing: ${students.length} / ${totalCount}${search ? " (filtered)" : ""}`}
        </Typography>
        <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 1, justifyContent: "flex-end" }}>
          {!isMobile && (
            <ToggleButtonGroup
              size="small"
              exclusive
              value={viewMode}
              onChange={(_, value: "card" | "table" | null) => {
                if (value) setViewMode(value);
              }}
            >
              <ToggleButton value="table">Table</ToggleButton>
              <ToggleButton value="card">Cards</ToggleButton>
            </ToggleButtonGroup>
          )}
          {!isMobile && (
            <>
              <Button
                variant="outlined"
                onClick={() => void setAllStatuses(1)}
                disabled={!canSave || bulkUpdating}
              >
                All present
              </Button>
              <Button
                variant="outlined"
                onClick={() => void setAllStatuses(0)}
                disabled={!canSave || bulkUpdating}
              >
                All absent
              </Button>
              <Button variant="contained" onClick={() => void handleSave()} disabled={!canSave || bulkUpdating}>
                {saving ? "Saving..." : alreadyMarked ? "Update attendance" : "Save attendance"}
              </Button>
            </>
          )}
        </Box>
      </Box>

      {!isMobile && viewMode === "table" ? (
        <TableContainer
          component={Paper}
          variant="outlined"
          sx={{ maxHeight: "62vh", overflow: "auto" }}
        >
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                <TableCell
                  sx={{
                    position: "sticky",
                    left: 0,
                    zIndex: 4,
                    backgroundColor: "background.paper",
                    width: 72,
                    minWidth: 72,
                  }}
                >
                  Sl.No
                </TableCell>
                <TableCell
                  sx={{
                    position: "sticky",
                    left: 72,
                    zIndex: 3,
                    backgroundColor: "background.paper",
                    minWidth: 140,
                  }}
                >
                  Student No
                </TableCell>
                <TableCell>Name</TableCell>
                {showStudentSectionColumn && <TableCell>Section</TableCell>}
                <TableCell>Batch</TableCell>
                <TableCell>Mobile</TableCell>
                <TableCell>Email</TableCell>
                <TableCell align="center" sx={{ whiteSpace: "nowrap" }}>
                  Present
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {sortedStudents.map((s, idx) => (
                <TableRow key={s.studentNumber} hover>
                  <TableCell
                    sx={{
                      position: "sticky",
                      left: 0,
                      zIndex: 3,
                      backgroundColor: "background.paper",
                      width: 72,
                      minWidth: 72,
                    }}
                  >
                    {(pageNumber - 1) * pageSize + idx + 1}
                  </TableCell>
                  <TableCell
                    sx={{
                      position: "sticky",
                      left: 72,
                      zIndex: 2,
                      backgroundColor: "background.paper",
                      minWidth: 140,
                    }}
                  >
                    {s.studentNumber}
                  </TableCell>
                  <TableCell>{s.name}</TableCell>
                  {showStudentSectionColumn && (
                    <TableCell>{s.sectionCode ?? "-"}</TableCell>
                  )}
                  <TableCell>{s.batch ?? "-"}</TableCell>
                  <TableCell>{s.mobile || "-"}</TableCell>
                  <TableCell>{s.email || "-"}</TableCell>
                  <TableCell align="center">
                    <Checkbox
                      checked={resolveStatus(s, statusMap) === 1}
                      onChange={(e) =>
                        setStatusMap((prev) => ({
                          ...prev,
                          [s.studentNumber]: e.target.checked ? 1 : 0,
                        }))
                      }
                      disabled={isLocked}
                      slotProps={{ input: { "aria-label": `Present for ${s.studentNumber}` } }}
                      size="small"
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      ) : (
        sortedStudents.map((s, idx) => (
          <Card key={s.studentNumber}>
            <CardContent sx={{ pb: "16px !important" }}>
              <Stack spacing={1}>
                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    gap: 1,
                    flexWrap: "wrap",
                  }}
                >
                  <Typography variant="subtitle2" sx={{ flex: "1 1 auto", minWidth: 0 }} noWrap>
                    Sl.No: {(pageNumber - 1) * pageSize + idx + 1}
                  </Typography>
                  <FormControlLabel
                    sx={{ mr: 0, ml: 0 }}
                    control={
                      <Checkbox
                        checked={resolveStatus(s, statusMap) === 1}
                        onChange={(e) =>
                          setStatusMap((prev) => ({
                            ...prev,
                            [s.studentNumber]: e.target.checked ? 1 : 0,
                          }))
                        }
                        disabled={isLocked}
                        size="small"
                      />
                    }
                    label="Present"
                  />
                </Box>

                <Typography variant="body2">
                  <strong>Student No:</strong> {s.studentNumber}
                </Typography>
                <Typography variant="body2">
                  <strong>Name:</strong> {s.name}
                </Typography>
                {showStudentSectionColumn && (
                  <Typography variant="body2">
                    <strong>Section:</strong> {s.sectionCode ?? "-"}
                  </Typography>
                )}
                <Typography variant="body2">
                  <strong>Batch:</strong> {s.batch ?? "-"}
                </Typography>
                <Typography variant="body2">
                  <strong>Mobile:</strong> {s.mobile || "-"}
                </Typography>
                {!isMobile && (
                  <Typography variant="body2">
                    <strong>Email:</strong> {s.email || "-"}
                  </Typography>
                )}
              </Stack>
            </CardContent>
          </Card>
        ))
      )}

      {hasMore && (
        <Button variant="outlined" onClick={handleLoadMore} disabled={loadingStudents}>
          {loadingStudents ? "Loading..." : "Load more"}
        </Button>
      )}

      {isMobile && (
        <Box
          sx={{
            position: "fixed",
            left: 8,
            right: 8,
            zIndex: 1202,
            bottom: `max(52px, calc(8px + env(safe-area-inset-bottom, 0px)))`,
            backgroundColor: "background.paper",
            border: 1,
            borderColor: "divider",
            borderRadius: 2,
            p: 1,
            boxShadow: 3,
          }}
        >
          <Box sx={{ display: "flex", gap: 1, mb: 1 }}>
            <Button
              fullWidth
              size="small"
              variant="outlined"
              onClick={() => void setAllStatuses(1)}
              disabled={!canSave || bulkUpdating}
            >
              All present
            </Button>
            <Button
              fullWidth
              size="small"
              variant="outlined"
              onClick={() => void setAllStatuses(0)}
              disabled={!canSave || bulkUpdating}
            >
              All absent
            </Button>
          </Box>
          <Button
            fullWidth
            variant="contained"
            onClick={() => void handleSave()}
            disabled={!canSave || bulkUpdating}
          >
            {saving ? "Saving..." : alreadyMarked ? "Update attendance" : "Save attendance"}
          </Button>
        </Box>
      )}
        </>
      )}
    </Stack>
  );
};

export default AttendanceMarking;

