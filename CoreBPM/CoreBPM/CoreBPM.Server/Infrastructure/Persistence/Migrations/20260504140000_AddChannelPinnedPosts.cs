using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelPinnedPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // notify_channel_pinned_posts — закреплённые публикации канала
            migrationBuilder.CreateTable(
                name: "notify_channel_pinned_posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_channel_pinned_posts", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_channel_pinned_posts_channel",
                        column: x => x.channel_id,
                        principalTable: "notify_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notify_channel_pinned_posts_post",
                        column: x => x.post_id,
                        principalTable: "notify_channel_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_channel_pinned_posts_channel_id",
                table: "notify_channel_pinned_posts",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "ix_notify_channel_pinned_posts_channel_post",
                table: "notify_channel_pinned_posts",
                columns: new[] { "channel_id", "post_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "notify_channel_pinned_posts");
        }
    }
}
