using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Abhyanvaya.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AI30_Phase1_EnterpriseSchedulingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulingAcademicYear",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingAcademicYear", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingCampus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingCampus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingFacultyWorkload",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffId = table.Column<int>(type: "integer", nullable: false),
                    MaxPeriodsPerDay = table.Column<int>(type: "integer", nullable: false),
                    MaxPeriodsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    TeachingLoadHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    LabLoadHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    MentoringLoadHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    AdministrativeLoadHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    IsGuestFaculty = table.Column<bool>(type: "boolean", nullable: false),
                    IsAdjunctFaculty = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingFacultyWorkload", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyWorkload_StaffMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingAcademicTerm",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingAcademicTerm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingAcademicTerm_SchedulingAcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingHoliday",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    HolidayType = table.Column<byte>(type: "smallint", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingHoliday", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingHoliday_SchedulingAcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimeSlotSet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimeSlotSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimeSlotSet_SchedulingAcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingWorkingDay",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "smallint", nullable: false),
                    IsWorking = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingWorkingDay", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingWorkingDay_SchedulingAcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingBuilding",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampusId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingBuilding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingBuilding_SchedulingCampus_CampusId",
                        column: x => x.CampusId,
                        principalTable: "SchedulingCampus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingFacultyDayPreference",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacultyWorkloadId = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "smallint", nullable: false),
                    PreferenceType = table.Column<byte>(type: "smallint", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingFacultyDayPreference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyDayPreference_SchedulingFacultyWorkload_Fa~",
                        column: x => x.FacultyWorkloadId,
                        principalTable: "SchedulingFacultyWorkload",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingTimeSlot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimeSlotSetId = table.Column<int>(type: "integer", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "smallint", nullable: true),
                    SlotKind = table.Column<byte>(type: "smallint", nullable: false),
                    SessionKind = table.Column<byte>(type: "smallint", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingTimeSlot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingTimeSlot_SchedulingTimeSlotSet_TimeSlotSetId",
                        column: x => x.TimeSlotSetId,
                        principalTable: "SchedulingTimeSlotSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingFloor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuildingId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LevelNumber = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingFloor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingFloor_SchedulingBuilding_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "SchedulingBuilding",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingFacultyTimeSlotPreference",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacultyWorkloadId = table.Column<int>(type: "integer", nullable: false),
                    TimeSlotId = table.Column<int>(type: "integer", nullable: false),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingFacultyTimeSlotPreference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTimeSlotPreference_SchedulingFacultyWorklo~",
                        column: x => x.FacultyWorkloadId,
                        principalTable: "SchedulingFacultyWorkload",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingFacultyTimeSlotPreference_SchedulingTimeSlot_Time~",
                        column: x => x.TimeSlotId,
                        principalTable: "SchedulingTimeSlot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingRoom",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FloorId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RoomType = table.Column<byte>(type: "smallint", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    FeatureFlags = table.Column<short>(type: "smallint", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingRoom", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingRoom_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingRoom_SchedulingFloor_FloorId",
                        column: x => x.FloorId,
                        principalTable: "SchedulingFloor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingRoomAllocationRule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: true),
                    RoomType = table.Column<byte>(type: "smallint", nullable: true),
                    MinCapacity = table.Column<int>(type: "integer", nullable: true),
                    MaxCapacity = table.Column<int>(type: "integer", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    CourseId = table.Column<int>(type: "integer", nullable: true),
                    RequireComputerLab = table.Column<bool>(type: "boolean", nullable: false),
                    RequireScienceLab = table.Column<bool>(type: "boolean", nullable: false),
                    RequireCommerceLab = table.Column<bool>(type: "boolean", nullable: false),
                    RequireAiCamera = table.Column<bool>(type: "boolean", nullable: false),
                    RequireProjector = table.Column<bool>(type: "boolean", nullable: false),
                    RequireSmartBoard = table.Column<bool>(type: "boolean", nullable: false),
                    PreferredRoomId = table.Column<int>(type: "integer", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingRoomAllocationRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingRoomAllocationRule_Course_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Course",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingRoomAllocationRule_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingRoomAllocationRule_SchedulingAcademicYear_Academi~",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingRoomAllocationRule_SchedulingRoom_PreferredRoomId",
                        column: x => x.PreferredRoomId,
                        principalTable: "SchedulingRoom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingSubjectAllocation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    StaffId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    SemesterId = table.Column<int>(type: "integer", nullable: false),
                    WeeklyHours = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PreferredRoomId = table.Column<int>(type: "integer", nullable: true),
                    LabRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AiAttendanceEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AttendanceMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingSubjectAllocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingSubjectAllocation_Course_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Course",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingSubjectAllocation_Group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingSubjectAllocation_SchedulingAcademicYear_Academic~",
                        column: x => x.AcademicYearId,
                        principalTable: "SchedulingAcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingSubjectAllocation_SchedulingRoom_PreferredRoomId",
                        column: x => x.PreferredRoomId,
                        principalTable: "SchedulingRoom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingSubjectAllocation_Semester_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "Semester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingSubjectAllocation_StaffMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulingSubjectAllocation_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permission",
                columns: new[] { "Id", "Action", "Key", "Resource" },
                values: new object[,]
                {
                    { 16, "View", "Enrollment.View", "Enrollment" },
                    { 17, "Manage", "Enrollment.Manage", "Enrollment" },
                    { 18, "View", "Scheduling.View", "Scheduling" },
                    { 19, "Manage", "Scheduling.Manage", "Scheduling" }
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 8, 1, 14, 36, 51, 205, DateTimeKind.Utc).AddTicks(3344));

            migrationBuilder.InsertData(
                table: "ApplicationRolePermission",
                columns: new[] { "ApplicationRoleId", "PermissionId" },
                values: new object[,]
                {
                    { 100, 16 },
                    { 100, 17 },
                    { 100, 18 },
                    { 100, 19 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingAcademicTerm_AcademicYearId",
                table: "SchedulingAcademicTerm",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingAcademicTerm_TenantId_AcademicYearId_Sequence",
                table: "SchedulingAcademicTerm",
                columns: new[] { "TenantId", "AcademicYearId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingAcademicYear_TenantId_Code",
                table: "SchedulingAcademicYear",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingBuilding_CampusId",
                table: "SchedulingBuilding",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingBuilding_TenantId_CampusId_Code",
                table: "SchedulingBuilding",
                columns: new[] { "TenantId", "CampusId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingCampus_TenantId_Code",
                table: "SchedulingCampus",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyDayPreference_FacultyWorkloadId",
                table: "SchedulingFacultyDayPreference",
                column: "FacultyWorkloadId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyDayPreference_TenantId_FacultyWorkloadId_D~",
                table: "SchedulingFacultyDayPreference",
                columns: new[] { "TenantId", "FacultyWorkloadId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTimeSlotPreference_FacultyWorkloadId",
                table: "SchedulingFacultyTimeSlotPreference",
                column: "FacultyWorkloadId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTimeSlotPreference_TenantId_FacultyWorkloa~",
                table: "SchedulingFacultyTimeSlotPreference",
                columns: new[] { "TenantId", "FacultyWorkloadId", "TimeSlotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyTimeSlotPreference_TimeSlotId",
                table: "SchedulingFacultyTimeSlotPreference",
                column: "TimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyWorkload_StaffId",
                table: "SchedulingFacultyWorkload",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFacultyWorkload_TenantId_StaffId",
                table: "SchedulingFacultyWorkload",
                columns: new[] { "TenantId", "StaffId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFloor_BuildingId",
                table: "SchedulingFloor",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingFloor_TenantId_BuildingId_LevelNumber",
                table: "SchedulingFloor",
                columns: new[] { "TenantId", "BuildingId", "LevelNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingHoliday_AcademicYearId",
                table: "SchedulingHoliday",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingHoliday_TenantId_AcademicYearId_Date_Name",
                table: "SchedulingHoliday",
                columns: new[] { "TenantId", "AcademicYearId", "Date", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoom_DepartmentId",
                table: "SchedulingRoom",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoom_FloorId",
                table: "SchedulingRoom",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoom_TenantId_FloorId_Code",
                table: "SchedulingRoom",
                columns: new[] { "TenantId", "FloorId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomAllocationRule_AcademicYearId",
                table: "SchedulingRoomAllocationRule",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomAllocationRule_CourseId",
                table: "SchedulingRoomAllocationRule",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomAllocationRule_DepartmentId",
                table: "SchedulingRoomAllocationRule",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomAllocationRule_PreferredRoomId",
                table: "SchedulingRoomAllocationRule",
                column: "PreferredRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingRoomAllocationRule_TenantId_Name",
                table: "SchedulingRoomAllocationRule",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectAllocation_AcademicYearId",
                table: "SchedulingSubjectAllocation",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectAllocation_CourseId",
                table: "SchedulingSubjectAllocation",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectAllocation_GroupId",
                table: "SchedulingSubjectAllocation",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectAllocation_PreferredRoomId",
                table: "SchedulingSubjectAllocation",
                column: "PreferredRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectAllocation_SemesterId",
                table: "SchedulingSubjectAllocation",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectAllocation_StaffId",
                table: "SchedulingSubjectAllocation",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectAllocation_SubjectId",
                table: "SchedulingSubjectAllocation",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingSubjectAllocation_TenantId_AcademicYearId_Subject~",
                table: "SchedulingSubjectAllocation",
                columns: new[] { "TenantId", "AcademicYearId", "SubjectId", "CourseId", "GroupId", "SemesterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimeSlot_TenantId_TimeSlotSetId_DayOfWeek_PeriodN~",
                table: "SchedulingTimeSlot",
                columns: new[] { "TenantId", "TimeSlotSetId", "DayOfWeek", "PeriodNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimeSlot_TimeSlotSetId",
                table: "SchedulingTimeSlot",
                column: "TimeSlotSetId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimeSlotSet_AcademicYearId",
                table: "SchedulingTimeSlotSet",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingTimeSlotSet_TenantId_Code",
                table: "SchedulingTimeSlotSet",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingWorkingDay_AcademicYearId",
                table: "SchedulingWorkingDay",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingWorkingDay_TenantId_AcademicYearId_DayOfWeek",
                table: "SchedulingWorkingDay",
                columns: new[] { "TenantId", "AcademicYearId", "DayOfWeek" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulingAcademicTerm");

            migrationBuilder.DropTable(
                name: "SchedulingFacultyDayPreference");

            migrationBuilder.DropTable(
                name: "SchedulingFacultyTimeSlotPreference");

            migrationBuilder.DropTable(
                name: "SchedulingHoliday");

            migrationBuilder.DropTable(
                name: "SchedulingRoomAllocationRule");

            migrationBuilder.DropTable(
                name: "SchedulingSubjectAllocation");

            migrationBuilder.DropTable(
                name: "SchedulingWorkingDay");

            migrationBuilder.DropTable(
                name: "SchedulingFacultyWorkload");

            migrationBuilder.DropTable(
                name: "SchedulingTimeSlot");

            migrationBuilder.DropTable(
                name: "SchedulingRoom");

            migrationBuilder.DropTable(
                name: "SchedulingTimeSlotSet");

            migrationBuilder.DropTable(
                name: "SchedulingFloor");

            migrationBuilder.DropTable(
                name: "SchedulingAcademicYear");

            migrationBuilder.DropTable(
                name: "SchedulingBuilding");

            migrationBuilder.DropTable(
                name: "SchedulingCampus");

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 16 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 17 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 18 });

            migrationBuilder.DeleteData(
                table: "ApplicationRolePermission",
                keyColumns: new[] { "ApplicationRoleId", "PermissionId" },
                keyValues: new object[] { 100, 19 });

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Permission",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 23, 17, 18, 54, 849, DateTimeKind.Utc).AddTicks(3023));
        }
    }
}
