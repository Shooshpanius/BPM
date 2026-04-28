using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PositionDepartmentOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Удаляем старый FK (Restrict) и уникальный индекс
            migrationBuilder.DropForeignKey(
                name: "fk_org_positions_org_departments_department_id",
                table: "org_positions");

            migrationBuilder.DropIndex(
                name: "ix_org_positions_department_id_code",
                table: "org_positions");

            // Делаем колонку необязательной
            migrationBuilder.AlterColumn<Guid>(
                name: "department_id",
                table: "org_positions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // Восстанавливаем уникальный индекс с обновлённым фильтром
            migrationBuilder.CreateIndex(
                name: "ix_org_positions_department_id_code",
                table: "org_positions",
                columns: new[] { "department_id", "code" },
                unique: true,
                filter: "department_id IS NOT NULL AND code IS NOT NULL AND is_deleted = false");

            // Восстанавливаем FK с SetNull при удалении подразделения
            migrationBuilder.AddForeignKey(
                name: "fk_org_positions_org_departments_department_id",
                table: "org_positions",
                column: "department_id",
                principalTable: "org_departments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_org_positions_org_departments_department_id",
                table: "org_positions");

            migrationBuilder.DropIndex(
                name: "ix_org_positions_department_id_code",
                table: "org_positions");

            migrationBuilder.AlterColumn<Guid>(
                name: "department_id",
                table: "org_positions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_positions_department_id_code",
                table: "org_positions",
                columns: new[] { "department_id", "code" },
                unique: true,
                filter: "code IS NOT NULL AND is_deleted = false");

            migrationBuilder.AddForeignKey(
                name: "fk_org_positions_org_departments_department_id",
                table: "org_positions",
                column: "department_id",
                principalTable: "org_departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
