using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ParcelConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Model-only change: Parcel.Version maps to PostgreSQL's xmin
            // system column. Npgsql's SQL generator emits no DDL for this
            // AddColumn (verified via `dotnet ef migrations script`) — it
            // exists so the model snapshot knows about the row version.
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Parcels",
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
                table: "Parcels");
        }
    }
}
