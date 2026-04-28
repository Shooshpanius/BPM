using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropEmployeePositionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_org_employees_org_positions_position_id",
                table: "org_employees");

            migrationBuilder.DropIndex(
                name: "ix_org_employees_position_id",
                table: "org_employees");

            migrationBuilder.DropColumn(
                name: "position_id",
                table: "org_employees");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "position_id",
                table: "org_employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_employees_position_id",
                table: "org_employees",
                column: "position_id");

            migrationBuilder.AddForeignKey(
                name: "fk_org_employees_org_positions_position_id",
                table: "org_employees",
                column: "position_id",
                principalTable: "org_positions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
