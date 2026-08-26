import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import type { SectionDto } from "../services/sectionService";
import type { SubjectDto } from "../services/attendanceService";
import { useAuth } from "./AuthContext";
import { useTenantContext } from "./TenantContextProvider";
import {
  getAcademicConfiguration,
  getAcademicHierarchy,
  listPrograms,
  type ProgramDto,
} from "../services/programService";
import { listAcademicYears, type AcademicYearDto } from "../services/schedulingService";
import { listMasterCourses, listMasterGroups, listSemesters, listStaff } from "../services/setupService";
import { listSections } from "../services/sectionService";
import { getSubjects } from "../services/attendanceService";
import { getApiErrorMessage } from "../utils/apiErrorMessage";
import { isAbortError, replaceAbortController } from "../utils/academicRequest";
import {
  academicCascadePath,
  applyCascadeSelection,
  buildProgramCourseIndex,
  collectHierarchyConsistencyWarnings,
  filterCoursesForProgram,
  filterGroupsForCourse,
  filterSectionsForScope,
  filterSemestersForCourseGroup,
  resetAcademicSelection,
  sanitizeSelectionAgainstOptions,
  type CascadePatch,
  type HierarchyConsistencyWarning,
} from "../utils/academicCascade";
import { listCourses } from "../services/setupService";
import {
  emptyAcademicUiCatalogs,
  emptyAcademicUiSelection,
  type AcademicAttendanceContext,
  type AcademicTimetableContext,
  type AcademicUiCatalogs,
  type AcademicUiFilteredOptions,
  type AcademicUiSelection,
} from "../types/academicUiContext";

type AcademicUiContextValue = {
  /** Tenant flag from `/academic-structure/configuration`. */
  enablePrograms: boolean;
  /** True when Programs feature is on and at least one program exists. */
  programsAvailable: boolean;
  /** Hierarchy GET succeeded while Programs enabled (fail-closed Course filter depends on this). */
  hierarchyReady: boolean;
  /** Hierarchy GET failed while Programs enabled — Course options stay empty until refresh. */
  hierarchyFailed: boolean;
  /** Hierarchy projection disagreed with authoritative Course.ProgramId (diagnostic). */
  hierarchyConsistencyWarnings: HierarchyConsistencyWarning[];
  loading: boolean;
  /** Scoped section list fetch in progress. */
  sectionsLoading: boolean;
  /** Scoped subject list fetch in progress (Course + Group + Semester). */
  subjectsLoading: boolean;
  error: string | null;
  selection: AcademicUiSelection;
  catalogs: AcademicUiCatalogs;
  /** Cascaded options — pages must prefer these over raw catalogs. */
  options: AcademicUiFilteredOptions;
  cascadePath: string;
  timetableContext: AcademicTimetableContext | null;
  attendanceContext: AcademicAttendanceContext | null;
  setSelection: (patch: CascadePatch) => void;
  replaceSelection: (next: AcademicUiSelection) => void;
  clearSelection: () => void;
  setTimetableContext: (ctx: AcademicTimetableContext | null) => void;
  setAttendanceContext: (ctx: AcademicAttendanceContext | null) => void;
  /** Prefill cascade from soft timetable resolution (does not mark attendance). */
  applyTimetablePrefill: (ctx: AcademicTimetableContext) => void;
  refreshCatalogs: () => Promise<void>;
  /** Load faculty page into catalogs.faculty (paginated — never full dump). */
  loadFacultyOptions: (params?: { search?: string; page?: number; pageSize?: number }) => Promise<void>;
};

const AcademicUiState = createContext<AcademicUiContextValue | null>(null);

const idSet = (ids: Array<number | null | undefined>) =>
  new Set(ids.filter((id): id is number => id != null && Number.isFinite(id)));

