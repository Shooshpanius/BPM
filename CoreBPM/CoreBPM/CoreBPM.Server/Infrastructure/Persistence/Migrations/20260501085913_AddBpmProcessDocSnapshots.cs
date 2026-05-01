using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmProcessDocSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bpm_process_doc_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    html_content = table.Column<string>(type: "text", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    generated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_process_doc_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_process_doc_snapshots_bpm_process_versions_process_vers",
                        column: x => x.process_version_id,
                        principalTable: "bpm_process_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bpm_process_doc_snapshots_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_process_doc_snapshots_process_id",
                table: "bpm_process_doc_snapshots",
                column: "process_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_process_doc_snapshots_process_version_id",
                table: "bpm_process_doc_snapshots",
                column: "process_version_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bpm_process_doc_snapshots");
        }
    }
}
