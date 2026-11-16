using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SinglePass.FavIcons.Repository.Migrations
{
    /// <inheritdoc />
    public partial class FaviconRefreshStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckedUtc",
                table: "FavIcons",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedUtc",
                table: "FavIcons",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FavIcons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0); // FavIconStatus.Missing

            // Keep one legacy row per host and size, preferring a row with an image.
            migrationBuilder.Sql("""
                DELETE FROM FavIcons
                WHERE rowid IN (
                    SELECT RowId FROM (
                        SELECT rowid AS RowId,
                               ROW_NUMBER() OVER (
                                   PARTITION BY Host, Size
                                   ORDER BY (length(Bytes) > 0) DESC, rowid DESC
                               ) AS DuplicateRank
                        FROM FavIcons
                        WHERE Host IS NOT NULL
                    )
                    WHERE DuplicateRank > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_FavIcons_Host_Size",
                table: "FavIcons",
                columns: new[] { "Host", "Size" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FavIcons_Host_Size",
                table: "FavIcons");

            migrationBuilder.DropColumn(
                name: "LastCheckedUtc",
                table: "FavIcons");

            migrationBuilder.DropColumn(
                name: "LastUpdatedUtc",
                table: "FavIcons");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FavIcons");
        }
    }
}
