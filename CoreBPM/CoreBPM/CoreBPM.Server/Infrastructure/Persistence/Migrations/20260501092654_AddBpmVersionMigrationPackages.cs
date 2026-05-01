using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmVersionMigrationPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bpm_version_migration_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_version_migration_packages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bpm_version_migration_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    error_comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    manual_change_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bpm_version_migration_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_bpm_version_migration_items_bpm_instances_instance_id",
                        column: x => x.instance_id,
                        principalTable: "bpm_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bpm_version_migration_items_bpm_process_versions_target_ver",
                        column: x => x.target_version_id,
                        principalTable: "bpm_process_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bpm_version_migration_items_bpm_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "bpm_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bpm_version_migration_items_bpm_version_migration_packages_",
                        column: x => x.package_id,
                        principalTable: "bpm_version_migration_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpm_version_migration_items_instance_id",
                table: "bpm_version_migration_items",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_version_migration_items_package_id",
                table: "bpm_version_migration_items",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_version_migration_items_process_id",
                table: "bpm_version_migration_items",
                column: "process_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_version_migration_items_status",
                table: "bpm_version_migration_items",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_version_migration_items_target_version_id",
                table: "bpm_version_migration_items",
                column: "target_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_version_migration_packages_created_by_user_id",
                table: "bpm_version_migration_packages",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_bpm_version_migration_packages_status",
                table: "bpm_version_migration_packages",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bpm_version_migration_items");

            migrationBuilder.DropTable(
                name: "bpm_version_migration_packages");
        }
    }
}
