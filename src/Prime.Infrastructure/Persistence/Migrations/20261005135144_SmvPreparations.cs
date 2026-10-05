using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SmvPreparations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Smvs_CertifiedBasis",
                table: "Smvs");

            migrationBuilder.CreateTable(
                name: "SmvPreparations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionYear = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DateOfValuation = table.Column<DateOnly>(type: "date", nullable: true),
                    BaseValuationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProposedSmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvPreparations", x => x.Id);
                    table.CheckConstraint("CK_SmvPreparations_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancellationReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SmvPreparations_Smvs_ProposedSmvId",
                        column: x => x.ProposedSmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmvConsultations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvPreparationId = table.Column<Guid>(type: "uuid", nullable: false),
                    HeldOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Venue = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Attendance = table.Column<int>(type: "integer", nullable: true),
                    MinutesReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvConsultations", x => x.Id);
                    table.CheckConstraint("CK_SmvConsultations_Attendance", "\"Attendance\" IS NULL OR \"Attendance\" >= 0");
                    table.ForeignKey(
                        name: "FK_SmvConsultations_SmvPreparations_SmvPreparationId",
                        column: x => x.SmvPreparationId,
                        principalTable: "SmvPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmvPreparationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvPreparationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvPreparationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmvPreparationEvents_SmvPreparations_SmvPreparationId",
                        column: x => x.SmvPreparationId,
                        principalTable: "SmvPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Smvs_CertifiedBasis",
                table: "Smvs",
                sql: "\"Basis\" <> 'Certified' OR \"CertificationReference\" IS NOT NULL OR \"Status\" IN ('Draft', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_SmvConsultations_SmvPreparationId",
                table: "SmvConsultations",
                column: "SmvPreparationId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvPreparationEvents_SmvPreparationId_OccurredOn",
                table: "SmvPreparationEvents",
                columns: new[] { "SmvPreparationId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_SmvPreparations_ProposedSmvId",
                table: "SmvPreparations",
                column: "ProposedSmvId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmvPreparations_RevisionYear",
                table: "SmvPreparations",
                column: "RevisionYear",
                unique: true,
                filter: "\"Status\" <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmvConsultations");

            migrationBuilder.DropTable(
                name: "SmvPreparationEvents");

            migrationBuilder.DropTable(
                name: "SmvPreparations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Smvs_CertifiedBasis",
                table: "Smvs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Smvs_CertifiedBasis",
                table: "Smvs",
                sql: "\"Basis\" <> 'Certified' OR \"CertificationReference\" IS NOT NULL");
        }
    }
}
