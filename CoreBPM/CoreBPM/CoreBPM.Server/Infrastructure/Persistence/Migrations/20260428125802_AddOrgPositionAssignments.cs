using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgPositionAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "org_position_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_position_assignments", x => x.id);
                    table.CheckConstraint("ck_org_position_assignments_dates", "end_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("ck_org_position_assignments_rate", "rate IN (0.25, 0.50, 0.75, 1.00)");
                    table.ForeignKey(
                        name: "fk_org_position_assignments_org_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "org_organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_org_position_assignments_org_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "org_positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_org_position_assignments_org_users_user_id",
                        column: x => x.user_id,
                        principalTable: "org_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_org_position_assignments_organization_id",
                table: "org_position_assignments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_position_assignments_position_id_end_date",
                table: "org_position_assignments",
                columns: new[] { "position_id", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_org_position_assignments_user_id",
                table: "org_position_assignments",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "org_position_assignments");
        }
    }
}
