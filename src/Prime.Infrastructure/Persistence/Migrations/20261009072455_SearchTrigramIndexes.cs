using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Trigram indexes for the substring searches of the property register and the owner register, which match
    /// <c>lower(column) LIKE '%term%'</c> and scanned the whole table: with 250,000 DEMO properties a search took 0.6 s to
    /// count and up to 1.5 s to page (docs/analysis/production-hardening.md §9, H4). Expression indexes, so not in the model.
    /// </summary>
    public partial class SearchTrigramIndexes : Migration
    {
        private static readonly (string Table, string Column)[] Searched =
        [
            ("Property", "PropertyIdentificationNumber"), ("Property", "LotNumber"), ("Property", "TitleNumber"),
            ("Property", "SurveyNumber"), ("Property", "TaxMapNumber"),
            ("Taxpayers", "LastName"), ("Taxpayers", "FirstName"), ("Taxpayers", "CorporateName"), ("Taxpayers", "Tin"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            foreach (var (table, column) in Searched)
            {
                migrationBuilder.Sql($"""CREATE INDEX IF NOT EXISTS "IX_{table}_{column}_Trgm" ON "{table}" USING gin (lower("{column}") gin_trgm_ops);""");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in Searched)
            {
                migrationBuilder.Sql($"""DROP INDEX IF EXISTS "IX_{table}_{column}_Trgm";""");
            }

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
