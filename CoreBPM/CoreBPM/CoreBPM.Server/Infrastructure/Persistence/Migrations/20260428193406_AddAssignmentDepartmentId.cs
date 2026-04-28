using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentDepartmentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "org_position_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_position_assignments_department_id",
                table: "org_position_assignments",
                column: "department_id");

            migrationBuilder.AddForeignKey(
                name: "fk_org_position_assignments_org_departments_department_id",
                table: "org_position_assignments",
                column: "department_id",
                principalTable: "org_departments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_org_position_assignments_org_departments_department_id",
                table: "org_position_assignments");

            migrationBuilder.DropIndex(
                name: "ix_org_position_assignments_department_id",
                table: "org_position_assignments");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "org_position_assignments");
        }
    }
}
