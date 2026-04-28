using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropEmployeeDepartmentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_org_employees_org_departments_department_id",
                table: "org_employees");

            migrationBuilder.DropIndex(
                name: "ix_org_employees_department_id",
                table: "org_employees");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "org_employees");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "org_employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_employees_department_id",
                table: "org_employees",
                column: "department_id");

            migrationBuilder.AddForeignKey(
                name: "fk_org_employees_org_departments_department_id",
                table: "org_employees",
                column: "department_id",
                principalTable: "org_departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
