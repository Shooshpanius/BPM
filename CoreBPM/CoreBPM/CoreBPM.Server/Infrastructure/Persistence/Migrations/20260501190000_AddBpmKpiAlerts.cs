using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmKpiAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bpm_kpi_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    avg_cycle_time_minutes = table.Column<double>(type: "double precision", nullable: false),
                    target_cycle_time_minutes = table.Column<double>(type: "double precision", nullable: false),
                    exceed_percent = table.Column<double>(type: "double precision", nullable: false),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_kpi_alerts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_kpi_alerts_detected_at",
                table: "bpm_kpi_alerts",
                column: "detected_at");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_kpi_alerts_process_id",
                table: "bpm_kpi_alerts",
                column: "process_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "bpm_kpi_alerts");
        }
    }
}
