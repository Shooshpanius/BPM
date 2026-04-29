using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDmnTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rules_dmn_tables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    hit_policy = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rules_dmn_tables", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rules_dmn_table_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rules_dmn_table_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_rules_dmn_table_versions_rules_dmn_tables_table_id",
                        column: x => x.table_id,
                        principalTable: "rules_dmn_tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rules_dmn_columns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    column_kind = table.Column<int>(type: "integer", nullable: false),
                    value_type = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rules_dmn_columns", x => x.id);
                    table.ForeignKey(
                        name: "fk_rules_dmn_columns_rules_dmn_table_versions_version_id",
                        column: x => x.version_id,
                        principalTable: "rules_dmn_table_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rules_dmn_rows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rules_dmn_rows", x => x.id);
                    table.ForeignKey(
                        name: "fk_rules_dmn_rows_rules_dmn_table_versions_version_id",
                        column: x => x.version_id,
                        principalTable: "rules_dmn_table_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rules_dmn_cells",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_id = table.Column<Guid>(type: "uuid", nullable: false),
                    column_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    annotation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rules_dmn_cells", x => x.id);
                    table.ForeignKey(
                        name: "fk_rules_dmn_cells_rules_dmn_columns_column_id",
                        column: x => x.column_id,
                        principalTable: "rules_dmn_columns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rules_dmn_cells_rules_dmn_rows_row_id",
                        column: x => x.row_id,
                        principalTable: "rules_dmn_rows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rules_dmn_cells_column_id",
                table: "rules_dmn_cells",
                column: "column_id");

            migrationBuilder.CreateIndex(
                name: "ix_rules_dmn_cells_row_id_column_id",
                table: "rules_dmn_cells",
                columns: new[] { "row_id", "column_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rules_dmn_columns_version_id_order",
                table: "rules_dmn_columns",
                columns: new[] { "version_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_rules_dmn_rows_version_id_order",
                table: "rules_dmn_rows",
                columns: new[] { "version_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_rules_dmn_table_versions_table_id_version_number",
                table: "rules_dmn_table_versions",
                columns: new[] { "table_id", "version_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rules_dmn_cells");

            migrationBuilder.DropTable(
                name: "rules_dmn_columns");

            migrationBuilder.DropTable(
                name: "rules_dmn_rows");

            migrationBuilder.DropTable(
                name: "rules_dmn_table_versions");

            migrationBuilder.DropTable(
                name: "rules_dmn_tables");
        }
    }
}