export const AcademicUiProvider = ({ children }: { children: ReactNode }) => {
  const { token } = useAuth();
  const { context, isSuperAdmin, hasOperationalContext, subscribe } = useTenantContext();

  const [enablePrograms, setEnablePrograms] = useState(false);
  const [hierarchyReady, setHierarchyReady] = useState(false);
  const [hierarchyFailed, setHierarchyFailed] = useState(false);
  const [hierarchyConsistencyWarnings, setHierarchyConsistencyWarnings] = useState<HierarchyConsistencyWarning[]>(
    [],
  );
  const [loading, setLoading] = useState(false);
  const [sectionsLoading, setSectionsLoading] = useState(false);
  const [subjectsLoading, setSubjectsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selection, setSelectionState] = useState<AcademicUiSelection>(emptyAcademicUiSelection);
  const [catalogs, setCatalogs] = useState<AcademicUiCatalogs>(emptyAcademicUiCatalogs);
  const [programCourseIndex, setProgramCourseIndex] = useState<Map<number, Set<number>>>(() => new Map());
  const [timetableContext, setTimetableContext] = useState<AcademicTimetableContext | null>(null);
  const [attendanceContext, setAttendanceContext] = useState<AcademicAttendanceContext | null>(null);
  const sectionCacheRef = useRef<Map<string, SectionDto[]>>(new Map());
  const subjectCacheRef = useRef<Map<string, SubjectDto[]>>(new Map());
  const sectionsAbortRef = useRef<AbortController | null>(null);
  const subjectsAbortRef = useRef<AbortController | null>(null);
  const facultyAbortRef = useRef<AbortController | null>(null);

  const canLoad = Boolean(token) && (!isSuperAdmin || hasOperationalContext);

  const refreshCatalogs = useCallback(async () => {
    if (!canLoad) {
      setCatalogs(emptyAcademicUiCatalogs());
      setEnablePrograms(false);
      setHierarchyReady(false);
      setHierarchyFailed(false);
      setHierarchyConsistencyWarnings([]);
      setProgramCourseIndex(new Map());
      sectionCacheRef.current.clear();
      subjectCacheRef.current.clear();
      return;
    }

    setLoading(true);
    setHierarchyFailed(false);
    try {
      // Soft-fail years/config: faculty may lack Scheduling.View / Program.View (403).
      // Attendance must still load Course/Group/Semester catalogs for manual marking.
      const [yearsRes, coursesRes, groupsRes, semestersRes, cfgRes] = await Promise.all([
        listAcademicYears().catch(() => ({ data: [] as AcademicYearDto[] })),
        listMasterCourses(),
        listMasterGroups(),
        listSemesters(),
        getAcademicConfiguration().catch(() => null),
      ]);

      const programsEnabled = Boolean(cfgRes?.data?.enablePrograms);
      setEnablePrograms(programsEnabled);

      let programs: ProgramDto[] = [];
      let index = new Map<number, Set<number>>();
      let nextHierarchyReady = !programsEnabled;
      let nextHierarchyFailed = false;
      let courses = coursesRes.data ?? [];
      let hierarchyError: string | null = null;
      let warnings: HierarchyConsistencyWarning[] = [];

      if (programsEnabled) {
        const programsRes = await listPrograms(false).catch(() => ({ data: [] as ProgramDto[] }));
        programs = programsRes.data ?? [];

        // Enrich authorized master catalog with authoritative Course.ProgramId (existing /api/course).
        const courseProgramRes = await listCourses().catch(() => null);
        if (courseProgramRes?.data) {
          const programById = new Map(
            courseProgramRes.data.map((c) => [c.id, c.programId ?? null] as const),
          );
          courses = courses.map((c) => ({
            ...c,
            programId: programById.has(c.id) ? programById.get(c.id) ?? null : (c.programId ?? null),
          }));
        }

        try {
          const hierarchyRes = await getAcademicHierarchy({
            includeInactive: false,
            includeSections: false,
            includeSubjects: false,
          });
          index = buildProgramCourseIndex(hierarchyRes.data?.roots);
          nextHierarchyReady = true;
          nextHierarchyFailed = false;
          warnings = collectHierarchyConsistencyWarnings(courses, index);
          if (warnings.length > 0 && typeof console !== "undefined") {
            console.warn(
              "[AI29.1D] Academic hierarchy consistency warnings (Course.ProgramId is authoritative):",
              warnings,
            );
          }
        } catch (hierarchyErr) {
          // Fail closed: do not expose unrelated courses when hierarchy cannot be loaded.
          // 401/403 from Program/structure APIs are surfaced via getApiErrorMessage (server authoritative).
          index = new Map();
          nextHierarchyReady = false;
          nextHierarchyFailed = true;
          hierarchyError = getApiErrorMessage(
            hierarchyErr,
            "Unable to load academic hierarchy. Course options are hidden until refresh succeeds.",
            {
              forbiddenFallback:
                "You are not authorized to load the academic hierarchy (Program.View). Course options stay hidden until access is granted.",
            },
          );
        }
      }

      setHierarchyReady(nextHierarchyReady);
      setHierarchyFailed(nextHierarchyFailed);
      setHierarchyConsistencyWarnings(warnings);
      setProgramCourseIndex(index);
      setCatalogs((prev) => ({
        ...prev,
        academicYears: yearsRes.data ?? [],
        programs,
        courses,
        groups: groupsRes.data ?? [],
        semesters: semestersRes.data ?? [],
        // sections/subjects refreshed by scope effects
        sections: [],
        subjects: [],
      }));
      setError(hierarchyError);
    } catch (err) {
      // Catalog load failed — do not invent Course options; keep prior fail-closed flags conservative.
      setHierarchyReady(false);
      setHierarchyConsistencyWarnings([]);
      setProgramCourseIndex(new Map());
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [canLoad]);

  useEffect(() => {
    if (!token) {
      setSelectionState(emptyAcademicUiSelection());
      setCatalogs(emptyAcademicUiCatalogs());
      setEnablePrograms(false);
      setHierarchyReady(false);
      setHierarchyFailed(false);
      setHierarchyConsistencyWarnings([]);
      setTimetableContext(null);
      setAttendanceContext(null);
      return;
    }
    void refreshCatalogs();
  }, [token, context?.selectedCollegeId, canLoad, refreshCatalogs]);

  useEffect(() => {
    const unsubChanged = subscribe("ContextChanged", () => {
      setSelectionState(emptyAcademicUiSelection());
      setTimetableContext(null);
      setAttendanceContext(null);
    });
    const unsubCleared = subscribe("ContextCleared", () => {
      setSelectionState(emptyAcademicUiSelection());
      setCatalogs(emptyAcademicUiCatalogs());
      setEnablePrograms(false);
      setHierarchyReady(false);
      setHierarchyFailed(false);
      setHierarchyConsistencyWarnings([]);
      setTimetableContext(null);
      setAttendanceContext(null);
      sectionCacheRef.current.clear();
      subjectCacheRef.current.clear();
    });
    return () => {
      unsubChanged();
      unsubCleared();
    };
  }, [subscribe]);

  // Sections for operational scope (year + C/G/S). Section is optional.
  useEffect(() => {
    if (!canLoad) return;
    const { academicYearId, courseId, groupId, semesterId } = selection;
    if (academicYearId == null || courseId == null || groupId == null || semesterId == null) {
      setSectionsLoading(false);
      setCatalogs((prev) => (prev.sections.length ? { ...prev, sections: [] } : prev));
      return;
    }

    const cacheKey = `${academicYearId}:${courseId}:${groupId}:${semesterId}`;
    const cached = sectionCacheRef.current.get(cacheKey);
    if (cached) {
      setSectionsLoading(false);
      setCatalogs((prev) => (prev.sections === cached ? prev : { ...prev, sections: cached }));
      return;
    }

    const controller = replaceAbortController(sectionsAbortRef.current);
    sectionsAbortRef.current = controller;
    setSectionsLoading(true);
    void listSections({ academicYearId, courseId, groupId, semesterId }, { signal: controller.signal })
      .then((res) => {
        const rows = res.data ?? [];
        sectionCacheRef.current.set(cacheKey, rows);
        setCatalogs((prev) => ({ ...prev, sections: rows }));
      })
      .catch((err) => {
        if (isAbortError(err)) return;
        setCatalogs((prev) => ({ ...prev, sections: [] }));
      })
      .finally(() => {
        if (!controller.signal.aborted) setSectionsLoading(false);
      });
    return () => {
      controller.abort();
    };
  }, [canLoad, selection.academicYearId, selection.courseId, selection.groupId, selection.semesterId]);

  // Subjects — Course + Group + Semester only (never Section).
  useEffect(() => {
    if (!canLoad) return;
    const { courseId, groupId, semesterId } = selection;
    if (courseId == null || groupId == null || semesterId == null) {
      setSubjectsLoading(false);
      setCatalogs((prev) => (prev.subjects.length ? { ...prev, subjects: [] } : prev));
      return;
    }

    const cacheKey = `${courseId}:${groupId}:${semesterId}`;
    const cached = subjectCacheRef.current.get(cacheKey);
    if (cached) {
      setSubjectsLoading(false);
      setCatalogs((prev) => (prev.subjects === cached ? prev : { ...prev, subjects: cached }));
      return;
    }

    const controller = replaceAbortController(subjectsAbortRef.current);
    subjectsAbortRef.current = controller;
    setSubjectsLoading(true);
    void getSubjects(courseId, groupId, semesterId, { signal: controller.signal })
      .then((res) => {
        const rows = res.data ?? [];
        subjectCacheRef.current.set(cacheKey, rows);
        setCatalogs((prev) => ({ ...prev, subjects: rows }));
      })
      .catch((err) => {
        if (isAbortError(err)) return;
        setCatalogs((prev) => ({ ...prev, subjects: [] }));
      })
      .finally(() => {
        if (!controller.signal.aborted) setSubjectsLoading(false);
      });
    return () => {
      controller.abort();
    };
  }, [canLoad, selection.courseId, selection.groupId, selection.semesterId]);

  const programsAvailable = enablePrograms && catalogs.programs.length > 0;

  const options: AcademicUiFilteredOptions = useMemo(() => {
    // Prompt 4B: EnablePrograms alone activates Program mode (no full-catalog fallback).
    const courses = filterCoursesForProgram(
      catalogs.courses,
      enablePrograms,
      selection.programId,
      programCourseIndex,
      enablePrograms
        ? { hierarchyReady, hierarchyFailed }
        : { hierarchyReady: true, hierarchyFailed: false },
    );
    const groups = filterGroupsForCourse(catalogs.groups, selection.courseId);
    const semesters = filterSemestersForCourseGroup(catalogs.semesters, selection.courseId, selection.groupId);
    const sections = filterSectionsForScope(catalogs.sections, {
      academicYearId: selection.academicYearId,
      courseId: selection.courseId,
      groupId: selection.groupId,
      semesterId: selection.semesterId,
    });
    return {
      programs: enablePrograms ? catalogs.programs : [],
      courses,
      groups,
      semesters,
      sections,
      subjects: catalogs.subjects,
    };
  }, [
    catalogs.courses,
    catalogs.groups,
    catalogs.programs,
    catalogs.sections,
    catalogs.semesters,
    catalogs.subjects,
    enablePrograms,
    hierarchyFailed,
    hierarchyReady,
    programCourseIndex,
    selection.academicYearId,
    selection.courseId,
    selection.groupId,
    selection.programId,
    selection.semesterId,
  ]);

  // Sanitize stale IDs when filtered options change (e.g. Programs toggled off).
  useEffect(() => {
    setSelectionState((prev) => {
      const next = sanitizeSelectionAgainstOptions(prev, {
        enablePrograms,
        programIds: idSet(options.programs.map((p) => p.id)),
        courseIds: idSet(options.courses.map((c) => c.id)),
        groupIds: idSet(options.groups.map((g) => g.id)),
        semesterIds: idSet(options.semesters.map((s) => s.id)),
        sectionIds: idSet(options.sections.map((s) => s.id)),
        subjectIds: idSet(options.subjects.map((s) => s.id)),
      });
      return selectionEqual(prev, next) ? prev : next;
    });
  }, [enablePrograms, options]);

  const setSelection = useCallback((patch: CascadePatch) => {
    setSelectionState((prev) => applyCascadeSelection(prev, patch));
  }, []);

  const replaceSelection = useCallback((next: AcademicUiSelection) => {
    setSelectionState({ ...next, sectionIds: [...(next.sectionIds ?? [])] });
  }, []);

  const clearSelection = useCallback(() => {
    setSelectionState(resetAcademicSelection());
  }, []);

  const applyTimetablePrefill = useCallback((ctx: AcademicTimetableContext) => {
    setTimetableContext(ctx);
    setSelectionState((prev) =>
      applyCascadeSelection(prev, {
        courseId: ctx.courseId ?? prev.courseId,
        groupId: ctx.groupId ?? prev.groupId,
        semesterId: ctx.semesterId ?? prev.semesterId,
        subjectId: ctx.subjectId ?? prev.subjectId,
        sectionIds: ctx.sectionIds?.length ? [...ctx.sectionIds] : prev.sectionIds,
        sectionId:
          ctx.sectionIds?.length === 1
            ? ctx.sectionIds[0]!
            : ctx.sectionIds?.length
              ? null
              : prev.sectionId,
      }),
    );
  }, []);

  const loadFacultyOptions = useCallback(
    async (params?: { search?: string; page?: number; pageSize?: number }) => {
      if (!canLoad) return;
      const controller = replaceAbortController(facultyAbortRef.current);
      facultyAbortRef.current = controller;
      try {
        const collegeId = context?.selectedCollegeId ?? undefined;
        const res = await listStaff(
          {
            collegeId: collegeId ?? undefined,
            search: params?.search,
            page: params?.page ?? 1,
            pageSize: params?.pageSize ?? 50,
          },
          { signal: controller.signal },
        );
        setCatalogs((prev) => ({ ...prev, faculty: res.data?.items ?? [] }));
      } catch (err) {
        if (isAbortError(err)) return;
        setError(getApiErrorMessage(err));
      }
    },
    [canLoad, context?.selectedCollegeId],
  );

  const value = useMemo<AcademicUiContextValue>(
    () => ({
      enablePrograms,
      programsAvailable,
      hierarchyReady,
      hierarchyFailed,
      hierarchyConsistencyWarnings,
      loading,
      sectionsLoading,
      subjectsLoading,
      error,
      selection,
      catalogs,
      options,
      cascadePath: academicCascadePath(enablePrograms),
      timetableContext,
      attendanceContext,
      setSelection,
      replaceSelection,
      clearSelection,
      setTimetableContext,
      setAttendanceContext,
      applyTimetablePrefill,
      refreshCatalogs,
      loadFacultyOptions,
    }),
    [
      enablePrograms,
      programsAvailable,
      hierarchyReady,
      hierarchyFailed,
      hierarchyConsistencyWarnings,
      loading,
      sectionsLoading,
      subjectsLoading,
      error,
      selection,
      catalogs,
      options,
      timetableContext,
      attendanceContext,
      setSelection,
      replaceSelection,
      clearSelection,
      applyTimetablePrefill,
      refreshCatalogs,
      loadFacultyOptions,
    ],
  );

  return <AcademicUiState.Provider value={value}>{children}</AcademicUiState.Provider>;
};

function selectionEqual(a: AcademicUiSelection, b: AcademicUiSelection): boolean {
  return (
    a.academicYearId === b.academicYearId &&
    a.programId === b.programId &&
    a.courseId === b.courseId &&
    a.groupId === b.groupId &&
    a.semesterId === b.semesterId &&
    a.sectionId === b.sectionId &&
    a.subjectId === b.subjectId &&
    a.facultyId === b.facultyId &&
    a.sectionIds.length === b.sectionIds.length &&
    a.sectionIds.every((id, i) => id === b.sectionIds[i])
  );
}

export const useAcademicUi = (): AcademicUiContextValue => {
  const ctx = useContext(AcademicUiState);
  if (!ctx) {
    throw new Error("useAcademicUi must be used within AcademicUiProvider");
  }
  return ctx;
};
