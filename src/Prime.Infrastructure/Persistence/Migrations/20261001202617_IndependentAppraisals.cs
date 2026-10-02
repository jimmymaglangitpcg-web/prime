using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndependentAppraisals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IndependentAppraisalId",
                table: "ValuationLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IndependentAppraisals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RpuId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Approach = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AppraisedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Basis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    EndReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndependentAppraisals", x => x.Id);
                    table.CheckConstraint("CK_IndependentAppraisals_Value", "\"Value\" >= 0");
                    table.ForeignKey(
                        name: "FK_IndependentAppraisals_RealPropertyUnit_RpuId",
                        column: x => x.RpuId,
                        principalTable: "RealPropertyUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IndependentAppraisalInputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IndependentAppraisalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndependentAppraisalInputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndependentAppraisalInputs_IndependentAppraisals_Independen~",
                        column: x => x.IndependentAppraisalId,
                        principalTable: "IndependentAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValuationLines_IndependentAppraisalId",
                table: "ValuationLines",
                column: "IndependentAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_IndependentAppraisalInputs_IndependentAppraisalId_Sequence",
                table: "IndependentAppraisalInputs",
                columns: new[] { "IndependentAppraisalId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndependentAppraisals_RpuId",
                table: "IndependentAppraisals",
                column: "RpuId");

            migrationBuilder.CreateIndex(
                name: "UX_IndependentAppraisals_Current",
                table: "IndependentAppraisals",
                columns: new[] { "Subject", "SubjectId" },
                unique: true,
                filter: "\"IsCurrent\"");

            migrationBuilder.AddForeignKey(
                name: "FK_ValuationLines_IndependentAppraisals_IndependentAppraisalId",
                table: "ValuationLines",
                column: "IndependentAppraisalId",
                principalTable: "IndependentAppraisals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ValuationLines_IndependentAppraisals_IndependentAppraisalId",
                table: "ValuationLines");

            migrationBuilder.DropTable(
                name: "IndependentAppraisalInputs");

            migrationBuilder.DropTable(
                name: "IndependentAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_ValuationLines_IndependentAppraisalId",
                table: "ValuationLines");

            migrationBuilder.DropColumn(
                name: "IndependentAppraisalId",
                table: "ValuationLines");
        }
    }
}
