using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmProcessKpi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "target_cycle_time_minutes",
                table: "bpm_processes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "target_on_time_percent",
                table: "bpm_processes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "target_cost_per_instance",
                table: "bpm_processes",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "target_cycle_time_minutes",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "target_on_time_percent",
                table: "bpm_processes");

            migrationBuilder.DropColumn(
                name: "target_cost_per_instance",
                table: "bpm_processes");
        }
    }
}
