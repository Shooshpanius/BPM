using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionOrganizationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_org_positions_department_id_code",
                table: "org_positions");

            // Добавляем колонку как nullable, чтобы корректно обработать существующие данные
            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "org_positions",
                type: "uuid",
                nullable: true,
                defaultValue: null);

            // Для существующих записей, привязанных к подразделению, заполняем organization_id
            // из организации соответствующего подразделения
            migrationBuilder.Sql(@"
                UPDATE org_positions p
                SET organization_id = d.organization_id
                FROM org_departments d
                WHERE p.department_id = d.id
                  AND p.organization_id IS NULL;
            ");

            // Для оставшихся записей без подразделения назначаем первую доступную организацию
            migrationBuilder.Sql(@"
                UPDATE org_positions
                SET organization_id = (SELECT id FROM org_organizations ORDER BY created_at LIMIT 1)
                WHERE organization_id IS NULL
                  AND (SELECT COUNT(*) FROM org_organizations) > 0;
            ");

            // Удаляем оставшиеся записи без организации (у которых нет ни подразделения,
            // ни ни одной организации в системе — теоретически невозможная ситуация)
            migrationBuilder.Sql(@"
                DELETE FROM org_positions WHERE organization_id IS NULL;
            ");

            // Делаем колонку обязательной
            migrationBuilder.AlterColumn<Guid>(
                name: "organization_id",
                table: "org_positions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_positions_department_id",
                table: "org_positions",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_positions_organization_id_code",
                table: "org_positions",
                columns: new[] { "organization_id", "code" },
                unique: true,
                filter: "code IS NOT NULL AND is_deleted = false");

            migrationBuilder.AddForeignKey(
                name: "fk_org_positions_org_organizations_organization_id",
                table: "org_positions",
                column: "organization_id",
                principalTable: "org_organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_org_positions_org_organizations_organization_id",
                table: "org_positions");

            migrationBuilder.DropIndex(
                name: "ix_org_positions_department_id",
                table: "org_positions");

            migrationBuilder.DropIndex(
                name: "ix_org_positions_organization_id_code",
                table: "org_positions");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "org_positions");

            migrationBuilder.CreateIndex(
                name: "ix_org_positions_department_id_code",
                table: "org_positions",
                columns: new[] { "department_id", "code" },
                unique: true,
                filter: "department_id IS NOT NULL AND code IS NOT NULL AND is_deleted = false");
        }
    }
}
