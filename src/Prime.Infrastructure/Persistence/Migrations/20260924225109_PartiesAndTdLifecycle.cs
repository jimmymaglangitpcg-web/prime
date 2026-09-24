using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartiesAndTdLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "TaxDeclarations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "TaxDeclarations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledBy",
                table: "TaxDeclarations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersededByTaxDeclarationId",
                table: "TaxDeclarations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TaxpayerId",
                table: "PropertyTaxpayers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnershipTypeId",
                table: "PropertyTaxpayers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "PropertyTaxpayers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "PropertyTaxpayers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Owner");

            migrationBuilder.CreateTable(
                name: "AnnotationTypes",
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
                    table.PrimaryKey("PK_AnnotationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxDeclarationAnnotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnnotationTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LiftedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LiftedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LiftReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LiftReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxDeclarationAnnotations", x => x.Id);
                    table.CheckConstraint("CK_TaxDeclarationAnnotations_Lifted", "(\"LiftedAt\" IS NULL) = (\"LiftReason\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_TaxDeclarationAnnotations_AnnotationTypes_AnnotationTypeId",
                        column: x => x.AnnotationTypeId,
                        principalTable: "AnnotationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxDeclarationAnnotations_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_SupersededByTaxDeclarationId",
                table: "TaxDeclarations",
                column: "SupersededByTaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "UX_TaxDeclarations_Rpu_Approved",
                table: "TaxDeclarations",
                column: "RpuId",
                unique: true,
                filter: "\"Status\" = 'Approved'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxDeclarations_Cancelled",
                table: "TaxDeclarations",
                sql: "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UX_PropertyTaxpayers_CurrentUnknownOwner",
                table: "PropertyTaxpayers",
                column: "PropertyId",
                unique: true,
                filter: "\"Role\" = 'UnknownOwner' AND \"IsCurrent\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PropertyTaxpayers_Ended",
                table: "PropertyTaxpayers",
                sql: "\"IsCurrent\" OR \"EndDate\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PropertyTaxpayers_OwnershipType",
                table: "PropertyTaxpayers",
                sql: "(\"Role\" = 'Owner') = (\"OwnershipTypeId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PropertyTaxpayers_Share",
                table: "PropertyTaxpayers",
                sql: "\"OwnershipPercentage\" >= 0 AND \"OwnershipPercentage\" <= 100 AND (\"Role\" <> 'UnknownOwner' OR \"OwnershipPercentage\" = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PropertyTaxpayers_UnknownOwner",
                table: "PropertyTaxpayers",
                sql: "(\"Role\" = 'UnknownOwner') = (\"TaxpayerId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AnnotationTypes_Code",
                table: "AnnotationTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationAnnotations_AnnotationTypeId",
                table: "TaxDeclarationAnnotations",
                column: "AnnotationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationAnnotations_TaxDeclarationId_EffectiveDate",
                table: "TaxDeclarationAnnotations",
                columns: new[] { "TaxDeclarationId", "EffectiveDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_TaxDeclarations_TaxDeclarations_SupersededByTaxDeclarationId",
                table: "TaxDeclarations",
                column: "SupersededByTaxDeclarationId",
                principalTable: "TaxDeclarations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaxDeclarations_TaxDeclarations_SupersededByTaxDeclarationId",
                table: "TaxDeclarations");

            migrationBuilder.DropTable(
                name: "TaxDeclarationAnnotations");

            migrationBuilder.DropTable(
                name: "AnnotationTypes");

            migrationBuilder.DropIndex(
                name: "IX_TaxDeclarations_SupersededByTaxDeclarationId",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "UX_TaxDeclarations_Rpu_Approved",
                table: "TaxDeclarations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxDeclarations_Cancelled",
                table: "TaxDeclarations");

            migrationBuilder.DropIndex(
                name: "UX_PropertyTaxpayers_CurrentUnknownOwner",
                table: "PropertyTaxpayers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PropertyTaxpayers_Ended",
                table: "PropertyTaxpayers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PropertyTaxpayers_OwnershipType",
                table: "PropertyTaxpayers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PropertyTaxpayers_Share",
                table: "PropertyTaxpayers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PropertyTaxpayers_UnknownOwner",
                table: "PropertyTaxpayers");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "SupersededByTaxDeclarationId",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "PropertyTaxpayers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "PropertyTaxpayers");

            migrationBuilder.AlterColumn<Guid>(
                name: "TaxpayerId",
                table: "PropertyTaxpayers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnershipTypeId",
                table: "PropertyTaxpayers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
