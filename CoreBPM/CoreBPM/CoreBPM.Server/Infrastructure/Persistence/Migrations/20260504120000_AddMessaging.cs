using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // notify_chats
            migrationBuilder.CreateTable(
                name: "notify_chats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_chats", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_chats_last_message_at",
                table: "notify_chats",
                column: "last_message_at");

            // notify_chat_members
            migrationBuilder.CreateTable(
                name: "notify_chat_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_muted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_chat_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_chat_members_chat",
                        column: x => x.chat_id,
                        principalTable: "notify_chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_chat_members_chat_id_user_id",
                table: "notify_chat_members",
                columns: new[] { "chat_id", "user_id" },
                unique: true);

            // notify_messages
            migrationBuilder.CreateTable(
                name: "notify_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    is_edited = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reply_to_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_messages_chat",
                        column: x => x.chat_id,
                        principalTable: "notify_chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notify_messages_reply_to",
                        column: x => x.reply_to_message_id,
                        principalTable: "notify_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_messages_chat_id",
                table: "notify_messages",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "ix_notify_messages_created_at",
                table: "notify_messages",
                column: "created_at");

            // notify_message_reads
            migrationBuilder.CreateTable(
                name: "notify_message_reads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_message_reads", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_message_reads_message",
                        column: x => x.message_id,
                        principalTable: "notify_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_message_reads_message_id_user_id",
                table: "notify_message_reads",
                columns: new[] { "message_id", "user_id" },
                unique: true);

            // notify_message_reactions
            migrationBuilder.CreateTable(
                name: "notify_message_reactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emoji = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_message_reactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_message_reactions_message",
                        column: x => x.message_id,
                        principalTable: "notify_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_message_reactions_message_id_user_id_emoji",
                table: "notify_message_reactions",
                columns: new[] { "message_id", "user_id", "emoji" },
                unique: true);

            // notify_pinned_messages
            migrationBuilder.CreateTable(
                name: "notify_pinned_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_pinned_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_pinned_messages_chat",
                        column: x => x.chat_id,
                        principalTable: "notify_chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notify_pinned_messages_message",
                        column: x => x.message_id,
                        principalTable: "notify_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_pinned_messages_chat_id",
                table: "notify_pinned_messages",
                column: "chat_id");

            // notify_channels
            migrationBuilder.CreateTable(
                name: "notify_channels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    icon_emoji = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_channels", x => x.id);
                });

            // notify_channel_subscribers
            migrationBuilder.CreateTable(
                name: "notify_channel_subscribers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subscribed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_channel_subscribers", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_channel_subscribers_channel",
                        column: x => x.channel_id,
                        principalTable: "notify_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_channel_subscribers_channel_id_user_id",
                table: "notify_channel_subscribers",
                columns: new[] { "channel_id", "user_id" },
                unique: true);

            // notify_channel_posts
            migrationBuilder.CreateTable(
                name: "notify_channel_posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    is_edited = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_channel_posts", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_channel_posts_channel",
                        column: x => x.channel_id,
                        principalTable: "notify_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_channel_posts_channel_id",
                table: "notify_channel_posts",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "ix_notify_channel_posts_created_at",
                table: "notify_channel_posts",
                column: "created_at");

            // notify_user_messaging_prefs
            migrationBuilder.CreateTable(
                name: "notify_user_messaging_prefs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "by_activity"),
                    pinned_chat_ids = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    hidden_chat_ids = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_user_messaging_prefs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_user_messaging_prefs_user_id",
                table: "notify_user_messaging_prefs",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "notify_user_messaging_prefs");
            migrationBuilder.DropTable(name: "notify_channel_posts");
            migrationBuilder.DropTable(name: "notify_channel_subscribers");
            migrationBuilder.DropTable(name: "notify_channels");
            migrationBuilder.DropTable(name: "notify_pinned_messages");
            migrationBuilder.DropTable(name: "notify_message_reactions");
            migrationBuilder.DropTable(name: "notify_message_reads");
            migrationBuilder.DropTable(name: "notify_messages");
            migrationBuilder.DropTable(name: "notify_chat_members");
            migrationBuilder.DropTable(name: "notify_chats");
        }
    }
}
