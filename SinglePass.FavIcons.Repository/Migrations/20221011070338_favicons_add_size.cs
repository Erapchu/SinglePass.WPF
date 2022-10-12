using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SinglePass.FavIcons.Repository.Migrations
{
    public partial class favicons_add_size : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Size",
                table: "FavIcons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Size",
                table: "FavIcons");
        }
    }
}
