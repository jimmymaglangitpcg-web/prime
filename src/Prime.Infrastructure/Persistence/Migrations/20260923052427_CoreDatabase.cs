using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CoreDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActualUses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActualUses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupabaseUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildingComponentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingComponentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildingTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Classifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conditions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MachineryTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OwnershipTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnershipTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PsgcCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoadTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoadTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StructuralTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuralTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubClassifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubClassifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Zones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Bucket = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Municipalities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvinceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PsgcCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsCity = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Municipalities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Municipalities_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    AppUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.AppUserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Barangays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PsgcCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Barangays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Barangays_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Property",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyIdentificationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProvinceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Street = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Sitio = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BlockNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SurveyNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TitleNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TaxMapNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Property", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Property_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Property_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Property_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Property_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Taxpayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxpayerType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    MiddleName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Suffix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CorporateName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Tin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: true),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProvinceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taxpayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Taxpayers_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Taxpayers_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Taxpayers_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Parcels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Geometry = table.Column<Geometry>(type: "geometry", nullable: true),
                    Area = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    SurveyNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BlockNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcels_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Parcels_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Parcels_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RealPropertyUnit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RpuType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EffectivityDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PreviousRpuId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealPropertyUnit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RealPropertyUnit_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RealPropertyUnit_RealPropertyUnit_PreviousRpuId",
                        column: x => x.PreviousRpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropertyTaxpayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxpayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnershipTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnershipPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyTaxpayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyTaxpayers_OwnershipTypes_OwnershipTypeId",
                        column: x => x.OwnershipTypeId,
                        principalTable: "OwnershipTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyTaxpayers_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyTaxpayers_Taxpayers_TaxpayerId",
                        column: x => x.TaxpayerId,
                        principalTable: "Taxpayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StructuralTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumberOfStoreys = table.Column<int>(type: "integer", nullable: false),
                    FloorArea = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    TotalFloorArea = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    YearConstructed = table.Column<int>(type: "integer", nullable: true),
                    YearCompleted = table.Column<int>(type: "integer", nullable: true),
                    ConditionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletionPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Depreciation = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    DepreciatedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Buildings_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Buildings_BuildingTypes_BuildingTypeId",
                        column: x => x.BuildingTypeId,
                        principalTable: "BuildingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Buildings_Conditions_ConditionId",
                        column: x => x.ConditionId,
                        principalTable: "Conditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Buildings_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Buildings_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Buildings_StructuralTypes_StructuralTypeId",
                        column: x => x.StructuralTypeId,
                        principalTable: "StructuralTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Area = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    AreaUnit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationFactor = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    RoadFrontage = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    RoadTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCornerLot = table.Column<bool>(type: "boolean", nullable: false),
                    Zoning = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lands_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lands_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lands_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lands_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lands_RoadTypes_RoadTypeId",
                        column: x => x.RoadTypeId,
                        principalTable: "RoadTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lands_SubClassifications_SubClassificationId",
                        column: x => x.SubClassificationId,
                        principalTable: "SubClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lands_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MachineryUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineryTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Brand = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Capacity = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    CapacityUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DateAcquired = table.Column<DateOnly>(type: "date", nullable: true),
                    AcquisitionCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallationCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OtherCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EconomicLifeYears = table.Column<int>(type: "integer", nullable: true),
                    RemainingLifeYears = table.Column<int>(type: "integer", nullable: true),
                    Depreciation = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    MarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AssessedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineryUnits_MachineryTypes_MachineryTypeId",
                        column: x => x.MachineryTypeId,
                        principalTable: "MachineryTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryUnits_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryUnits_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxDeclarations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    EffectivityDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Taxability = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssessmentYear = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PreviousTaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxDeclarations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxDeclarations_ActualUses_ActualUseId",
                        column: x => x.ActualUseId,
                        principalTable: "ActualUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxDeclarations_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxDeclarations_Property_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxDeclarations_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxDeclarations_SubClassifications_SubClassificationId",
                        column: x => x.SubClassificationId,
                        principalTable: "SubClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxDeclarations_TaxDeclarations_PreviousTaxDeclarationId",
                        column: x => x.PreviousTaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BuildingComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingComponents_BuildingComponentTypes_ComponentTypeId",
                        column: x => x.ComponentTypeId,
                        principalTable: "BuildingComponentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuildingComponents_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActualUses_Code",
                table: "ActualUses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_SupabaseUserId",
                table: "AppUsers",
                column: "SupabaseUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Barangays_MunicipalityId",
                table: "Barangays",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_Barangays_PsgcCode",
                table: "Barangays",
                column: "PsgcCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildingComponents_BuildingId",
                table: "BuildingComponents",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingComponents_ComponentTypeId",
                table: "BuildingComponents",
                column: "ComponentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingComponentTypes_Code",
                table: "BuildingComponentTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_ActualUseId",
                table: "Buildings",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_BuildingTypeId",
                table: "Buildings",
                column: "BuildingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_ConditionId",
                table: "Buildings",
                column: "ConditionId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_PropertyId",
                table: "Buildings",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_RpuId",
                table: "Buildings",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_StructuralTypeId",
                table: "Buildings",
                column: "StructuralTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingTypes_Code",
                table: "BuildingTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classifications_Code",
                table: "Classifications",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conditions_Code",
                table: "Conditions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentTypeId",
                table: "Documents",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_RelatedEntityType_RelatedEntityId",
                table: "Documents",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_Code",
                table: "DocumentTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lands_ActualUseId",
                table: "Lands",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_Lands_ClassificationId",
                table: "Lands",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_Lands_PropertyId",
                table: "Lands",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Lands_RoadTypeId",
                table: "Lands",
                column: "RoadTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Lands_RpuId",
                table: "Lands",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_Lands_SubClassificationId",
                table: "Lands",
                column: "SubClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_Lands_ZoneId",
                table: "Lands",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryTypes_Code",
                table: "MachineryTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineryUnits_MachineryTypeId",
                table: "MachineryUnits",
                column: "MachineryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryUnits_PropertyId",
                table: "MachineryUnits",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryUnits_RpuId",
                table: "MachineryUnits",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryUnits_SerialNumber",
                table: "MachineryUnits",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Municipalities_ProvinceId",
                table: "Municipalities",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Municipalities_PsgcCode",
                table: "Municipalities",
                column: "PsgcCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTypes_Code",
                table: "OwnershipTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BarangayId",
                table: "Parcels",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Geometry",
                table: "Parcels",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_PropertyId",
                table: "Parcels",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_SurveyNumber",
                table: "Parcels",
                column: "SurveyNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ZoneId",
                table: "Parcels",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Property_BarangayId",
                table: "Property",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_Property_LotNumber",
                table: "Property",
                column: "LotNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Property_MunicipalityId",
                table: "Property",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_Property_PropertyIdentificationNumber",
                table: "Property",
                column: "PropertyIdentificationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Property_ProvinceId",
                table: "Property",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Property_SurveyNumber",
                table: "Property",
                column: "SurveyNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Property_TaxMapNumber",
                table: "Property",
                column: "TaxMapNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Property_TitleNumber",
                table: "Property",
                column: "TitleNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Property_ZoneId",
                table: "Property",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTaxpayers_OwnershipTypeId",
                table: "PropertyTaxpayers",
                column: "OwnershipTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTaxpayers_PropertyId_IsCurrent",
                table: "PropertyTaxpayers",
                columns: new[] { "PropertyId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTaxpayers_PropertyId_StartDate",
                table: "PropertyTaxpayers",
                columns: new[] { "PropertyId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTaxpayers_TaxpayerId",
                table: "PropertyTaxpayers",
                column: "TaxpayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_PsgcCode",
                table: "Provinces",
                column: "PsgcCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RealPropertyUnit_PreviousRpuId",
                table: "RealPropertyUnit",
                column: "PreviousRpuId");

            migrationBuilder.CreateIndex(
                name: "IX_RealPropertyUnit_PropertyId_EffectivityDate",
                table: "RealPropertyUnit",
                columns: new[] { "PropertyId", "EffectivityDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RealPropertyUnit_RpuNumber",
                table: "RealPropertyUnit",
                column: "RpuNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoadTypes_Code",
                table: "RoadTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StructuralTypes_Code",
                table: "StructuralTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubClassifications_Code",
                table: "SubClassifications",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_ActualUseId",
                table: "TaxDeclarations",
                column: "ActualUseId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_AssessmentYear",
                table: "TaxDeclarations",
                column: "AssessmentYear");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_ClassificationId",
                table: "TaxDeclarations",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_PreviousTaxDeclarationId",
                table: "TaxDeclarations",
                column: "PreviousTaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_PropertyId",
                table: "TaxDeclarations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_RpuId_EffectivityDate",
                table: "TaxDeclarations",
                columns: new[] { "RpuId", "EffectivityDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_SubClassificationId",
                table: "TaxDeclarations",
                column: "SubClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_TaxDeclarationNumber",
                table: "TaxDeclarations",
                column: "TaxDeclarationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_BarangayId",
                table: "Taxpayers",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_CorporateName",
                table: "Taxpayers",
                column: "CorporateName");

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_LastName",
                table: "Taxpayers",
                column: "LastName");

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_MunicipalityId",
                table: "Taxpayers",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_ProvinceId",
                table: "Taxpayers",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_Tin",
                table: "Taxpayers",
                column: "Tin");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Zones_Code",
                table: "Zones",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildingComponents");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Lands");

            migrationBuilder.DropTable(
                name: "MachineryUnits");

            migrationBuilder.DropTable(
                name: "Parcels");

            migrationBuilder.DropTable(
                name: "PropertyTaxpayers");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "TaxDeclarations");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "BuildingComponentTypes");

            migrationBuilder.DropTable(
                name: "Buildings");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropTable(
                name: "RoadTypes");

            migrationBuilder.DropTable(
                name: "MachineryTypes");

            migrationBuilder.DropTable(
                name: "OwnershipTypes");

            migrationBuilder.DropTable(
                name: "Taxpayers");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Classifications");

            migrationBuilder.DropTable(
                name: "SubClassifications");

            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "ActualUses");

            migrationBuilder.DropTable(
                name: "BuildingTypes");

            migrationBuilder.DropTable(
                name: "Conditions");

            migrationBuilder.DropTable(
                name: "RealPropertyUnit");

            migrationBuilder.DropTable(
                name: "StructuralTypes");

            migrationBuilder.DropTable(
                name: "Property");

            migrationBuilder.DropTable(
                name: "Barangays");

            migrationBuilder.DropTable(
                name: "Zones");

            migrationBuilder.DropTable(
                name: "Municipalities");

            migrationBuilder.DropTable(
                name: "Provinces");
        }
    }
}
