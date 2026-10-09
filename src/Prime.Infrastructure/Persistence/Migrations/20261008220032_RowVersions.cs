using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RowVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Model-only change (docs/analysis/production-hardening.md §4.4, H2): every IVersioned
            // entity's RowVersion maps to PostgreSQL's xmin system column. Npgsql emits no DDL for these
            // AddColumn calls (verified with `dotnet ef migrations script`, the ParcelConcurrencyToken
            // precedent); they exist so the model snapshot knows the row versions.
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Valuations",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "TransactionTypes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Taxpayers",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "TaxDeclarations",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "TaxDeclarationCancellationRequests",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SwornStatements",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SmvSchedules",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Smvs",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SmvPreparations",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SmvExtraItemCosts",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SmvDepreciationSchedules",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SmvBuildingCosts",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SignUpRequests",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "RolePermissionChanges",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "RevenueImpactStudies",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "RevenueAccountMappings",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "RealPropertyUnit",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PropertyTransactions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PropertyExemptions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Property",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Offices",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "OfficeJurisdictions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "OfficeAssignments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "NumberingSchemes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "NoticesOfCancellation",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "NoticesOfAssessment",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "MachineryUnits",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Lands",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "IndependentAppraisals",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "GeneralRevisionProgrammes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "GeneralRevisionChecklistStepDefinitions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "FormDefinitions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ExemptionTypes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "DiscoverySummonses",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Buildings",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Assessments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "AssessmentRollSubmissions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "AssessmentLevels",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "AssessmentLevelCeilings",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ApprovalDelegations",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ApprovalChains",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "AdjustmentFactors",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Valuations");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Taxpayers");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "TaxDeclarationCancellationRequests");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SwornStatements");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SmvSchedules");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SmvPreparations");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SmvExtraItemCosts");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SmvDepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SmvBuildingCosts");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SignUpRequests");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "RolePermissionChanges");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "RevenueImpactStudies");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "RevenueAccountMappings");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "RealPropertyUnit");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PropertyTransactions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PropertyExemptions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Offices");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "OfficeJurisdictions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "OfficeAssignments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "NumberingSchemes");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "NoticesOfCancellation");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "NoticesOfAssessment");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "MachineryUnits");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Lands");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "IndependentAppraisals");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "GeneralRevisionProgrammes");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "GeneralRevisionChecklistStepDefinitions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "FormDefinitions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ExemptionTypes");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "DiscoverySummonses");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "AssessmentRollSubmissions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "AssessmentLevels");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "AssessmentLevelCeilings");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ApprovalDelegations");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ApprovalChains");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "AdjustmentFactors");
        }
    }
}
