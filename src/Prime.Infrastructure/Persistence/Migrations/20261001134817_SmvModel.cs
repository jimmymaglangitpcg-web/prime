using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SmvModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PricedClassificationId",
                table: "ValuationLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PricedSubClassificationId",
                table: "ValuationLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActualUseId",
                table: "SmvSchedules",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "BarangayId",
                table: "SmvSchedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubClassificationId",
                table: "SmvSchedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrdinanceNumber",
                table: "Smvs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "OrdinanceDate",
                table: "Smvs",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<string>(
                name: "Basis",
                table: "Smvs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Ordinance"); // existing SMVs were enacted by ordinance

            migrationBuilder.AddColumn<string>(
                name: "CertificationReference",
                table: "Smvs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CertifiedOn",
                table: "Smvs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ConsultationsHeldOn",
                table: "Smvs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ProposedOn",
                table: "Smvs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicationReference",
                table: "Smvs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PublishedForCommentOn",
                table: "Smvs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PublishedOn",
                table: "Smvs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SubmittedToBlgfOn",
                table: "Smvs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValuationClassificationId",
                table: "LandStrips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValuationSubClassificationId",
                table: "LandStrips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "ContentImportItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);

            migrationBuilder.CreateTable(
                name: "SmvCoverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvCoverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmvCoverages_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvCoverages_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValuationLines_PricedClassificationId",
                table: "ValuationLines",
                column: "PricedClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationLines_PricedSubClassificationId",
                table: "ValuationLines",
                column: "PricedSubClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSchedules_BarangayId",
                table: "SmvSchedules",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSchedules_SubClassificationId",
                table: "SmvSchedules",
                column: "SubClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_Smvs_CertificationReference",
                table: "Smvs",
                column: "CertificationReference",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Smvs_CertifiedBasis",
                table: "Smvs",
                sql: "\"Basis\" <> 'Certified' OR \"CertificationReference\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Smvs_OrdinanceBasis",
                table: "Smvs",
                sql: "\"Basis\" <> 'Ordinance' OR (\"OrdinanceNumber\" IS NOT NULL AND \"OrdinanceDate\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_LandStrips_ValuationClassificationId",
                table: "LandStrips",
                column: "ValuationClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_LandStrips_ValuationSubClassificationId",
                table: "LandStrips",
                column: "ValuationSubClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvCoverages_MunicipalityId",
                table: "SmvCoverages",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvCoverages_SmvId_MunicipalityId",
                table: "SmvCoverages",
                columns: new[] { "SmvId", "MunicipalityId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LandStrips_Classifications_ValuationClassificationId",
                table: "LandStrips",
                column: "ValuationClassificationId",
                principalTable: "Classifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LandStrips_SubClassifications_ValuationSubClassificationId",
                table: "LandStrips",
                column: "ValuationSubClassificationId",
                principalTable: "SubClassifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SmvSchedules_Barangays_BarangayId",
                table: "SmvSchedules",
                column: "BarangayId",
                principalTable: "Barangays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SmvSchedules_SubClassifications_SubClassificationId",
                table: "SmvSchedules",
                column: "SubClassificationId",
                principalTable: "SubClassifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ValuationLines_Classifications_PricedClassificationId",
                table: "ValuationLines",
                column: "PricedClassificationId",
                principalTable: "Classifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ValuationLines_SubClassifications_PricedSubClassificationId",
                table: "ValuationLines",
                column: "PricedSubClassificationId",
                principalTable: "SubClassifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LandStrips_Classifications_ValuationClassificationId",
                table: "LandStrips");

            migrationBuilder.DropForeignKey(
                name: "FK_LandStrips_SubClassifications_ValuationSubClassificationId",
                table: "LandStrips");

            migrationBuilder.DropForeignKey(
                name: "FK_SmvSchedules_Barangays_BarangayId",
                table: "SmvSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_SmvSchedules_SubClassifications_SubClassificationId",
                table: "SmvSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ValuationLines_Classifications_PricedClassificationId",
                table: "ValuationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ValuationLines_SubClassifications_PricedSubClassificationId",
                table: "ValuationLines");

            migrationBuilder.DropTable(
                name: "SmvCoverages");

            migrationBuilder.DropIndex(
                name: "IX_ValuationLines_PricedClassificationId",
                table: "ValuationLines");

            migrationBuilder.DropIndex(
                name: "IX_ValuationLines_PricedSubClassificationId",
                table: "ValuationLines");

            migrationBuilder.DropIndex(
                name: "IX_SmvSchedules_BarangayId",
                table: "SmvSchedules");

            migrationBuilder.DropIndex(
                name: "IX_SmvSchedules_SubClassificationId",
                table: "SmvSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Smvs_CertificationReference",
                table: "Smvs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Smvs_CertifiedBasis",
                table: "Smvs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Smvs_OrdinanceBasis",
                table: "Smvs");

            migrationBuilder.DropIndex(
                name: "IX_LandStrips_ValuationClassificationId",
                table: "LandStrips");

            migrationBuilder.DropIndex(
                name: "IX_LandStrips_ValuationSubClassificationId",
                table: "LandStrips");

            migrationBuilder.DropColumn(
                name: "PricedClassificationId",
                table: "ValuationLines");

            migrationBuilder.DropColumn(
                name: "PricedSubClassificationId",
                table: "ValuationLines");

            migrationBuilder.DropColumn(
                name: "BarangayId",
                table: "SmvSchedules");

            migrationBuilder.DropColumn(
                name: "SubClassificationId",
                table: "SmvSchedules");

            migrationBuilder.DropColumn(
                name: "Basis",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "CertificationReference",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "CertifiedOn",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "ConsultationsHeldOn",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "ProposedOn",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "PublicationReference",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "PublishedForCommentOn",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "PublishedOn",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "SubmittedToBlgfOn",
                table: "Smvs");

            migrationBuilder.DropColumn(
                name: "ValuationClassificationId",
                table: "LandStrips");

            migrationBuilder.DropColumn(
                name: "ValuationSubClassificationId",
                table: "LandStrips");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActualUseId",
                table: "SmvSchedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrdinanceNumber",
                table: "Smvs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "OrdinanceDate",
                table: "Smvs",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "ContentImportItems",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }
    }
}
