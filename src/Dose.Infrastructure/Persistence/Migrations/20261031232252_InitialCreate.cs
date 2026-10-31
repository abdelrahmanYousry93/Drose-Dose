using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dose.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Entity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EducationalStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RequiresGuardianContact = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationalStages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ReceivingAccount = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    InstructionsAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingBands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingBands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequiresPackage = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContactPhoneE164 = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    GuardianName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GuardianPhoneE164 = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudyTracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyTracks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DisplayNameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PhotoUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    BioAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    QualificationsAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    YearsOfExperience = table.Column<int>(type: "int", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Grades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PricingBandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurriculumSystem = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Grades_EducationalStages_StageId",
                        column: x => x.StageId,
                        principalTable: "EducationalStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Grades_PricingBands_PricingBandId",
                        column: x => x.PricingBandId,
                        principalTable: "PricingBands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ServiceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    PricingBandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StudyTrackId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Policy = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingPlans_PricingBands_PricingBandId",
                        column: x => x.PricingBandId,
                        principalTable: "PricingBands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingPlans_ServiceTypes_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingPlans_StudyTracks_StudyTrackId",
                        column: x => x.StudyTrackId,
                        principalTable: "StudyTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingPlans_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Chapters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudyTrackId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chapters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chapters_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Chapters_StudyTracks_StudyTrackId",
                        column: x => x.StudyTrackId,
                        principalTable: "StudyTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Chapters_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradeSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeSubjects_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradeSubjects_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingPlanVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    PrivatePrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingPlanVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingPlanVersions_PricingPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "PricingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ChapterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lessons_Chapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    TargetTierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TierMin = table.Column<int>(type: "int", nullable: true),
                    TierMax = table.Column<int>(type: "int", nullable: true),
                    WorstCasePrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    FinalAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Policy = table.Column<int>(type: "int", nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceSnapshots_PricingPlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "PricingPlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    MinParticipants = table.Column<int>(type: "int", nullable: false),
                    MaxParticipants = table.Column<int>(type: "int", nullable: false),
                    PricePerStudent = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingTiers", x => x.Id);
                    table.CheckConstraint("CK_PricingTier_Price", "[PricePerStudent] > 0");
                    table.CheckConstraint("CK_PricingTier_Range", "[MinParticipants] >= 1 AND [MaxParticipants] >= [MinParticipants]");
                    table.ForeignKey(
                        name: "FK_PricingTiers_PricingPlanVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "PricingPlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudyTrackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopeTitleAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ScopeDetailsAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    MinParticipants = table.Column<int>(type: "int", nullable: false),
                    PricingPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetTierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Policy = table.Column<int>(type: "int", nullable: false),
                    BookingDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TierDecisionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MeetingProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MeetingUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.CheckConstraint("CK_Session_Capacity", "[Capacity] >= 1 AND [MinParticipants] >= 1 AND [MinParticipants] <= [Capacity]");
                    table.CheckConstraint("CK_Session_Time", "[EndUtc] > [StartUtc]");
                    table.ForeignKey(
                        name: "FK_Sessions_Chapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_PricingPlanVersions_PricingPlanVersionId",
                        column: x => x.PricingPlanVersionId,
                        principalTable: "PricingPlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_PricingTiers_TargetTierId",
                        column: x => x.TargetTierId,
                        principalTable: "PricingTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_ServiceTypes_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_StudyTracks_StudyTrackId",
                        column: x => x.StudyTrackId,
                        principalTable: "StudyTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactPhoneE164 = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HoldExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QuotedPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    QuotedWorstCasePrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    TermsAcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TopUpTermsAcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriceSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccessTokenHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_PriceSnapshots_PriceSnapshotId",
                        column: x => x.PriceSnapshotId,
                        principalTable: "PriceSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentProofs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    SenderReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    FileContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentProofs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentProofs_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentProofs_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entity_EntityId",
                table: "AuditLogs",
                columns: new[] { "Entity", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_AccessTokenHash",
                table: "Bookings",
                column: "AccessTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PriceSnapshotId",
                table: "Bookings",
                column: "PriceSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Reference",
                table: "Bookings",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SessionId_Status",
                table: "Bookings",
                columns: new[] { "SessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_StudentId",
                table: "Bookings",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UX_Bookings_ActivePerContact",
                table: "Bookings",
                columns: new[] { "SessionId", "ContactPhoneE164" },
                unique: true,
                filter: "[ContactPhoneE164] IS NOT NULL AND [Status] IN (0,1,2,3,4)");

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_GradeId",
                table: "Chapters",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_StudyTrackId",
                table: "Chapters",
                column: "StudyTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_SubjectId",
                table: "Chapters",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Grades_PricingBandId",
                table: "Grades",
                column: "PricingBandId");

            migrationBuilder.CreateIndex(
                name: "IX_Grades_StageId",
                table: "Grades",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeSubjects_GradeId_SubjectId",
                table: "GradeSubjects",
                columns: new[] { "GradeId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeSubjects_SubjectId",
                table: "GradeSubjects",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_ChapterId",
                table: "Lessons",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Code",
                table: "PaymentMethods",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_BookingId",
                table: "PaymentProofs",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_PaymentMethodId",
                table: "PaymentProofs",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_SenderReference",
                table: "PaymentProofs",
                column: "SenderReference");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_Status_SubmittedAtUtc",
                table: "PaymentProofs",
                columns: new[] { "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceSnapshots_PlanVersionId",
                table: "PriceSnapshots",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingPlans_PricingBandId",
                table: "PricingPlans",
                column: "PricingBandId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingPlans_ServiceTypeId_Mode_PricingBandId_StudyTrackId_SubjectId_IsActive",
                table: "PricingPlans",
                columns: new[] { "ServiceTypeId", "Mode", "PricingBandId", "StudyTrackId", "SubjectId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingPlans_StudyTrackId",
                table: "PricingPlans",
                column: "StudyTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingPlans_SubjectId",
                table: "PricingPlans",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingPlanVersions_PlanId_VersionNo",
                table: "PricingPlanVersions",
                columns: new[] { "PlanId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingTiers_VersionId",
                table: "PricingTiers",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTypes_Code",
                table: "ServiceTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ChapterId",
                table: "Sessions",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_GradeId_SubjectId_StudyTrackId_Status",
                table: "Sessions",
                columns: new[] { "GradeId", "SubjectId", "StudyTrackId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_LessonId",
                table: "Sessions",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_PricingPlanVersionId",
                table: "Sessions",
                column: "PricingPlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ServiceTypeId",
                table: "Sessions",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Status_StartUtc",
                table: "Sessions",
                columns: new[] { "Status", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_StudyTrackId",
                table: "Sessions",
                column: "StudyTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_SubjectId",
                table: "Sessions",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TargetTierId",
                table: "Sessions",
                column: "TargetTierId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TeacherId",
                table: "Sessions",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ContactPhoneE164",
                table: "Students",
                column: "ContactPhoneE164");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "GradeSubjects");

            migrationBuilder.DropTable(
                name: "PaymentProofs");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "PriceSnapshots");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Lessons");

            migrationBuilder.DropTable(
                name: "PricingTiers");

            migrationBuilder.DropTable(
                name: "Teachers");

            migrationBuilder.DropTable(
                name: "Chapters");

            migrationBuilder.DropTable(
                name: "PricingPlanVersions");

            migrationBuilder.DropTable(
                name: "Grades");

            migrationBuilder.DropTable(
                name: "PricingPlans");

            migrationBuilder.DropTable(
                name: "EducationalStages");

            migrationBuilder.DropTable(
                name: "PricingBands");

            migrationBuilder.DropTable(
                name: "ServiceTypes");

            migrationBuilder.DropTable(
                name: "StudyTracks");

            migrationBuilder.DropTable(
                name: "Subjects");
        }
    }
}
