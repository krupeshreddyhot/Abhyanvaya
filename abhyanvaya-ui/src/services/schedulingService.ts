import api from "../api/axios";

// --- Enums (mirror backend Abhyanvaya.Domain.Enums.Scheduling) ---

export const HolidayType = {
  National: 1,
  University: 2,
  College: 3,
  Exam: 4,
  Unexpected: 5,
} as const;
export type HolidayType = (typeof HolidayType)[keyof typeof HolidayType];

export const RoomType = {
  Classroom: 1,
  ComputerLab: 2,
  ScienceLab: 3,
  CommerceLab: 4,
  Seminar: 5,
  Auditorium: 6,
  Other: 99,
} as const;
export type RoomType = (typeof RoomType)[keyof typeof RoomType];

export const RoomStatus = {
  Available: 1,
  Maintenance: 2,
  Reserved: 3,
} as const;
export type RoomStatus = (typeof RoomStatus)[keyof typeof RoomStatus];

export const RoomFeatureFlags = {
  None: 0,
  AiCamera: 1,
  Projector: 2,
  Wifi: 4,
  SmartBoard: 8,
  SmartClassroom: 16,
} as const;
export type RoomFeatureFlags = number;

export const SlotKind = {
  Period: 1,
  Break: 2,
  Lunch: 3,
  WorkingSession: 4,
} as const;
export type SlotKind = (typeof SlotKind)[keyof typeof SlotKind];

export const SessionKind = {
  None: 0,
  Morning: 1,
  Afternoon: 2,
  Evening: 3,
} as const;
export type SessionKind = (typeof SessionKind)[keyof typeof SessionKind];

export const FacultyDayPreferenceType = {
  Preferred: 1,
  Unavailable: 2,
} as const;
export type FacultyDayPreferenceType = (typeof FacultyDayPreferenceType)[keyof typeof FacultyDayPreferenceType];

export const FacultyAvailabilityType = {
  Preferred: 1,
  Unavailable: 2,
  AdministrativeDuty: 3,
  ExamDuty: 4,
  ApprovedLeave: 5,
  Custom: 6,
} as const;
export type FacultyAvailabilityType = (typeof FacultyAvailabilityType)[keyof typeof FacultyAvailabilityType];

export const RoomAvailabilityType = {
  Available: 1,
  Maintenance: 2,
  Reserved: 3,
  Examination: 4,
  Blocked: 5,
  Cleaning: 6,
} as const;
export type RoomAvailabilityType = (typeof RoomAvailabilityType)[keyof typeof RoomAvailabilityType];

export const TimeSlotTemplateType = {
  Regular: 1,
  Friday: 2,
  HalfDay: 3,
  Examination: 4,
  Holiday: 5,
  Summer: 6,
  Winter: 7,
} as const;
export type TimeSlotTemplateType = (typeof TimeSlotTemplateType)[keyof typeof TimeSlotTemplateType];

export const PreferredTeachingMode = {
  Morning: 1,
  Afternoon: 2,
  Evening: 3,
  Any: 4,
} as const;
export type PreferredTeachingMode = (typeof PreferredTeachingMode)[keyof typeof PreferredTeachingMode];

// --- DTOs ---

export type AcademicYearDto = {
  id: number;
  name: string;
  code: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
};

export type CreateAcademicYearRequest = {
  name: string;
  code: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
};

export type UpdateAcademicYearRequest = CreateAcademicYearRequest & { id: number };

export type ClonePreviousYearRequest = {
  sourceYearId: number;
  name: string;
  code: string;
  startDate: string;
  endDate: string;
  setAsCurrent: boolean;
};

export type WorkingDayDto = {
  id: number;
  academicYearId: number;
  dayOfWeek: number;
  isWorking: boolean;
};

export type UpsertWorkingDayRequest = {
  id?: number | null;
  academicYearId: number;
  dayOfWeek: number;
  isWorking: boolean;
};

export type HolidayDto = {
  id: number;
  academicYearId: number;
  name: string;
  date: string;
  holidayType: HolidayType;
  description: string | null;
  holidayTypeCatalogId: number | null;
  isWorkingDayOverride: boolean;
  requiresRescheduling: boolean;
  colour: string | null;
  priority: number | null;
};

export type CreateHolidayRequest = Omit<HolidayDto, "id">;
export type UpdateHolidayRequest = HolidayDto;

export type CampusDto = {
  id: number;
  name: string;
  code: string;
  address: string | null;
  isActive: boolean;
};

export type CreateCampusRequest = Omit<CampusDto, "id">;
export type UpdateCampusRequest = CampusDto;

export type BuildingDto = {
  id: number;
  campusId: number;
  name: string;
  code: string;
  isActive: boolean;
};

export type CreateBuildingRequest = Omit<BuildingDto, "id">;
export type UpdateBuildingRequest = BuildingDto;

export type FloorDto = {
  id: number;
  buildingId: number;
  name: string;
  levelNumber: number;
};

export type CreateFloorRequest = Omit<FloorDto, "id">;
export type UpdateFloorRequest = FloorDto;

export type RoomDto = {
  id: number;
  floorId: number;
  name: string;
  code: string;
  roomType: RoomType;
  capacity: number;
  status: RoomStatus;
  featureFlags: RoomFeatureFlags;
  departmentId: number | null;
  isActive: boolean;
  campusName: string | null;
  buildingName: string | null;
  floorName: string | null;
};

export type CreateRoomRequest = Omit<RoomDto, "id" | "campusName" | "buildingName" | "floorName">;
export type UpdateRoomRequest = CreateRoomRequest & { id: number };

export type RoomSearchQuery = {
  search?: string;
  roomType?: RoomType;
  status?: RoomStatus;
  campusId?: number;
  buildingId?: number;
  floorId?: number;
  isActive?: boolean;
  sortBy?: string;
  sortDescending?: boolean;
  page?: number;
  pageSize?: number;
};

