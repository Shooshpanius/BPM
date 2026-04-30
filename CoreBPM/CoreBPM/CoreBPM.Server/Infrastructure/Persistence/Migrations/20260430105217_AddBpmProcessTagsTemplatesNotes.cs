using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmProcessTagsTemplatesNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_template",
                table: "bpm_processes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "tags_json",
                table: "bpm_processes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "release_notes",
                table: "bpm_process_versions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_template",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "tags_json",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "release_notes",
                table: "bpm_process_versions");
        }
    }
}
