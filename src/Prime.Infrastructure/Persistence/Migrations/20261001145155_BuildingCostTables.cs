using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuildingCostTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BuildingComponents_AdditionalItemCost",
                table: "BuildingComponents");

            migrationBuilder.AddColumn<Guid>(
                name: "TransactionTypeId",
                table: "Valuations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsNewDepreciation",
                table: "TransactionTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SmvBuildingCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    StructuralTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CostPerSquareMetre = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvBuildingCosts", x => x.Id);
                    table.CheckConstraint("CK_SmvBuildingCosts_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_SmvBuildingCosts_Cost", "\"CostPerSquareMetre\" > 0");
                    table.CheckConstraint("CK_SmvBuildingCosts_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                    table.ForeignKey(
                        name: "FK_SmvBuildingCosts_BuildingTypes_BuildingTypeId",
                        column: x => x.BuildingTypeId,
                        principalTable: "BuildingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvBuildingCosts_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvBuildingCosts_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvBuildingCosts_StructuralTypes_StructuralTypeId",
                        column: x => x.StructuralTypeId,
                        principalTable: "StructuralTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmvDepreciationSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    StructuralTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reading = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MinimumRemainingPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvDepreciationSchedules", x => x.Id);
                    table.CheckConstraint("CK_SmvDepreciationSchedules_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_SmvDepreciationSchedules_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                    table.CheckConstraint("CK_SmvDepreciationSchedules_Remaining", "\"MinimumRemainingPercent\" >= 0 AND \"MinimumRemainingPercent\" <= 100");
                    table.ForeignKey(
                        name: "FK_SmvDepreciationSchedules_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvDepreciationSchedules_StructuralTypes_StructuralTypeId",
                        column: x => x.StructuralTypeId,
                        principalTable: "StructuralTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmvExtraItemCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvExtraItemCosts", x => x.Id);
                    table.CheckConstraint("CK_SmvExtraItemCosts_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_SmvExtraItemCosts_Cost", "\"UnitCost\" > 0");
                    table.CheckConstraint("CK_SmvExtraItemCosts_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
                    table.ForeignKey(
                        name: "FK_SmvExtraItemCosts_BuildingComponentTypes_ComponentTypeId",
                        column: x => x.ComponentTypeId,
                        principalTable: "BuildingComponentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvExtraItemCosts_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmvDepreciationRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvDepreciationScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    FromAge = table.Column<int>(type: "integer", nullable: false),
                    ToAge = table.Column<int>(type: "integer", nullable: true),
                    Percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvDepreciationRows", x => x.Id);
                    table.CheckConstraint("CK_SmvDepreciationRows_Ages", "\"FromAge\" >= 0 AND (\"ToAge\" IS NULL OR \"ToAge\" >= \"FromAge\")");
                    table.CheckConstraint("CK_SmvDepreciationRows_Percent", "\"Percent\" >= 0 AND \"Percent\" <= 100");
                    table.ForeignKey(
                        name: "FK_SmvDepreciationRows_SmvDepreciationSchedules_SmvDepreciatio~",
                        column: x => x.SmvDepreciationScheduleId,
                        principalTable: "SmvDepreciationSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Valuations_TransactionTypeId",
                table: "Valuations",
                column: "TransactionTypeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BuildingComponents_AdditionalItemCost",
                table: "BuildingComponents",
                sql: "NOT \"IsAdditionalItem\" OR \"Cost\" IS NOT NULL OR \"Quantity\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SmvBuildingCosts_BuildingTypeId",
                table: "SmvBuildingCosts",
                column: "BuildingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvBuildingCosts_ClassificationId",
                table: "SmvBuildingCosts",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvBuildingCosts_Status_EffectiveDate",
                table: "SmvBuildingCosts",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SmvBuildingCosts_StructuralTypeId",
                table: "SmvBuildingCosts",
                column: "StructuralTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_SmvBuildingCosts_OpenApproved",
                table: "SmvBuildingCosts",
                columns: new[] { "SmvId", "StructuralTypeId", "BuildingTypeId", "ClassificationId" },
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_SmvDepreciationRows_SmvDepreciationScheduleId_Sequence",
                table: "SmvDepreciationRows",
                columns: new[] { "SmvDepreciationScheduleId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmvDepreciationSchedules_Status_EffectiveDate",
                table: "SmvDepreciationSchedules",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SmvDepreciationSchedules_StructuralTypeId",
                table: "SmvDepreciationSchedules",
                column: "StructuralTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_SmvDepreciationSchedules_OpenApproved",
                table: "SmvDepreciationSchedules",
                columns: new[] { "SmvId", "StructuralTypeId" },
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SmvExtraItemCosts_ComponentTypeId",
                table: "SmvExtraItemCosts",
                column: "ComponentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvExtraItemCosts_Status_EffectiveDate",
                table: "SmvExtraItemCosts",
                columns: new[] { "Status", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UX_SmvExtraItemCosts_OpenApproved",
                table: "SmvExtraItemCosts",
                columns: new[] { "SmvId", "ComponentTypeId" },
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"EndDate\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Valuations_TransactionTypes_TransactionTypeId",
                table: "Valuations",
                column: "TransactionTypeId",
                principalTable: "TransactionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Valuations_TransactionTypes_TransactionTypeId",
                table: "Valuations");

            migrationBuilder.DropTable(
                name: "SmvBuildingCosts");

            migrationBuilder.DropTable(
                name: "SmvDepreciationRows");

            migrationBuilder.DropTable(
                name: "SmvExtraItemCosts");

            migrationBuilder.DropTable(
                name: "SmvDepreciationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Valuations_TransactionTypeId",
                table: "Valuations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BuildingComponents_AdditionalItemCost",
                table: "BuildingComponents");

            migrationBuilder.DropColumn(
                name: "TransactionTypeId",
                table: "Valuations");

            migrationBuilder.DropColumn(
                name: "AllowsNewDepreciation",
                table: "TransactionTypes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BuildingComponents_AdditionalItemCost",
                table: "BuildingComponents",
                sql: "NOT \"IsAdditionalItem\" OR \"Cost\" IS NOT NULL");
        }
    }
}
