using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelPostReactionsAndComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // notify_post_reactions — реакции на публикации канала
            migrationBuilder.CreateTable(
                name: "notify_post_reactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emoji = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_post_reactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_post_reactions_post",
                        column: x => x.post_id,
                        principalTable: "notify_channel_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_post_reactions_post_user_emoji",
                table: "notify_post_reactions",
                columns: new[] { "post_id", "user_id", "emoji" },
                unique: true);

            // notify_post_comments — комментарии к публикациям канала
            migrationBuilder.CreateTable(
                name: "notify_post_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_post_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_post_comments_post",
                        column: x => x.post_id,
                        principalTable: "notify_channel_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_post_comments_post_id",
                table: "notify_post_comments",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "ix_notify_post_comments_created_at",
                table: "notify_post_comments",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "notify_post_reactions");
            migrationBuilder.DropTable(name: "notify_post_comments");
        }
    }
}