export type PagedRoomsResult = {
  items: RoomDto[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type TimeSlotSetDto = {
  id: number;
  name: string;
  code: string;
  academicYearId: number | null;
  description: string | null;
  isDefault: boolean;
};

export type CreateTimeSlotSetRequest = Omit<TimeSlotSetDto, "id">;
export type UpdateTimeSlotSetRequest = TimeSlotSetDto;

export type CloneTimeSlotSetRequest = {
  sourceSetId: number;
  name: string;
  code: string;
  academicYearId: number | null;
  isDefault: boolean;
};

export type TimeSlotDto = {
  id: number;
  timeSlotSetId: number;
  periodNumber: number | null;
  name: string;
  startTime: string;
  endTime: string;
  durationMinutes: number;
  dayOfWeek: number | null;
  slotKind: SlotKind;
  sessionKind: SessionKind;
};

export type CreateTimeSlotRequest = Omit<TimeSlotDto, "id">;
export type UpdateTimeSlotRequest = TimeSlotDto;

export type FacultyDayPreferenceDto = {
  id: number;
  facultyWorkloadId: number;
  dayOfWeek: number;
  preferenceType: FacultyDayPreferenceType;
};

export type FacultyTimeSlotPreferenceDto = {
  id: number;
  facultyWorkloadId: number;
  timeSlotId: number;
  isPreferred: boolean;
};

export type FacultyWorkloadDto = {
  id: number;
  staffId: number;
  maxPeriodsPerDay: number;
  maxPeriodsPerWeek: number;
  teachingLoadHours: number;
  labLoadHours: number;
  mentoringLoadHours: number;
  administrativeLoadHours: number;
  isGuestFaculty: boolean;
  isAdjunctFaculty: boolean;
  notes: string | null;
  dayPreferences: FacultyDayPreferenceDto[];
  timeSlotPreferences: FacultyTimeSlotPreferenceDto[];
};

export type UpsertFacultyWorkloadRequest = Omit<
  FacultyWorkloadDto,
  "id" | "dayPreferences" | "timeSlotPreferences"
>;

export type UpsertFacultyDayPreferenceRequest = {
  id?: number | null;
  facultyWorkloadId: number;
  dayOfWeek: number;
  preferenceType: FacultyDayPreferenceType;
};

export type UpsertFacultyTimeSlotPreferenceRequest = {
  id?: number | null;
  facultyWorkloadId: number;
  timeSlotId: number;
  isPreferred: boolean;
};

export type SubjectAllocationDto = {
  id: number;
  academicYearId: number;
  subjectId: number;
  staffId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
  departmentId: number;
  weeklyHours: number;
  preferredRoomId: number | null;
  labRequired: boolean;
  aiAttendanceEnabled: boolean;
  attendanceMandatory: boolean;
  effectiveFrom: string;
  effectiveTo: string | null;
  notes: string | null;
};

export type CreateSubjectAllocationRequest = Omit<SubjectAllocationDto, "id">;
export type UpdateSubjectAllocationRequest = SubjectAllocationDto;

export type RoomAllocationRuleDto = {
  id: number;
  name: string;
  academicYearId: number | null;
  roomType: RoomType | null;
  minCapacity: number | null;
  maxCapacity: number | null;
  departmentId: number | null;
  courseId: number | null;
  requireComputerLab: boolean;
  requireScienceLab: boolean;
  requireCommerceLab: boolean;
  requireAiCamera: boolean;
  requireProjector: boolean;
  requireSmartBoard: boolean;
  preferredRoomId: number | null;
  priority: number;
  notes: string | null;
};

export type CreateRoomAllocationRuleRequest = Omit<RoomAllocationRuleDto, "id">;
export type UpdateRoomAllocationRuleRequest = RoomAllocationRuleDto;

export type FacultyAvailabilityDto = {
  id: number;
  staffId: number;
  academicYearId: number;
  availabilityType: FacultyAvailabilityType;
  startDate: string;
  endDate: string;
  startSlotId: number | null;
  endSlotId: number | null;
  reason: string | null;
  remarks: string | null;
};

export type CreateFacultyAvailabilityRequest = Omit<FacultyAvailabilityDto, "id">;
export type UpdateFacultyAvailabilityRequest = FacultyAvailabilityDto;

export type RoomAvailabilityDto = {
  id: number;
  roomId: number;
  academicYearId: number;
  availabilityType: RoomAvailabilityType;
  startDate: string;
  endDate: string;
  startSlotId: number | null;
  endSlotId: number | null;
  reason: string | null;
};

export type CreateRoomAvailabilityRequest = Omit<RoomAvailabilityDto, "id">;
export type UpdateRoomAvailabilityRequest = RoomAvailabilityDto;

export type SubjectCategoryDto = {
  id: number;
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
};

export type CreateSubjectCategoryRequest = Omit<SubjectCategoryDto, "id">;
export type UpdateSubjectCategoryRequest = SubjectCategoryDto;

export type UpdateSubjectSchedulingCategoryRequest = {
  subjectId: number;
  subjectCategoryId: number;
  requiresRoomType: RoomType | null;
  defaultDurationMinutes: number | null;
  requiresLabEquipment: boolean;
};

export type TimeSlotTemplateDto = {
  id: number;
  name: string;
  description: string | null;
  templateType: TimeSlotTemplateType;
  isDefault: boolean;
  setCount: number;
  slotCount: number;
};

export type TimeSlotTemplatePreviewDto = {
  id: number;
  name: string;
  description: string | null;
  templateType: TimeSlotTemplateType;
  isDefault: boolean;
  sets: TimeSlotSetDto[];
  slots: TimeSlotDto[];
};

export type CreateTimeSlotTemplateRequest = {
  name: string;
  description?: string | null;
  templateType: TimeSlotTemplateType;
  isDefault: boolean;
};

export type UpdateTimeSlotTemplateRequest = CreateTimeSlotTemplateRequest & { id: number };

export type CloneTimeSlotTemplateRequest = {
  sourceTemplateId: number;
  name: string;
  description?: string | null;
  templateType: TimeSlotTemplateType;
  isDefault: boolean;
};

export type NamedCountDto = {
  name: string;
  count: number;
};

export type FacultyTeachingPreferenceDto = {
  id: number;
  staffId: number;
  academicYearId: number;
  preferredCampusId: number | null;
  preferredBuildingId: number | null;
  preferredFloorId: number | null;
  preferredRoomId: number | null;
  preferredSubjectId: number | null;
  preferredDepartmentId: number | null;
  preferredCourseId: number | null;
  preferredGroupId: number | null;
  preferredSemesterId: number | null;
  preferredFirstPeriod: number | null;
  preferredLastPeriod: number | null;
  preferredWorkingDaysFlags: number;
  maximumContinuousClasses: number;
  minimumBreakBetweenClasses: number;
  preferredTeachingMode: PreferredTeachingMode;
  priority: number;
  remarks: string | null;
  isActive: boolean;
};

export type CreateFacultyTeachingPreferenceRequest = Omit<FacultyTeachingPreferenceDto, "id">;
export type UpdateFacultyTeachingPreferenceRequest = FacultyTeachingPreferenceDto;

export type RoomFeatureDto = {
  id: number;
  code: string;
  name: string;
  category: string;
  sortOrder: number;
  isActive: boolean;
};

export type CreateRoomFeatureRequest = Omit<RoomFeatureDto, "id">;
export type UpdateRoomFeatureRequest = RoomFeatureDto;

export type RoomFeatureAssignmentDto = {
  id: number;
  roomId: number;
  roomFeatureId: number;
  featureCode: string;
  featureName: string;
  featureCategory: string;
};

export type AssignRoomFeatureRequest = {
  roomFeatureId: number;
};

export type CloneRoomFeatureAssignmentsRequest = {
  fromRoomId: number;
  toRoomId: number;
};

export type SubjectDeliveryTypeDto = {
  id: number;
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
};

export type CreateSubjectDeliveryTypeRequest = Omit<SubjectDeliveryTypeDto, "id">;
export type UpdateSubjectDeliveryTypeRequest = SubjectDeliveryTypeDto;

export type UpdateSubjectDeliveryFieldsRequest = {
  subjectId: number;
  deliveryTypeId: number;
  preferredRoomFeatureId: number | null;
  requiresAttendance: boolean;
  expectedCapacity: number | null;
  requiresRoomType: RoomType | null;
};

export type HolidayTypeCatalogDto = {
  id: number;
  code: string;
  name: string;
  colour: string;
  priority: number;
  sortOrder: number;
  isActive: boolean;
};

export type CreateHolidayTypeCatalogRequest = Omit<HolidayTypeCatalogDto, "id">;
export type UpdateHolidayTypeCatalogRequest = HolidayTypeCatalogDto;

export type SchedulingValidationReportDto = {
  missingFacultyPreferencesCount: number;
  subjectsMissingDeliveryTypeCount: number;
  duplicateRoomFeatureAssignmentCount: number;
  roomsWithoutFeaturesCount: number;
  holidaysMissingCatalogTypeCount: number;
};

export type SchedulingDashboardDto = {
  academicYearCount: number;
  campusCount: number;
  buildingCount: number;
  roomCount: number;
  subjectCount: number;
  facultyCount: number;
  totalWeeklyHours: number;
  totalRoomCapacity: number;
  timeSlotSetCount: number;
  facultyWorkloadCount: number;
  subjectAllocationCount: number;
  roomRuleCount: number;
  holidayCount: number;
  departmentCount: number;
  facultyAvailabilityCount: number;
  roomAvailabilityCount: number;
  subjectCategoryCount: number;
  timeSlotTemplateCount: number;
  facultyUnavailableCount: number;
  roomsBlockedCount: number;
  subjectsMissingCategoryCount: number;
  unusedTemplateCount: number;
  departmentsWithoutAllocationCount: number;
  facultyPreferenceCount: number;
  roomFeatureCount: number;
  roomFeatureAssignmentCount: number;
  subjectDeliveryTypeCount: number;
  holidayTypeCatalogCount: number;
  missingFacultyPreferencesCount: number;
  roomsWithFeaturesCount: number;
  roomsWithoutFeaturesCount: number;
  roomFeatureCoveragePercent: number;
  holidayDistribution: NamedCountDto[];
  deliveryTypeDistribution: NamedCountDto[];
};

// --- Academic years ---

export const listAcademicYears = () => api.get<AcademicYearDto[]>("/scheduling/academic-years");
export const getAcademicYear = (id: number) => api.get<AcademicYearDto>(`/scheduling/academic-years/${id}`);
export const createAcademicYear = (payload: CreateAcademicYearRequest) =>
  api.post<AcademicYearDto>("/scheduling/academic-years", payload);
export const updateAcademicYear = (id: number, payload: UpdateAcademicYearRequest) =>
  api.put<AcademicYearDto>(`/scheduling/academic-years/${id}`, payload);
export const deleteAcademicYear = (id: number) => api.delete(`/scheduling/academic-years/${id}`);
export const setCurrentAcademicYear = (id: number) =>
  api.post(`/scheduling/academic-years/${id}/set-current`);
export const cloneAcademicYear = (payload: ClonePreviousYearRequest) =>
  api.post<AcademicYearDto>("/scheduling/academic-years/clone", payload);

// --- Working days ---

export const listWorkingDays = (academicYearId: number) =>
  api.get<WorkingDayDto[]>("/scheduling/working-days", { params: { academicYearId } });
export const upsertWorkingDay = (payload: UpsertWorkingDayRequest) =>
  api.post<WorkingDayDto>("/scheduling/working-days", payload);
export const deleteWorkingDay = (id: number) => api.delete(`/scheduling/working-days/${id}`);

// --- Holidays ---

export const listHolidays = (academicYearId?: number) =>
  api.get<HolidayDto[]>("/scheduling/holidays", { params: academicYearId ? { academicYearId } : {} });
export const getHoliday = (id: number) => api.get<HolidayDto>(`/scheduling/holidays/${id}`);
export const createHoliday = (payload: CreateHolidayRequest) =>
  api.post<HolidayDto>("/scheduling/holidays", payload);
export const updateHoliday = (id: number, payload: UpdateHolidayRequest) =>
  api.put<HolidayDto>(`/scheduling/holidays/${id}`, payload);
export const deleteHoliday = (id: number) => api.delete(`/scheduling/holidays/${id}`);

// --- Campus facilities ---

export const listCampuses = () => api.get<CampusDto[]>("/scheduling/campuses");
export const createCampus = (payload: CreateCampusRequest) => api.post<CampusDto>("/scheduling/campuses", payload);
export const updateCampus = (id: number, payload: UpdateCampusRequest) =>
  api.put<CampusDto>(`/scheduling/campuses/${id}`, payload);
export const deleteCampus = (id: number) => api.delete(`/scheduling/campuses/${id}`);

export const listBuildings = (campusId?: number) =>
  api.get<BuildingDto[]>("/scheduling/buildings", { params: campusId ? { campusId } : {} });
export const createBuilding = (payload: CreateBuildingRequest) =>
  api.post<BuildingDto>("/scheduling/buildings", payload);
export const updateBuilding = (id: number, payload: UpdateBuildingRequest) =>
  api.put<BuildingDto>(`/scheduling/buildings/${id}`, payload);
export const deleteBuilding = (id: number) => api.delete(`/scheduling/buildings/${id}`);

export const listFloors = (buildingId?: number) =>
  api.get<FloorDto[]>("/scheduling/floors", { params: buildingId ? { buildingId } : {} });
export const createFloor = (payload: CreateFloorRequest) => api.post<FloorDto>("/scheduling/floors", payload);
export const updateFloor = (id: number, payload: UpdateFloorRequest) =>
  api.put<FloorDto>(`/scheduling/floors/${id}`, payload);
export const deleteFloor = (id: number) => api.delete(`/scheduling/floors/${id}`);

export const searchRooms = (query: RoomSearchQuery) =>
  api.get<PagedRoomsResult>("/scheduling/rooms", { params: query });
export const createRoom = (payload: CreateRoomRequest) => api.post<RoomDto>("/scheduling/rooms", payload);
export const updateRoom = (id: number, payload: UpdateRoomRequest) =>
  api.put<RoomDto>(`/scheduling/rooms/${id}`, payload);
export const deleteRoom = (id: number) => api.delete(`/scheduling/rooms/${id}`);

// --- Time slots ---

export const listTimeSlotSets = (academicYearId?: number) =>
  api.get<TimeSlotSetDto[]>("/scheduling/time-slot-sets", { params: academicYearId ? { academicYearId } : {} });
export const createTimeSlotSet = (payload: CreateTimeSlotSetRequest) =>
  api.post<TimeSlotSetDto>("/scheduling/time-slot-sets", payload);
export const updateTimeSlotSet = (id: number, payload: UpdateTimeSlotSetRequest) =>
  api.put<TimeSlotSetDto>(`/scheduling/time-slot-sets/${id}`, payload);
export const deleteTimeSlotSet = (id: number) => api.delete(`/scheduling/time-slot-sets/${id}`);
export const cloneTimeSlotSet = (payload: CloneTimeSlotSetRequest) =>
  api.post<TimeSlotSetDto>("/scheduling/time-slot-sets/clone", payload);

export const listTimeSlots = (timeSlotSetId: number) =>
  api.get<TimeSlotDto[]>("/scheduling/time-slots", { params: { timeSlotSetId } });
export const createTimeSlot = (payload: CreateTimeSlotRequest) =>
  api.post<TimeSlotDto>("/scheduling/time-slots", payload);
export const updateTimeSlot = (id: number, payload: UpdateTimeSlotRequest) =>
  api.put<TimeSlotDto>(`/scheduling/time-slots/${id}`, payload);
export const deleteTimeSlot = (id: number) => api.delete(`/scheduling/time-slots/${id}`);

// --- Faculty workloads ---

export const getFacultyWorkload = (staffId: number) =>
  api.get<FacultyWorkloadDto>(`/scheduling/faculty-workloads/${staffId}`);
export const upsertFacultyWorkload = (payload: UpsertFacultyWorkloadRequest) =>
  api.put<FacultyWorkloadDto>("/scheduling/faculty-workloads", payload);
export const deleteFacultyWorkload = (staffId: number) =>
  api.delete(`/scheduling/faculty-workloads/${staffId}`);
export const upsertFacultyDayPreference = (payload: UpsertFacultyDayPreferenceRequest) =>
  api.post<FacultyDayPreferenceDto>("/scheduling/faculty-workloads/day-preferences", payload);
export const deleteFacultyDayPreference = (id: number) =>
  api.delete(`/scheduling/faculty-workloads/day-preferences/${id}`);
export const upsertFacultyTimeSlotPreference = (payload: UpsertFacultyTimeSlotPreferenceRequest) =>
  api.post<FacultyTimeSlotPreferenceDto>("/scheduling/faculty-workloads/time-slot-preferences", payload);
export const deleteFacultyTimeSlotPreference = (id: number) =>
  api.delete(`/scheduling/faculty-workloads/time-slot-preferences/${id}`);

// --- Subject allocations ---

export const listSubjectAllocations = (params?: {
  academicYearId?: number;
  staffId?: number;
  departmentId?: number;
}) => api.get<SubjectAllocationDto[]>("/scheduling/subject-allocations", { params });
export const createSubjectAllocation = (payload: CreateSubjectAllocationRequest) =>
  api.post<SubjectAllocationDto>("/scheduling/subject-allocations", payload);
export const updateSubjectAllocation = (id: number, payload: UpdateSubjectAllocationRequest) =>
  api.put<SubjectAllocationDto>(`/scheduling/subject-allocations/${id}`, payload);
export const deleteSubjectAllocation = (id: number) => api.delete(`/scheduling/subject-allocations/${id}`);

// --- Room rules ---

export const listRoomRules = (academicYearId?: number) =>
  api.get<RoomAllocationRuleDto[]>("/scheduling/room-rules", { params: academicYearId ? { academicYearId } : {} });
export const createRoomRule = (payload: CreateRoomAllocationRuleRequest) =>
  api.post<RoomAllocationRuleDto>("/scheduling/room-rules", payload);
export const updateRoomRule = (id: number, payload: UpdateRoomAllocationRuleRequest) =>
  api.put<RoomAllocationRuleDto>(`/scheduling/room-rules/${id}`, payload);
export const deleteRoomRule = (id: number) => api.delete(`/scheduling/room-rules/${id}`);

// --- Faculty availability ---

export const listFacultyAvailability = (params?: { academicYearId?: number; staffId?: number }) =>
  api.get<FacultyAvailabilityDto[]>("/scheduling/faculty-availability", { params });
export const getFacultyAvailability = (id: number) =>
  api.get<FacultyAvailabilityDto>(`/scheduling/faculty-availability/${id}`);
export const createFacultyAvailability = (payload: CreateFacultyAvailabilityRequest) =>
  api.post<FacultyAvailabilityDto>("/scheduling/faculty-availability", payload);
export const updateFacultyAvailability = (id: number, payload: UpdateFacultyAvailabilityRequest) =>
  api.put<FacultyAvailabilityDto>(`/scheduling/faculty-availability/${id}`, payload);
export const deleteFacultyAvailability = (id: number) => api.delete(`/scheduling/faculty-availability/${id}`);

// --- Room availability ---

export const listRoomAvailability = (params?: { academicYearId?: number; roomId?: number }) =>
  api.get<RoomAvailabilityDto[]>("/scheduling/room-availability", { params });
export const getRoomAvailability = (id: number) =>
  api.get<RoomAvailabilityDto>(`/scheduling/room-availability/${id}`);
export const createRoomAvailability = (payload: CreateRoomAvailabilityRequest) =>
  api.post<RoomAvailabilityDto>("/scheduling/room-availability", payload);
export const updateRoomAvailability = (id: number, payload: UpdateRoomAvailabilityRequest) =>
  api.put<RoomAvailabilityDto>(`/scheduling/room-availability/${id}`, payload);
export const deleteRoomAvailability = (id: number) => api.delete(`/scheduling/room-availability/${id}`);

// --- Subject categories ---

export const listSubjectCategories = (isActive?: boolean) =>
  api.get<SubjectCategoryDto[]>("/scheduling/subject-categories", {
    params: isActive !== undefined ? { isActive } : {},
  });
export const getSubjectCategory = (id: number) =>
  api.get<SubjectCategoryDto>(`/scheduling/subject-categories/${id}`);
export const createSubjectCategory = (payload: CreateSubjectCategoryRequest) =>
  api.post<SubjectCategoryDto>("/scheduling/subject-categories", payload);
export const updateSubjectCategory = (id: number, payload: UpdateSubjectCategoryRequest) =>
  api.put<SubjectCategoryDto>(`/scheduling/subject-categories/${id}`, payload);
export const deleteSubjectCategory = (id: number) => api.delete(`/scheduling/subject-categories/${id}`);
export const updateSubjectSchedulingCategory = (
  subjectId: number,
  payload: UpdateSubjectSchedulingCategoryRequest,
) => api.put(`/scheduling/subject-categories/subjects/${subjectId}`, payload);

// --- Time slot templates ---

export const listTimeSlotTemplates = () => api.get<TimeSlotTemplateDto[]>("/scheduling/time-slot-templates");
export const getTimeSlotTemplate = (id: number) =>
  api.get<TimeSlotTemplateDto>(`/scheduling/time-slot-templates/${id}`);
export const previewTimeSlotTemplate = (id: number) =>
  api.get<TimeSlotTemplatePreviewDto>(`/scheduling/time-slot-templates/${id}/preview`);
export const createTimeSlotTemplate = (payload: CreateTimeSlotTemplateRequest) =>
  api.post<TimeSlotTemplateDto>("/scheduling/time-slot-templates", payload);
export const updateTimeSlotTemplate = (id: number, payload: UpdateTimeSlotTemplateRequest) =>
  api.put<TimeSlotTemplateDto>(`/scheduling/time-slot-templates/${id}`, payload);
export const deleteTimeSlotTemplate = (id: number) => api.delete(`/scheduling/time-slot-templates/${id}`);
export const cloneTimeSlotTemplate = (payload: CloneTimeSlotTemplateRequest) =>
  api.post<TimeSlotTemplateDto>("/scheduling/time-slot-templates/clone", payload);
export const setDefaultTimeSlotTemplate = (id: number) =>
  api.post<TimeSlotTemplateDto>(`/scheduling/time-slot-templates/${id}/set-default`);

// --- Dashboard ---

export const getSchedulingDashboard = () => api.get<SchedulingDashboardDto>("/scheduling/dashboard");

// --- Faculty teaching preferences ---

export const listFacultyTeachingPreferences = (params?: {
  academicYearId?: number;
  staffId?: number;
  isActive?: boolean;
}) => api.get<FacultyTeachingPreferenceDto[]>("/scheduling/faculty-preferences", { params });

export const getFacultyTeachingPreference = (id: number) =>
  api.get<FacultyTeachingPreferenceDto>(`/scheduling/faculty-preferences/${id}`);

export const createFacultyTeachingPreference = (payload: CreateFacultyTeachingPreferenceRequest) =>
  api.post<FacultyTeachingPreferenceDto>("/scheduling/faculty-preferences", payload);

export const updateFacultyTeachingPreference = (id: number, payload: UpdateFacultyTeachingPreferenceRequest) =>
  api.put<FacultyTeachingPreferenceDto>(`/scheduling/faculty-preferences/${id}`, payload);

export const deleteFacultyTeachingPreference = (id: number) =>
  api.delete(`/scheduling/faculty-preferences/${id}`);

// --- Room features ---

export const listRoomFeatures = (params?: { category?: string; isActive?: boolean }) =>
  api.get<RoomFeatureDto[]>("/scheduling/room-features", { params });

export const getRoomFeature = (id: number) => api.get<RoomFeatureDto>(`/scheduling/room-features/${id}`);

export const createRoomFeature = (payload: CreateRoomFeatureRequest) =>
  api.post<RoomFeatureDto>("/scheduling/room-features", payload);

export const updateRoomFeature = (id: number, payload: UpdateRoomFeatureRequest) =>
  api.put<RoomFeatureDto>(`/scheduling/room-features/${id}`, payload);

export const deleteRoomFeature = (id: number) => api.delete(`/scheduling/room-features/${id}`);

export const cloneRoomFeatureAssignments = (payload: CloneRoomFeatureAssignmentsRequest) =>
  api.post<RoomFeatureAssignmentDto[]>("/scheduling/room-features/clone-assignments", payload);

export const listRoomFeatureAssignments = (roomId: number) =>
  api.get<RoomFeatureAssignmentDto[]>(`/scheduling/rooms/${roomId}/features`);

export const assignRoomFeature = (roomId: number, payload: AssignRoomFeatureRequest) =>
  api.post<RoomFeatureAssignmentDto>(`/scheduling/rooms/${roomId}/features`, payload);

export const unassignRoomFeature = (roomId: number, roomFeatureId: number) =>
  api.delete(`/scheduling/rooms/${roomId}/features/${roomFeatureId}`);

// --- Subject delivery types ---

export const listSubjectDeliveryTypes = (isActive?: boolean) =>
  api.get<SubjectDeliveryTypeDto[]>("/scheduling/subject-delivery-types", {
    params: isActive !== undefined ? { isActive } : {},
  });

export const getSubjectDeliveryType = (id: number) =>
  api.get<SubjectDeliveryTypeDto>(`/scheduling/subject-delivery-types/${id}`);

export const createSubjectDeliveryType = (payload: CreateSubjectDeliveryTypeRequest) =>
  api.post<SubjectDeliveryTypeDto>("/scheduling/subject-delivery-types", payload);

export const updateSubjectDeliveryType = (id: number, payload: UpdateSubjectDeliveryTypeRequest) =>
  api.put<SubjectDeliveryTypeDto>(`/scheduling/subject-delivery-types/${id}`, payload);

export const deleteSubjectDeliveryType = (id: number) => api.delete(`/scheduling/subject-delivery-types/${id}`);

export const updateSubjectDeliveryFields = (subjectId: number, payload: UpdateSubjectDeliveryFieldsRequest) =>
  api.put(`/scheduling/subject-delivery-types/subjects/${subjectId}`, payload);

// --- Holiday type catalog ---

export const listHolidayTypes = (isActive?: boolean) =>
  api.get<HolidayTypeCatalogDto[]>("/scheduling/holiday-types", {
    params: isActive !== undefined ? { isActive } : {},
  });

export const getHolidayType = (id: number) => api.get<HolidayTypeCatalogDto>(`/scheduling/holiday-types/${id}`);

export const createHolidayType = (payload: CreateHolidayTypeCatalogRequest) =>
  api.post<HolidayTypeCatalogDto>("/scheduling/holiday-types", payload);

export const updateHolidayType = (id: number, payload: UpdateHolidayTypeCatalogRequest) =>
  api.put<HolidayTypeCatalogDto>(`/scheduling/holiday-types/${id}`, payload);

export const deleteHolidayType = (id: number) => api.delete(`/scheduling/holiday-types/${id}`);

// --- Validation report ---

export const getSchedulingValidationReport = () =>
  api.get<SchedulingValidationReportDto>("/scheduling/validation-report");

// --- Timetables (Phase 2) ---

export const TimetableStatus = {
  Draft: 1,
  Locked: 2,
  Published: 3,
  Archived: 4,
} as const;
export type TimetableStatus = (typeof TimetableStatus)[keyof typeof TimetableStatus];

export type TimetableDto = {
  id: number;
  name: string;
  code: string | null;
  academicYearId: number;
  academicYearName: string | null;
  departmentId: number | null;
  departmentName: string | null;
  timeSlotSetId: number | null;
  timeSlotSetName: string | null;
  scheduleVersionId?: number | null;
  status: TimetableStatus;
  notes: string | null;
  entryCount: number;
  isFrozen?: boolean;
  frozenDate?: string | null;
  frozenBy?: number | null;
  freezeReason?: string | null;
  unlockDate?: string | null;
  unlockedBy?: number | null;
  unlockReason?: string | null;
  archiveReasonId?: number | null;
  archiveReasonName?: string | null;
  archiveComments?: string | null;
  archivedBy?: number | null;
  archivedDate?: string | null;
  referenceVersionId?: number | null;
};

export type TimetableEntryDto = {
  id: number;
  timetableId: number;
  dayOfWeek: number;
  timeSlotId: number;
  timeSlotName: string | null;
  startTime: string | null;
  endTime: string | null;
  subjectAllocationId: number;
  staffId: number;
  staffName: string | null;
  roomId: number;
  roomName: string | null;
  departmentId: number;
  departmentName: string | null;
  courseId: number;
  courseName: string | null;
  groupId: number;
  groupName: string | null;
  semesterId: number;
  semesterName: string | null;
  subjectId: number;
  subjectName: string | null;
  remarks: string | null;
};

export type CreateTimetableRequest = {
  name: string;
  code?: string | null;
  academicYearId: number;
  departmentId?: number | null;
  timeSlotSetId?: number | null;
  notes?: string | null;
};

export type UpdateTimetableRequest = CreateTimetableRequest & { id: number };

export type CreateTimetableEntryRequest = {
  dayOfWeek: number;
  timeSlotId: number;
  subjectAllocationId: number;
  roomId?: number | null;
  remarks?: string | null;
};

export type UpdateTimetableEntryRequest = CreateTimetableEntryRequest & { id: number };

export type UpsertTimetableEntryRequest = {
  id?: number | null;
  dayOfWeek: number;
  timeSlotId: number;
  subjectAllocationId: number;
  roomId?: number | null;
  remarks?: string | null;
};

export type BulkPasteEntriesRequest = {
  entries: UpsertTimetableEntryRequest[];
};

export type MoveTimetableEntryRequest = {
  dayOfWeek: number;
  timeSlotId: number;
  roomId?: number | null;
};

export type CopyTimetableEntryRequest = {
  targetDayOfWeek: number;
  targetTimeSlotId: number;
  roomId?: number | null;
};

export type TimetableGridDto = {
  timetable: TimetableDto;
  entries: TimetableEntryDto[];
  timeSlots: TimeSlotDto[];
};

export type TimetableProjectionDto = {
  timetable: TimetableDto;
  entries: TimetableEntryDto[];
};

export type TimetableDashboardDto = {
  draftTimetableCount: number;
  lockedCount: number;
  scheduledPeriodCount: number;
  departmentsWithTimetable: number;
  facultyScheduledCount: number;
  roomsScheduledCount: number;
  dailyDistribution: NamedCountDto[];
  facultyLoad: NamedCountDto[];
  roomUsage: NamedCountDto[];
};

export type TimetableExportView = "faculty" | "student" | "room" | "department";

export type TimetableExportParams = {
  view: TimetableExportView;
  staffId?: number;
  courseId?: number;
  groupId?: number;
  semesterId?: number;
  roomId?: number;
  departmentId?: number;
};

export const listTimetables = (params?: {
  academicYearId?: number;
  status?: TimetableStatus;
  departmentId?: number;
  includeArchived?: boolean;
}) => api.get<TimetableDto[]>("/scheduling/timetables", { params });

export const getTimetableDashboard = (academicYearId?: number) =>
  api.get<TimetableDashboardDto>("/scheduling/timetables/dashboard", {
    params: academicYearId ? { academicYearId } : {},
  });

export const getTimetable = (id: number) => api.get<TimetableDto>(`/scheduling/timetables/${id}`);

export const getTimetableGrid = (id: number) =>
  api.get<TimetableGridDto>(`/scheduling/timetables/${id}/grid`);

export const getTimetableFacultyProjection = (id: number, staffId: number) =>
  api.get<TimetableProjectionDto>(`/scheduling/timetables/${id}/faculty/${staffId}`);

export const getTimetableStudentProjection = (
  id: number,
  params: { courseId: number; groupId: number; semesterId: number },
) => api.get<TimetableProjectionDto>(`/scheduling/timetables/${id}/student`, { params });

export const getTimetableRoomProjection = (id: number, roomId: number) =>
  api.get<TimetableProjectionDto>(`/scheduling/timetables/${id}/room/${roomId}`);

export const getTimetableDepartmentProjection = (id: number, departmentId: number) =>
  api.get<TimetableProjectionDto>(`/scheduling/timetables/${id}/department/${departmentId}`);

export const createTimetable = (payload: CreateTimetableRequest) =>
  api.post<TimetableDto>("/scheduling/timetables", payload);

export const updateTimetable = (id: number, payload: UpdateTimetableRequest) =>
  api.put<TimetableDto>(`/scheduling/timetables/${id}`, payload);

export const deleteTimetable = (id: number) => api.delete(`/scheduling/timetables/${id}`);

export const lockTimetable = (id: number) =>
  api.post<TimetableDto>(`/scheduling/timetables/${id}/lock`);

export const unlockTimetable = (id: number) =>
  api.post<TimetableDto>(`/scheduling/timetables/${id}/unlock`);

export const createTimetableEntry = (timetableId: number, payload: CreateTimetableEntryRequest) =>
  api.post<TimetableEntryDto>(`/scheduling/timetables/${timetableId}/entries`, payload);

export const updateTimetableEntry = (entryId: number, payload: UpdateTimetableEntryRequest) =>
  api.put<TimetableEntryDto>(`/scheduling/timetables/entries/${entryId}`, payload);

export const deleteTimetableEntry = (entryId: number) =>
  api.delete(`/scheduling/timetables/entries/${entryId}`);

export const moveTimetableEntry = (entryId: number, payload: MoveTimetableEntryRequest) =>
  api.post<TimetableEntryDto>(`/scheduling/timetables/entries/${entryId}/move`, payload);

export const copyTimetableEntry = (entryId: number, payload: CopyTimetableEntryRequest) =>
  api.post<TimetableEntryDto>(`/scheduling/timetables/entries/${entryId}/copy`, payload);

export const duplicateTimetableEntry = (entryId: number) =>
  api.post<TimetableEntryDto>(`/scheduling/timetables/entries/${entryId}/duplicate`);

export const bulkTimetableEntries = (timetableId: number, payload: BulkPasteEntriesRequest) =>
  api.post<TimetableEntryDto[]>(`/scheduling/timetables/${timetableId}/entries/bulk`, payload);

export const exportTimetableExcel = (id: number, params: TimetableExportParams) =>
  api.get<Blob>(`/scheduling/timetables/${id}/export/excel`, {
    params,
    responseType: "blob",
  });

// --- Timetable governance (Phase 2A) ---

export const ScheduleVersionStatus = {
  Draft: 1,
  UnderReview: 2,
  Approved: 3,
  Published: 4,
  Archived: 5,
} as const;
export type ScheduleVersionStatus = (typeof ScheduleVersionStatus)[keyof typeof ScheduleVersionStatus];

export const TimetableApprovalRequestStatus = {
  Pending: 1,
  InReview: 2,
  Approved: 3,
  Rejected: 4,
  Returned: 5,
  Cancelled: 6,
} as const;
export type TimetableApprovalRequestStatus =
  (typeof TimetableApprovalRequestStatus)[keyof typeof TimetableApprovalRequestStatus];

export const ApprovalDecision = {
  Approved: 1,
  Rejected: 2,
  Returned: 3,
} as const;
export type ApprovalDecision = (typeof ApprovalDecision)[keyof typeof ApprovalDecision];

export const TimetableCloneJobType = {
  Day: 1,
  Week: 2,
  Semester: 3,
  AcademicYear: 4,
  Department: 5,
  Course: 6,
  Group: 7,
  Faculty: 8,
  Room: 9,
} as const;
export type TimetableCloneJobType = (typeof TimetableCloneJobType)[keyof typeof TimetableCloneJobType];

export const TimetableCloneJobStatus = {
  Queued: 1,
  Running: 2,
  Completed: 3,
  Failed: 4,
} as const;
export type TimetableCloneJobStatus = (typeof TimetableCloneJobStatus)[keyof typeof TimetableCloneJobStatus];

export const TimetableChangeOperation = {
  Create: 1,
  Update: 2,
  Delete: 3,
  Move: 4,
  Copy: 5,
  Clone: 6,
  Publish: 7,
  Archive: 8,
  Lock: 9,
  Unlock: 10,
  Freeze: 11,
  Unfreeze: 12,
} as const;
export type TimetableChangeOperation = (typeof TimetableChangeOperation)[keyof typeof TimetableChangeOperation];

export const VersionDifferenceKind = { Added: 1, Removed: 2, Modified: 3 } as const;
export type VersionDifferenceKind = (typeof VersionDifferenceKind)[keyof typeof VersionDifferenceKind];

export const VersionDifferenceCategory = {
  AddedEntry: 1,
  RemovedEntry: 2,
  FacultyAssignment: 3,
  RoomAssignment: 4,
  SubjectAssignment: 5,
  PeriodChange: 6,
  TimeSlotChange: 7,
  Other: 8,
} as const;
export type VersionDifferenceCategory =
  (typeof VersionDifferenceCategory)[keyof typeof VersionDifferenceCategory];

export type ScheduleVersionDto = {
  id: number;
  academicYearId: number;
  academicYearName: string | null;
  academicTermId: number | null;
  academicTermName: string | null;
  versionNumber: number;
  versionName: string;
  status: ScheduleVersionStatus;
  isCurrent: boolean;
  publishedDate: string | null;
  publishedBy: number | null;
  archivedDate: string | null;
  archivedBy: number | null;
  archiveReasonId?: number | null;
  archiveReasonName?: string | null;
  archiveComments?: string | null;
  referenceVersionId?: number | null;
  parentVersionId: number | null;
  remarks: string | null;
  timetableCount: number;
};

export type CreateScheduleVersionRequest = {
  academicYearId: number;
  academicTermId?: number | null;
  versionName: string;
  remarks?: string | null;
  createEmptyTimetable?: boolean;
  timetableName?: string | null;
  departmentId?: number | null;
  timeSlotSetId?: number | null;
};

export type DuplicateScheduleVersionRequest = {
  sourceVersionId: number;
  versionName: string;
  remarks?: string | null;
  cloneAllTimetables?: boolean;
};

export type ScheduleVersionHistoryDto = {
  versionId: number;
  versionName: string;
  versionNumber: number;
  status: ScheduleVersionStatus;
  createdDate: string;
  createdBy: number | null;
  publishedDate: string | null;
  archivedDate: string | null;
};

export type TimetableApprovalStepDto = {
  id: number;
  stepOrder: number;
  roleKey: string;
  status: TimetableApprovalRequestStatus;
  assignedTo: number | null;
  decidedBy: number | null;
  decidedUtc: string | null;
  decision: ApprovalDecision | null;
  comments: string | null;
};

export type TimetableApprovalRequestDto = {
  id: number;
  scheduleVersionId: number;
  versionName: string | null;
  timetableId: number;
  timetableName: string | null;
  status: TimetableApprovalRequestStatus;
  submittedBy: number;
  submittedUtc: string;
  currentStepOrder: number;
  steps: TimetableApprovalStepDto[];
};

export type SubmitForReviewRequest = {
  timetableId: number;
  comments?: string | null;
};

export type DecideApprovalStepRequest = {
  requestId: number;
  stepOrder: number;
  decision: ApprovalDecision;
  comments?: string | null;
  decisionNotes?: string | null;
  reviewerRemarks?: string | null;
};

export type TimetableApprovalHistoryDto = {
  stepOrder: number;
  actorUserId: number;
  decision: ApprovalDecision | null;
  comments: string | null;
  occurredUtc: string;
  oldStatus?: TimetableApprovalRequestStatus | null;
  newStatus?: TimetableApprovalRequestStatus | null;
};

export type ApprovalCommentDto = {
  id: number;
  requestId: number;
  actorUserId: number;
  comment: string;
  occurredUtc: string;
  isDecisionNote: boolean;
};

export type DecisionHistoryDto = {
  id: number;
  requestId: number;
  stepOrder: number;
  actorUserId: number;
  decision: ApprovalDecision | null;
  action: string;
  comment: string | null;
  decisionNotes: string | null;
  reviewerRemarks: string | null;
  oldStatus: TimetableApprovalRequestStatus | null;
  newStatus: TimetableApprovalRequestStatus | null;
  occurredUtc: string;
};

export type TimetableApprovalTimelineDto = {
  requestId: number;
  status: TimetableApprovalRequestStatus;
  events: TimetableApprovalHistoryDto[];
  comments?: ApprovalCommentDto[];
  decisions?: DecisionHistoryDto[];
};

export type CompareScheduleVersionsRequest = {
  leftVersionId: number;
  rightVersionId: number;
  departmentId?: number | null;
  search?: string | null;
  kindFilter?: VersionDifferenceKind | null;
  categoryFilter?: VersionDifferenceCategory | null;
};

export type ComparisonSummaryDto = {
  added: number;
  modified: number;
  removed: number;
  facultyChanges: number;
  roomChanges: number;
  subjectChanges: number;
  periodChanges: number;
  timeSlotChanges: number;
};

export type VersionDifferenceDto = {
  kind: VersionDifferenceKind;
  category: VersionDifferenceCategory;
  summary: string;
  leftEntryId: number | null;
  rightEntryId: number | null;
  leftTimetableId: number | null;
  rightTimetableId: number | null;
  dayOfWeek: number | null;
  timeSlotId: number | null;
  subjectId: number | null;
  subjectName: string | null;
  staffId: number | null;
  staffName: string | null;
  roomId: number | null;
  roomName: string | null;
  leftValue: string | null;
  rightValue: string | null;
  changedFields: string[];
};

export type VersionComparisonDto = {
  leftVersionId: number;
  leftVersionName: string;
  leftStatus: ScheduleVersionStatus;
  rightVersionId: number;
  rightVersionName: string;
  rightStatus: ScheduleVersionStatus;
  summary: ComparisonSummaryDto;
  differences: VersionDifferenceDto[];
  grouped: Record<string, VersionDifferenceDto[]>;
};

export type ArchiveReasonDto = {
  id: number;
  code: number;
  name: string;
  description: string | null;
  sortOrder: number;
};

export type ArchiveLifecycleItemDto = {
  timetableId: number;
  timetableName: string;
  archiveReasonName: string | null;
  archiveReasonCode: number | null;
  comments: string | null;
  archivedBy: number | null;
  archivedDate: string | null;
  referenceVersionId: number | null;
  referenceVersionName: string | null;
};

export type TimetableCloneJobDto = {
  id: number;
  jobType: TimetableCloneJobType;
  sourceTimetableId: number;
  targetTimetableId: number | null;
  payloadJson: string | null;
  status: TimetableCloneJobStatus;
  progressPercent: number;
  summary: string | null;
  error: string | null;
  requestedBy: number;
  startedUtc: string | null;
  completedUtc: string | null;
};

export type EnqueueTimetableCloneRequest = {
  jobType: TimetableCloneJobType;
  sourceTimetableId: number;
  targetTimetableId?: number | null;
  sourceDayOfWeek?: number | null;
  targetDayOfWeek?: number | null;
  targetScheduleVersionId?: number | null;
  targetTimetableName?: string | null;
  departmentId?: number | null;
  courseId?: number | null;
  groupId?: number | null;
  staffId?: number | null;
  roomId?: number | null;
  executeSynchronously?: boolean;
};

export type SoftWarningDto = {
  code: string;
  severity: string;
  message: string;
  entryId: number | null;
  staffId: number | null;
  roomId: number | null;
  dayOfWeek: number | null;
  timeSlotId: number | null;
  dismissed: boolean;
};

export type DismissSoftWarningRequest = {
  code: string;
  entryId?: number | null;
  staffId?: number | null;
  roomId?: number | null;
  dayOfWeek?: number | null;
  timeSlotId?: number | null;
};

export type TimetableChangeHistoryDto = {
  id: number;
  timetableId: number;
  entryId: number | null;
  userId: number | null;
  occurredUtc: string;
  operation: TimetableChangeOperation;
  oldValueJson: string | null;
  newValueJson: string | null;
  reason: string | null;
};

export type TimetableChangeHistoryFilter = {
  timetableId: number;
  entryId?: number | null;
  operation?: TimetableChangeOperation | null;
  fromUtc?: string | null;
  toUtc?: string | null;
};

export type TimetableGovernanceDashboardDto = {
  draftVersionCount: number;
  publishedVersionCount: number;
  approvalQueueCount: number;
  pendingReviewsCount: number;
  softWarningCount: number;
  recentlyPublishedCount: number;
  archivedVersionCount: number;
  recentChangesCount: number;
  frozenTimetableCount?: number;
  archivedTimetableCount?: number;
  approvalTrend: NamedCountDto[];
  versionGrowth: NamedCountDto[];
  publishingHistory: NamedCountDto[];
  archiveReasonDistribution?: NamedCountDto[];
  latestArchives?: ArchiveLifecycleItemDto[];
};

export type PublishTimetableRequest = {
  reason?: string | null;
};

export type ArchiveTimetableRequest = {
  reason?: string | null;
  archiveReasonId?: number | null;
  comments?: string | null;
  referenceVersionId?: number | null;
};

export type ArchiveScheduleVersionRequest = {
  archiveReasonId: number;
  comments?: string | null;
  referenceVersionId?: number | null;
};

export const listScheduleVersions = (params?: {
  academicYearId?: number;
  academicTermId?: number;
  status?: ScheduleVersionStatus;
  includeArchived?: boolean;
}) => api.get<ScheduleVersionDto[]>("/scheduling/versions", { params });

export const getScheduleVersion = (id: number) => api.get<ScheduleVersionDto>(`/scheduling/versions/${id}`);

export const getScheduleVersionHistory = (academicYearId: number, academicTermId?: number) =>
  api.get<ScheduleVersionHistoryDto[]>("/scheduling/versions/history", {
    params: { academicYearId, academicTermId },
  });

export const createScheduleVersion = (payload: CreateScheduleVersionRequest) =>
  api.post<ScheduleVersionDto>("/scheduling/versions", payload);

export const duplicateScheduleVersion = (payload: DuplicateScheduleVersionRequest) =>
  api.post<ScheduleVersionDto>("/scheduling/versions/duplicate", payload);

export const clonePreviousScheduleVersion = (params: {
  academicYearId: number;
  academicTermId?: number;
  versionName: string;
}) => api.post<ScheduleVersionDto>("/scheduling/versions/clone-previous", null, { params });

export const markCurrentScheduleVersion = (id: number) =>
  api.post<ScheduleVersionDto>(`/scheduling/versions/${id}/mark-current`);

export const archiveScheduleVersion = (id: number, payload?: ArchiveScheduleVersionRequest) =>
  api.post<ScheduleVersionDto>(`/scheduling/versions/${id}/archive`, payload ?? {});

export const compareScheduleVersions = (payload: CompareScheduleVersionsRequest) =>
  api.post<VersionComparisonDto>("/scheduling/versions/compare", payload);

export const exportVersionComparisonExcel = (payload: CompareScheduleVersionsRequest) =>
  api.post("/scheduling/versions/compare/export", payload, { responseType: "blob" });

export const listApprovalQueue = (status?: TimetableApprovalRequestStatus) =>
  api.get<TimetableApprovalRequestDto[]>("/scheduling/approvals", {
    params: status !== undefined ? { status } : {},
  });

export const getApprovalTimeline = (requestId: number) =>
  api.get<TimetableApprovalTimelineDto>(`/scheduling/approvals/${requestId}/timeline`);

export const submitTimetableForReview = (payload: SubmitForReviewRequest) =>
  api.post<TimetableApprovalRequestDto>("/scheduling/approvals/submit", payload);

export const decideApprovalStep = (payload: DecideApprovalStepRequest) =>
  api.post<TimetableApprovalRequestDto>("/scheduling/approvals/decide", payload);

export const addApprovalComment = (payload: { requestId: number; comment: string; isDecisionNote?: boolean }) =>
  api.post<ApprovalCommentDto>("/scheduling/approvals/comments", payload);

export const listCloneJobs = (status?: TimetableCloneJobStatus) =>
  api.get<TimetableCloneJobDto[]>("/scheduling/clone-jobs", {
    params: status !== undefined ? { status } : {},
  });

export const getCloneJob = (id: number) => api.get<TimetableCloneJobDto>(`/scheduling/clone-jobs/${id}`);

export const enqueueCloneJob = (payload: EnqueueTimetableCloneRequest) =>
  api.post<TimetableCloneJobDto>("/scheduling/clone-jobs", payload);

export const getGovernanceDashboard = (academicYearId?: number) =>
  api.get<TimetableGovernanceDashboardDto>("/scheduling/governance/dashboard", {
    params: academicYearId ? { academicYearId } : {},
  });

export const publishTimetable = (id: number, payload?: PublishTimetableRequest) =>
  api.post<TimetableDto>(`/scheduling/timetables/${id}/publish`, payload ?? {});

export const archiveTimetable = (id: number, payload?: ArchiveTimetableRequest) =>
  api.post<TimetableDto>(`/scheduling/timetables/${id}/archive`, payload ?? {});

export const freezeTimetable = (id: number, payload: { reason: string }) =>
  api.post<TimetableDto>(`/scheduling/timetables/${id}/freeze`, payload);

export const unlockFrozenTimetable = (id: number, payload: { reason: string }) =>
  api.post<TimetableDto>(`/scheduling/timetables/${id}/unlock-frozen`, payload);

export const listArchiveReasons = () => api.get<ArchiveReasonDto[]>("/scheduling/timetables/archive-reasons");

export const getTimetableSoftWarnings = (id: number) =>
  api.get<SoftWarningDto[]>(`/scheduling/timetables/${id}/soft-warnings`);

export const dismissTimetableSoftWarning = (id: number, payload: DismissSoftWarningRequest) =>
  api.post(`/scheduling/timetables/${id}/soft-warnings/dismiss`, payload);

export const getTimetableChangeHistory = (id: number, params?: Omit<TimetableChangeHistoryFilter, "timetableId">) =>
  api.get<TimetableChangeHistoryDto[]>(`/scheduling/timetables/${id}/history`, { params });

export const exportTimetableChangeHistoryExcel = (
  id: number,
  params?: Omit<TimetableChangeHistoryFilter, "timetableId">,
) =>
  api.get<Blob>(`/scheduling/timetables/${id}/history/export/excel`, {
    params,
    responseType: "blob",
  });

// --- AI30 Phase 2B Conflict Detection ---

export type ConflictCategory = 1 | 2 | 3 | 4 | 99;
export type ConflictSeverity = 1 | 2 | 3 | 4;

export type ConflictRecommendationDto = {
  suggestedResolution: string;
  navigationPath?: string | null;
  timetableId?: number | null;
  timetableEntryId?: number | null;
  dayOfWeek?: number | null;
  timeSlotId?: number | null;
};

export type ConflictResultDto = {
  ruleCode: string;
  ruleName: string;
  category: ConflictCategory;
  severity: ConflictSeverity;
  description: string;
  whyOccurred: string;
  recommendation: ConflictRecommendationDto;
  timetableId?: number | null;
  timetableEntryId?: number | null;
  relatedEntryId?: number | null;
  dayOfWeek?: number | null;
  timeSlotId?: number | null;
  staffId?: number | null;
  roomId?: number | null;
  departmentId?: number | null;
  courseId?: number | null;
  groupId?: number | null;
  semesterId?: number | null;
  subjectId?: number | null;
};

export type ConflictSummaryDto = {
  runId: number;
  timetableId?: number | null;
  academicYearId: number;
  departmentId?: number | null;
  startedUtc: string;
  completedUtc?: string | null;
  status: string;
  triggerSource: string;
  totalConflicts: number;
  facultyCount: number;
  roomCount: number;
  studentCount: number;
  calendarCount: number;
  criticalCount: number;
  errorCount: number;
  warningCount: number;
  informationCount: number;
  blocksEditing: boolean;
};

export type ConflictWorkspaceDto = {
  summary: ConflictSummaryDto;
  conflicts: ConflictResultDto[];
  groupedByRule: Record<string, number>;
  groupedByCategory: Record<string, number>;
};

export type HeatMapCellDto = {
  dayOfWeek: number;
  timeSlotId: number;
  timeSlotName?: string | null;
  loadCount: number;
  colour: string;
  maxSeverity: ConflictSeverity;
};

export type HeatMapDto = {
  kind: string;
  entityId?: number | null;
  entityName?: string | null;
  academicYearId: number;
  timetableId?: number | null;
  cells: HeatMapCellDto[];
  loadDistribution: Record<string, number>;
};

export type ConflictTrendPointDto = {
  dateUtc: string;
  warningCount: number;
  errorCount: number;
  criticalCount: number;
  totalConflicts: number;
};

export type ConflictDashboardDto = {
  latestSummary: ConflictSummaryDto;
  facultyConflicts: number;
  roomConflicts: number;
  studentConflicts: number;
  calendarConflicts: number;
  validationStatus: string;
  conflictCategories: Record<string, number>;
  warningTrends: ConflictTrendPointDto[];
  heatMaps: HeatMapDto[];
};

export type AttendanceSessionResolutionDto = {
  mode: "Legacy" | "Timetable" | string;
  hasTimetable: boolean;
  message: string;
  timetableId?: number | null;
  timetableEntryId?: number | null;
  courseId?: number | null;
  groupId?: number | null;
  semesterId?: number | null;
  subjectId?: number | null;
  periodNumber?: number | null;
  timeSlotId?: number | null;
  roomId?: number | null;
  subjectName?: string | null;
  roomName?: string | null;
  attendanceDate?: string | null;
};

export const analyzeConflicts = (payload: {
  timetableId?: number;
  academicYearId?: number;
  departmentId?: number;
  triggerSource?: string;
}) => api.post<{ summary: ConflictSummaryDto; conflicts: ConflictResultDto[] }>("/scheduling/conflicts/analyze", payload);

export const getConflictWorkspace = (params?: {
  timetableId?: number;
  academicYearId?: number;
  departmentId?: number;
  staffId?: number;
  roomId?: number;
  category?: ConflictCategory;
  severity?: ConflictSeverity;
  search?: string;
  reanalyze?: boolean;
}) => api.get<ConflictWorkspaceDto>("/scheduling/conflicts/workspace", { params });

export const getConflictDashboard = (params?: { academicYearId?: number; timetableId?: number }) =>
  api.get<ConflictDashboardDto>("/scheduling/conflicts/dashboard", { params });

export const getFacultyHeatMap = (academicYearId: number, params?: { staffId?: number; timetableId?: number }) =>
  api.get<HeatMapDto>("/scheduling/conflicts/heatmaps/faculty", { params: { academicYearId, ...params } });

export const getRoomHeatMap = (academicYearId: number, params?: { roomId?: number; timetableId?: number }) =>
  api.get<HeatMapDto>("/scheduling/conflicts/heatmaps/room", { params: { academicYearId, ...params } });

export const getDepartmentHeatMap = (
  academicYearId: number,
  params?: { departmentId?: number; timetableId?: number },
) => api.get<HeatMapDto>("/scheduling/conflicts/heatmaps/department", { params: { academicYearId, ...params } });

export const resolveAttendanceSession = (params?: { staffId?: number; date?: string }) =>
  api.get<AttendanceSessionResolutionDto>("/attendance-resolution/current", { params });

// AI30 Phase 2B.5 - Conflict Intelligence (advisory only)
export type ConflictResolutionDto = {
  recommendationId: string;
  title: string;
  summary: string;
  providerCode: string;
  options: {
    optionCode: string;
    label: string;
    description: string;
    actionHint: string;
    suggestedRoomId?: number | null;
    suggestedStaffId?: number | null;
    suggestedTimeSlotId?: number | null;
    suggestedDayOfWeek?: number | null;
    navigationPath?: string | null;
  }[];
  score: { confidence: number; impact: number; difficulty: number; rank: number };
  reasons: { code: string; message: string }[];
  estimatedResolution?: string | null;
  navigationPath?: string | null;
  isAdvisoryOnly: boolean;
  modifiesTimetable: boolean;
};

export type ConflictExplanationDto = {
  ruleCode: string;
  ruleName: string;
  ruleCategory: string;
  ruleDescription: string;
  businessReason: string;
  severity: ConflictSeverity;
  priority: number;
  whyTriggered: string;
  suggestedAction: string;
  impact: string;
  references: string[];
  navigationPath?: string | null;
  timetableId?: number | null;
  timetableEntryId?: number | null;
};

export type ImpactGraphDto = {
  summary: {
    facultyAffected: number;
    studentsAffected: number;
    roomsAffected: number;
    departmentsAffected: number;
    publishedVersionsAffected: number;
    workloadSignals: number;
    availabilitySignals: number;
    attendanceSignals: number;
    maxSeverity: ConflictSeverity;
    riskLevel: string;
  };
  nodes: { nodeId: string; category: number; label: string; entityId?: number | null; severity: ConflictSeverity; detail?: string | null }[];
  edges: { fromNodeId: string; toNodeId: string; relation: string }[];
  navigationPath?: string | null;
  isAdvisoryOnly: boolean;
};

export type DependencyGraphDto = {
  nodeCount: number;
  edgeCount: number;
  clusterCount: number;
  rootConflictCount: number;
  nodes: {
    nodeId: string;
    ruleCode: string;
    label: string;
    severity: ConflictSeverity;
    timetableEntryId?: number | null;
    relatedEntryId?: number | null;
    navigationPath?: string | null;
    clusterKey?: string | null;
  }[];
  edges: { fromNodeId: string; toNodeId: string; relation: string; reason: string }[];
  mermaid: string;
  clusters: Record<string, string[]>;
};

export type ConflictGuidanceDto = {
  conflict: ConflictResultDto;
  suggestedResolutions: ConflictResolutionDto[];
  explanation: ConflictExplanationDto;
  impact: ImpactGraphDto;
};

export type ConflictRuleThresholdDto = {
  thresholdKey: string;
  displayName: string;
  description?: string | null;
  unit: string;
  value: number;
  version: number;
  source: string;
  isActive: boolean;
};

export type ConflictAnalyticsDashboardDto = {
  topConflictTypes: { name: string; count: number }[];
  mostViolatedRules: { name: string; count: number }[];
  facultyConflictTrends: { name: string; count: number }[];
  roomConflictTrends: { name: string; count: number }[];
  departmentConflictTrends: { name: string; count: number }[];
  weeklyComparison: ConflictTrendPointDto[];
  monthlyComparison: ConflictTrendPointDto[];
  conflictResolutionRatePercent: number;
  averageResolutionTimeHours: number;
  totalHistoricalFindings: number;
  totalRuns: number;
};

export type EnhancedConflictWorkspaceDto = {
  workspace: ConflictWorkspaceDto;
  groupedByRule: Record<string, ConflictResultDto[]>;
  groupedByDepartment: Record<string, ConflictResultDto[]>;
  groupedByFaculty: Record<string, ConflictResultDto[]>;
  groupedBySeverity: Record<string, ConflictResultDto[]>;
  groupedByRoom: Record<string, ConflictResultDto[]>;
  pins: { id: number; conflictDetectionRunId: number; ruleCode: string; timetableEntryId?: number | null }[];
  bookmarks: { id: number; name: string; filterJson: string }[];
  notes: { id: number; conflictDetectionRunId: number; ruleCode: string; timetableEntryId?: number | null; noteText: string; userId: number }[];
  dependencyGraph: DependencyGraphDto;
};

export const getConflictGuidance = (params: {
  ruleCode: string;
  timetableEntryId?: number;
  academicYearId?: number;
  timetableId?: number;
}) => api.get<ConflictGuidanceDto>("/scheduling/conflicts/guidance", { params });

export const getConflictExplanation = (params: {
  ruleCode: string;
  timetableEntryId?: number;
  academicYearId?: number;
  timetableId?: number;
}) => api.get<ConflictExplanationDto>("/scheduling/conflicts/explain", { params });

export const getConflictImpact = (params: {
  ruleCode: string;
  timetableEntryId?: number;
  academicYearId?: number;
  timetableId?: number;
}) => api.get<ImpactGraphDto>("/scheduling/conflicts/impact", { params });

export const getConflictDependencies = (params?: { academicYearId?: number; timetableId?: number }) =>
  api.get<DependencyGraphDto>("/scheduling/conflicts/dependencies", { params });

export const getEnhancedConflictWorkspace = (params?: {
  timetableId?: number;
  academicYearId?: number;
  departmentId?: number;
  staffId?: number;
  roomId?: number;
  category?: ConflictCategory;
  severity?: ConflictSeverity;
  search?: string;
  reanalyze?: boolean;
}) => api.get<EnhancedConflictWorkspaceDto>("/scheduling/conflicts/workspace/enhanced", { params });

export const pinConflict = (payload: { conflictDetectionRunId: number; ruleCode: string; timetableEntryId?: number }) =>
  api.post("/scheduling/conflicts/workspace/pins", payload);

export const addConflictNote = (payload: {
  conflictDetectionRunId: number;
  ruleCode: string;
  timetableEntryId?: number;
  noteText: string;
}) => api.post("/scheduling/conflicts/workspace/notes", payload);

export const saveConflictBookmark = (payload: { name: string; filterJson: string }) =>
  api.post("/scheduling/conflicts/workspace/bookmarks", payload);

export const getConflictAnalytics = (params?: { academicYearId?: number }) =>
  api.get<ConflictAnalyticsDashboardDto>("/scheduling/conflicts/analytics", { params });

export const exportConflictAnalyticsExcel = (params?: { academicYearId?: number }) =>
  api.get<Blob>("/scheduling/conflicts/analytics/export/excel", { params, responseType: "blob" });

export const exportConflictAnalyticsPdf = (params?: { academicYearId?: number }) =>
  api.get<Blob>("/scheduling/conflicts/analytics/export/pdf", { params, responseType: "blob" });

export const getConflictRuleThresholds = () =>
  api.get<ConflictRuleThresholdDto[]>("/scheduling/conflicts/rules/thresholds");

export const updateConflictRuleThreshold = (payload: { thresholdKey: string; value: number; changeReason?: string }) =>
  api.put<ConflictRuleThresholdDto>("/scheduling/conflicts/rules/thresholds", payload);

export const getConflictRuleThresholdHistory = (thresholdKey?: string) =>
  api.get<{ thresholdKey: string; oldValue: number; newValue: number; version: number; changeReason?: string; changedByUserId?: number; changedUtc: string }[]>(
    "/scheduling/conflicts/rules/thresholds/history",
    { params: { thresholdKey } },
  );

// AI30 Phase 2B.6 - Optimization Readiness (preview only, no apply)
export type OptimizationScoreDto = {
  totalScore: number;
  normalizedScore: number;
  dimensions: { dimension: number; dimensionName: string; rawValue: number; weight: number; weightedScore: number }[];
};

export type OptimizationMetricDto = {
  metricKind: number;
  metricName: string;
  value: number;
  unit: string;
  capturedUtc: string;
  timetableId?: number | null;
  academicYearId: number;
};

export type OptimizationSimulationDto = {
  simulationId: string;
  scenarioName: string;
  strategyKind: number;
  status: number;
  currentScore: number;
  projectedScore: number;
  scoreDelta: number;
  currentConflictCount: number;
  projectedConflictCount: number;
  baselineScore: OptimizationScoreDto;
  projectedScoreDetail: OptimizationScoreDto;
  metrics: OptimizationMetricDto[];
  candidates: { candidateId: string; description: string; proposedChangeSummaries: string[]; isAdvisoryOnly: boolean; modifiesLiveTimetable: boolean }[];
  proposedChanges: string[];
  canApply: boolean;
  modifiesTimetable: boolean;
  message: string;
  scoringTimeMs: number;
  executionTimeMs: number;
};

export type OptimizationPreviewDto = {
  simulation: OptimizationSimulationDto;
  conflictSnapshot?: ConflictDashboardDto | null;
  heatMaps: HeatMapDto[];
  telemetry: {
    simulationCount: number;
    executionTimeMs: number;
    scoringTimeMs: number;
    averageImprovement: number;
    rejectedSimulations: number;
    acceptedSimulations: number;
    mostUsedMetrics: { name: string; count: number }[];
  };
  showApplyButton: boolean;
};

export const getOptimizationPreview = (params?: { simulationId?: string; academicYearId?: number; timetableId?: number }) =>
  api.get<OptimizationPreviewDto>("/scheduling/optimization/preview", { params });

export const runOptimizationSimulation = (payload?: {
  timetableId?: number;
  academicYearId?: number;
  departmentId?: number;
  strategyKind?: number;
  scenarioName?: string;
}) => api.post<OptimizationSimulationDto>("/scheduling/optimization/simulate", payload ?? {});

export const getOptimizationScore = (params?: { academicYearId?: number; timetableId?: number; departmentId?: number }) =>
  api.get<OptimizationScoreDto>("/scheduling/optimization/score", { params });

export const getOptimizationPlugins = () =>
  api.get<{ category: string; providerCode: string; providerName: string; isImplemented: boolean; notes: string }[]>(
    "/scheduling/optimization/plugins",
  );

// AI30 Phase 2B.7 - Optimization Sandbox
export type ScenarioSummaryDto = {
  scenarioId: string;
  id: number;
  name: string;
  description?: string | null;
  status: number;
  owner: { userId: number; displayName: string };
  academicYearId: number;
  departmentId?: number | null;
  semesterId?: number | null;
  timetableId?: number | null;
  isFavorite: boolean;
  isPinned: boolean;
  isTemplate: boolean;
  isImmutable: boolean;
  category: string;
  tags: string[];
  currentScore: number;
  projectedScore: number;
  conflictCount: number;
  replayCount: number;
  comparisonCount: number;
  viewCount: number;
  snapshotCount: number;
  createdUtc: string;
  lastReplayedUtc?: string | null;
  modifiesProductionTimetable: boolean;
  canApply: boolean;
};

export type OptimizationScenarioDetailDto = {
  summary: ScenarioSummaryDto;
  snapshots: {
    snapshotId: string;
    sequence: number;
    label: string;
    simulationId?: string | null;
    capturedUtc: string;
    isImmutable: boolean;
  }[];
  history: { action: number; actionName: string; actorUserId?: number | null; details?: string | null; occurredUtc: string }[];
  notes: { id: number; userId: number; noteText: string; createdUtc: string }[];
  comments: { id: number; userId: number; commentText: string; createdUtc: string }[];
  bookmarks: { id: number; name: string }[];
  approvals: { id: number; status: string; message?: string | null; requestedByUserId: number; requestedUtc: string }[];
};

export type OptimizationWorkspaceDto = {
  scenarios: ScenarioSummaryDto[];
  favorites: ScenarioSummaryDto[];
  templates: ScenarioSummaryDto[];
  evolution: {
    scoreEvolution: { dateUtc: string; label: string; value: number }[];
    conflictEvolution: { dateUtc: string; label: string; value: number }[];
    utilization: { dateUtc: string; label: string; value: number }[];
    facultySatisfaction: { dateUtc: string; label: string; value: number }[];
    roomUsage: { dateUtc: string; label: string; value: number }[];
    travel: { dateUtc: string; label: string; value: number }[];
    breakCompliance: { dateUtc: string; label: string; value: number }[];
    notes: string;
  };
  showApplyButton: boolean;
};

export type ScenarioComparisonResultDto = {
  left: ScenarioSummaryDto;
  right: ScenarioSummaryDto;
  differences: { scoreDelta: number; conflictDelta: number; projectedScoreDelta: number; verdict: string };
  improvementHighlights: string[];
  canApply: boolean;
};

export const getOptimizationSandboxWorkspace = (params?: { academicYearId?: number; departmentId?: number }) =>
  api.get<OptimizationWorkspaceDto>("/scheduling/optimization/sandbox/workspace", { params });

export const createSandboxScenario = (payload: {
  name: string;
  description?: string;
  academicYearId?: number;
  departmentId?: number;
  semesterId?: number;
  timetableId?: number;
  sourceSimulationId?: string;
  category?: string;
  tagsCsv?: string;
  captureFromLatestSimulation?: boolean;
}) => api.post<ScenarioSummaryDto>("/scheduling/optimization/sandbox/scenarios", payload);

export const getSandboxScenarioDetail = (scenarioId: string) =>
  api.get<OptimizationScenarioDetailDto>(`/scheduling/optimization/sandbox/scenarios/${scenarioId}`);

export const saveSandboxScenario = (scenarioId: string) =>
  api.post<ScenarioSummaryDto>(`/scheduling/optimization/sandbox/scenarios/${scenarioId}/save`);

export const duplicateSandboxScenario = (payload: { scenarioId: string; newName?: string }) =>
  api.post<ScenarioSummaryDto>("/scheduling/optimization/sandbox/scenarios/duplicate", payload);

export const deleteSandboxScenario = (scenarioId: string) =>
  api.delete(`/scheduling/optimization/sandbox/scenarios/${scenarioId}`);

export const favoriteSandboxScenario = (scenarioId: string, value = true) =>
  api.post<ScenarioSummaryDto>(`/scheduling/optimization/sandbox/scenarios/${scenarioId}/favorite`, null, { params: { value } });

export const pinSandboxScenario = (scenarioId: string, value = true) =>
  api.post<ScenarioSummaryDto>(`/scheduling/optimization/sandbox/scenarios/${scenarioId}/pin`, null, { params: { value } });

export const archiveSandboxScenario = (scenarioId: string) =>
  api.post<ScenarioSummaryDto>(`/scheduling/optimization/sandbox/scenarios/${scenarioId}/archive`);

export const replaySandboxScenario = (scenarioId: string) =>
  api.post(`/scheduling/optimization/sandbox/scenarios/${scenarioId}/replay`);

export const compareSandboxScenarios = (payload: { leftScenarioId: string; rightScenarioId: string }) =>
  api.post<ScenarioComparisonResultDto>("/scheduling/optimization/sandbox/scenarios/compare", payload);

// AI30 Phase 3 - Enterprise Optimization Engine
export type OptimizationProgressDto = {
  runId: string;
  sessionId: string;
  currentStrategy: string;
  progressPercent: number;
  elapsedMs: number;
  estimatedRemainingMs?: number | null;
  currentScore: number;
  improvementDelta: number;
  statusMessage: string;
  status: number;
};

export type OptimizationComparisonDto = {
  originalScore: number;
  optimizedScore: number;
  scoreImprovement: number;
  originalConflicts: number;
  optimizedConflicts: number;
  conflictReduction: number;
  facultySatisfactionDelta: number;
  roomUsageDelta: number;
  travelDelta: number;
  breaksDelta: number;
  highlights: string[];
};

export type OptimizationRunSummaryDto = {
  runId: string;
  sessionId: string;
  status: number;
  strategyKind: number;
  academicYearId: number;
  timetableId?: number | null;
  baselineScore: number;
  projectedScore: number;
  improvementDelta: number;
  baselineConflictCount: number;
  projectedConflictCount: number;
  sandboxScenarioId?: string | null;
  resultDraftScheduleVersionId?: number | null;
  startedUtc: string;
  completedUtc?: string | null;
  elapsedMs: number;
  modifiesProductionTimetable: boolean;
};

export type OptimizationExecutionResultDto = {
  runId: string;
  sessionId: string;
  status: number;
  sandboxScenarioId?: string | null;
  comparison?: OptimizationComparisonDto | null;
  intermediateResults: Array<{
    strategyCode: string;
    strategyName: string;
    kind: number;
    candidateCount: number;
    scoreAfter: number;
    conflictCountAfter: number;
    elapsedMs: number;
    message: string;
  }>;
  elapsedMs: number;
  errorMessage?: string | null;
  combinedResult: {
    summary: {
      candidateCount: number;
      baselineScore: number;
      bestProjectedScore: number;
      improvementDelta: number;
      baselineConflictCount: number;
      projectedConflictCount: number;
      statusMessage: string;
    };
    candidates: Array<{
      candidateId: string;
      description: string;
      proposedChangeSummaries: string[];
      changeType: string;
      entryId?: number | null;
      strategyCode: string;
    }>;
  };
  modifiesProductionTimetable: boolean;
};

export type OptimizationDashboardDto = {
  totalRuns: number;
  completedRuns: number;
  approvedRuns: number;
  bestScore: number;
  averageImprovement: number;
  averageConflictReduction: number;
  averageFacultySatisfactionDelta: number;
  topStrategies: Array<{ strategyCode: string; candidateCount: number }>;
  recentRuns: OptimizationRunSummaryDto[];
  scenarioHistory: Array<{
    scenarioId?: string | null;
    runId: string;
    projectedScore: number;
    improvementDelta: number;
    startedUtc: string;
    status: number;
  }>;
};

export type OptimizationApprovalResultDto = {
  runId: string;
  draftScheduleVersionId: number;
  draftVersionName: string;
  appliedCandidateCount: number;
  overwrotePublishedTimetable: boolean;
  modifiedExistingDraft: boolean;
  message: string;
};

export const runOptimizationEngine = (payload: {
  academicYearId?: number;
  timetableId?: number;
  departmentId?: number;
  scenarioName?: string;
}) => api.post<OptimizationExecutionResultDto>("/scheduling/optimization/engine/run", payload);

export const listOptimizationRuns = (params?: { academicYearId?: number; departmentId?: number }) =>
  api.get<OptimizationRunSummaryDto[]>("/scheduling/optimization/engine/runs", { params });

export const getOptimizationRun = (runId: string) =>
  api.get<OptimizationExecutionResultDto>(`/scheduling/optimization/engine/runs/${runId}`);

export const getOptimizationComparison = (runId: string) =>
  api.get<OptimizationComparisonDto>(`/scheduling/optimization/engine/runs/${runId}/comparison`);

export const approveOptimizationRun = (payload: { runId: string; newVersionName?: string; remarks?: string }) =>
  api.post<OptimizationApprovalResultDto>("/scheduling/optimization/engine/approve", payload);

export const rejectOptimizationRun = (payload: { runId: string; reason?: string }) =>
  api.post("/scheduling/optimization/engine/reject", payload);

export const getOptimizationDashboard = (params?: { academicYearId?: number; departmentId?: number }) =>
  api.get<OptimizationDashboardDto>("/scheduling/optimization/engine/dashboard", { params });
