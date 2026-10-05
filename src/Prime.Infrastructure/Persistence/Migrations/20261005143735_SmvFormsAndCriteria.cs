using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SmvFormsAndCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CropDescription",
                table: "SmvSchedules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationDescription",
                table: "SmvSchedules",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SmvSubClassCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SmvId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubClassificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Criteria = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmvSubClassCriteria", x => x.Id);
                    table.CheckConstraint("CK_SmvSubClassCriteria_Sequence", "\"Sequence\" > 0");
                    table.ForeignKey(
                        name: "FK_SmvSubClassCriteria_Classifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "Classifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSubClassCriteria_Smvs_SmvId",
                        column: x => x.SmvId,
                        principalTable: "Smvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmvSubClassCriteria_SubClassifications_SubClassificationId",
                        column: x => x.SubClassificationId,
                        principalTable: "SubClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmvSubClassCriteria_ClassificationId",
                table: "SmvSubClassCriteria",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SmvSubClassCriteria_SmvId_ClassificationId_SubClassificatio~",
                table: "SmvSubClassCriteria",
                columns: new[] { "SmvId", "ClassificationId", "SubClassificationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmvSubClassCriteria_SubClassificationId",
                table: "SmvSubClassCriteria",
                column: "SubClassificationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmvSubClassCriteria");

            migrationBuilder.DropColumn(
                name: "CropDescription",
                table: "SmvSchedules");

            migrationBuilder.DropColumn(
                name: "LocationDescription",
                table: "SmvSchedules");
        }
    }
}
