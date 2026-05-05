using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CoreBPM.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_notify_sms_log",
                table: "notify_sms_log");

            migrationBuilder.DropPrimaryKey(
                name: "pk_notify_push_subscriptions",
                table: "notify_push_subscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_notify_inbox",
                table: "notify_inbox");

            migrationBuilder.DropPrimaryKey(
                name: "pk_notify_action_tokens",
                table: "notify_action_tokens");

            migrationBuilder.DropPrimaryKey(
                name: "pk_admin_vapid_settings",
                table: "admin_vapid_settings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_admin_smtp_settings",
                table: "admin_smtp_settings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_admin_sms_settings",
                table: "admin_sms_settings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_admin_email_templates",
                table: "admin_email_templates");

            migrationBuilder.RenameColumn(
                name: "p256_dh",
                table: "notify_push_subscriptions",
                newName: "p256dh");

            migrationBuilder.RenameIndex(
                name: "ix_notify_post_reactions_post_user_emoji",
                table: "notify_post_reactions",
                newName: "ix_notify_post_reactions_post_id_user_id_emoji");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "notify_inbox",
                newName: "type");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "notify_inbox",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "Link",
                table: "notify_inbox",
                newName: "link");

            migrationBuilder.RenameColumn(
                name: "Body",
                table: "notify_inbox",
                newName: "body");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "notify_inbox",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "notify_inbox",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "ReadAt",
                table: "notify_inbox",
                newName: "read_at");

            migrationBuilder.RenameColumn(
                name: "PayloadJson",
                table: "notify_inbox",
                newName: "payload_json");

            migrationBuilder.RenameColumn(
                name: "IsRead",
                table: "notify_inbox",
                newName: "is_read");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "notify_inbox",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_notify_inbox_UserId_IsRead",
                table: "notify_inbox",
                newName: "ix_notify_inbox_user_id_is_read");

            migrationBuilder.RenameIndex(
                name: "IX_notify_inbox_CreatedAt",
                table: "notify_inbox",
                newName: "ix_notify_inbox_created_at");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "admin_smtp_settings",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "Port",
                table: "admin_smtp_settings",
                newName: "port");

            migrationBuilder.RenameColumn(
                name: "Password",
                table: "admin_smtp_settings",
                newName: "password");

            migrationBuilder.RenameColumn(
                name: "Host",
                table: "admin_smtp_settings",
                newName: "host");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "admin_smtp_settings",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UseSsl",
                table: "admin_smtp_settings",
                newName: "use_ssl");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "admin_smtp_settings",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "FromName",
                table: "admin_smtp_settings",
                newName: "from_name");

            migrationBuilder.RenameColumn(
                name: "FromAddress",
                table: "admin_smtp_settings",
                newName: "from_address");

            migrationBuilder.AlterColumn<bool>(
                name: "sms",
                table: "user_task_notification_settings",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "push",
                table: "user_task_notification_settings",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);


            migrationBuilder.AlterColumn<int>(
                name: "start_hour",
                table: "notify_dnd_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 22);

            migrationBuilder.AlterColumn<bool>(
                name: "is_enabled",
                table: "notify_dnd_settings",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "end_hour",
                table: "notify_dnd_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 8);

            migrationBuilder.AlterColumn<bool>(
                name: "apply_to_sms",
                table: "notify_dnd_settings",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "apply_to_push",
                table: "notify_dnd_settings",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "admin_vapid_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "admin_smtp_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "admin_sms_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<bool>(
                name: "is_mandatory_sms",
                table: "admin_notification_templates",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "is_mandatory_push",
                table: "admin_notification_templates",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "is_mandatory_in_app",
                table: "admin_notification_templates",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "is_mandatory_email",
                table: "admin_notification_templates",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "is_active",
                table: "admin_notification_templates",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_notify_sms_log",
                table: "notify_sms_log",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_notify_push_subscriptions",
                table: "notify_push_subscriptions",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_notify_inbox",
                table: "notify_inbox",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_notify_action_tokens",
                table: "notify_action_tokens",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_admin_vapid_settings",
                table: "admin_vapid_settings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_admin_smtp_settings",
                table: "admin_smtp_settings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_admin_sms_settings",
                table: "admin_sms_settings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_admin_email_templates",
                table: "admin_email_templates",
                column: "id");

            migrationBuilder.CreateTable(
                name: "admin_notification_log_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    retention_days = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_notification_log_settings", x => x.id);
                });

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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_channels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notify_chats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_chats", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notify_throttle_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_throttle_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notify_throttle_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    min_interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_throttle_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notify_user_messaging_prefs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    pinned_chat_ids = table.Column<string>(type: "text", nullable: false),
                    hidden_chat_ids = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_user_messaging_prefs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_company_info",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_company_info", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_company_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_company_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_company_news",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_company_news", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_user_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "ru"),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    theme = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "system"),
                    date_format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    page_size = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_user_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_org_user_preferences_org_users_user_id",
                        column: x => x.user_id,
                        principalTable: "org_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "portal_branding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    logo_url = table.Column<string>(type: "text", nullable: true),
                    favicon_url = table.Column<string>(type: "text", nullable: true),
                    primary_color = table.Column<string>(type: "text", nullable: true),
                    accent_color = table.Column<string>(type: "text", nullable: true),
                    global_theme = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portal_branding", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portal_dashboard_widgets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    widget_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    col = table.Column<int>(type: "integer", nullable: false),
                    row = table.Column<int>(type: "integer", nullable: false),
                    col_span = table.Column<int>(type: "integer", nullable: false),
                    row_span = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    config_json = table.Column<string>(type: "text", nullable: true),
                    is_collapsed = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portal_dashboard_widgets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portal_menu_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    icon = table.Column<string>(type: "text", nullable: true),
                    section_id = table.Column<string>(type: "text", nullable: true),
                    external_url = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    required_role = table.Column<string>(type: "text", nullable: true),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portal_menu_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notify_channel_posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    is_edited = table.Column<bool>(type: "boolean", nullable: false),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_channel_posts", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_channel_posts_notify_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "notify_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notify_channel_subscribers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false),
                    subscribed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_channel_subscribers", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_channel_subscribers_notify_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "notify_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notify_chat_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false),
                    is_muted = table.Column<bool>(type: "boolean", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_chat_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_chat_members_notify_chats_chat_id",
                        column: x => x.chat_id,
                        principalTable: "notify_chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notify_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    is_edited = table.Column<bool>(type: "boolean", nullable: false),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    reply_to_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_messages_notify_chats_chat_id",
                        column: x => x.chat_id,
                        principalTable: "notify_chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notify_messages_notify_messages_reply_to_message_id",
                        column: x => x.reply_to_message_id,
                        principalTable: "notify_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notify_channel_pinned_posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_channel_pinned_posts", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_channel_pinned_posts_notify_channel_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "notify_channel_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notify_channel_pinned_posts_notify_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "notify_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notify_message_reactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emoji = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_message_reactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_message_reactions_notify_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "notify_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notify_message_reads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_message_reads", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_message_reads_notify_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "notify_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notify_pinned_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notify_pinned_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_notify_pinned_messages_notify_chats_chat_id",
                        column: x => x.chat_id,
                        principalTable: "notify_chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notify_pinned_messages_notify_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "notify_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notify_channel_pinned_posts_channel_id",
                table: "notify_channel_pinned_posts",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "ix_notify_channel_pinned_posts_channel_id_post_id",
                table: "notify_channel_pinned_posts",
                columns: new[] { "channel_id", "post_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notify_channel_pinned_posts_post_id",
                table: "notify_channel_pinned_posts",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "ix_notify_channel_posts_channel_id",
                table: "notify_channel_posts",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "ix_notify_channel_posts_created_at",
                table: "notify_channel_posts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_notify_channel_subscribers_channel_id_user_id",
                table: "notify_channel_subscribers",
                columns: new[] { "channel_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notify_chat_members_chat_id_user_id",
                table: "notify_chat_members",
                columns: new[] { "chat_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notify_chats_last_message_at",
                table: "notify_chats",
                column: "last_message_at");

            migrationBuilder.CreateIndex(
                name: "ix_notify_message_reactions_message_id_user_id_emoji",
                table: "notify_message_reactions",
                columns: new[] { "message_id", "user_id", "emoji" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notify_message_reads_message_id_user_id",
                table: "notify_message_reads",
                columns: new[] { "message_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notify_messages_chat_id",
                table: "notify_messages",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "ix_notify_messages_created_at",
                table: "notify_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_notify_messages_reply_to_message_id",
                table: "notify_messages",
                column: "reply_to_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_notify_pinned_messages_chat_id",
                table: "notify_pinned_messages",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "ix_notify_pinned_messages_message_id",
                table: "notify_pinned_messages",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_notify_throttle_log_user_id_event_type_channel",
                table: "notify_throttle_log",
                columns: new[] { "user_id", "event_type", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notify_throttle_settings_user_id_event_type_channel",
                table: "notify_throttle_settings",
                columns: new[] { "user_id", "event_type", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notify_user_messaging_prefs_user_id",
                table: "notify_user_messaging_prefs",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_company_links_sort_order",
                table: "org_company_links",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "ix_org_company_news_created_at",
                table: "org_company_news",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_org_user_preferences_user_id",
                table: "org_user_preferences",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_portal_dashboard_widgets_user_id",
                table: "portal_dashboard_widgets",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_portal_menu_items_sort_order",
                table: "portal_menu_items",
                column: "sort_order");

            // Удаляем старые FK с короткими именами, созданными вручную в AddChannelPostReactionsAndComments,
            // перед тем как добавить FK с полными EF-именами.
            migrationBuilder.DropForeignKey(
                name: "fk_notify_post_comments_post",
                table: "notify_post_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_notify_post_reactions_post",
                table: "notify_post_reactions");

            migrationBuilder.AddForeignKey(
                name: "fk_notify_post_comments_notify_channel_posts_post_id",
                table: "notify_post_comments",
                column: "post_id",
                principalTable: "notify_channel_posts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_notify_post_reactions_notify_channel_posts_post_id",
                table: "notify_post_reactions",
                column: "post_id",
                principalTable: "notify_channel_posts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notify_post_comments_notify_channel_posts_post_id",
                table: "notify_post_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_notify_post_reactions_notify_channel_posts_post_id",
                table: "notify_post_reactions");

            migrationBuilder.DropTable(
                name: "admin_notification_log_settings");

            migrationBuilder.DropTable(
                name: "notify_channel_pinned_posts");

            migrationBuilder.DropTable(
                name: "notify_channel_subscribers");

            migrationBuilder.DropTable(
                name: "notify_chat_members");

            migrationBuilder.DropTable(
                name: "notify_message_reactions");

            migrationBuilder.DropTable(
                name: "notify_message_reads");

            migrationBuilder.DropTable(
                name: "notify_pinned_messages");

            migrationBuilder.DropTable(
                name: "notify_throttle_log");

            migrationBuilder.DropTable(
                name: "notify_throttle_settings");

            migrationBuilder.DropTable(
                name: "notify_user_messaging_prefs");

            migrationBuilder.DropTable(
                name: "org_company_info");

            migrationBuilder.DropTable(
                name: "org_company_links");

            migrationBuilder.DropTable(
                name: "org_company_news");

            migrationBuilder.DropTable(
                name: "org_user_preferences");

            migrationBuilder.DropTable(
                name: "portal_branding");

            migrationBuilder.DropTable(
                name: "portal_dashboard_widgets");

            migrationBuilder.DropTable(
                name: "portal_menu_items");

            migrationBuilder.DropTable(
                name: "notify_channel_posts");

            migrationBuilder.DropTable(
                name: "notify_messages");

            migrationBuilder.DropTable(
                name: "notify_channels");

            migrationBuilder.DropTable(
                name: "notify_chats");

            migrationBuilder.DropPrimaryKey(
                name: "pk_notify_sms_log",
                table: "notify_sms_log");

            migrationBuilder.DropPrimaryKey(
                name: "pk_notify_push_subscriptions",
                table: "notify_push_subscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "pk_notify_inbox",
                table: "notify_inbox");

            migrationBuilder.DropPrimaryKey(
                name: "pk_notify_action_tokens",
                table: "notify_action_tokens");

            migrationBuilder.DropPrimaryKey(
                name: "pk_admin_vapid_settings",
                table: "admin_vapid_settings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_admin_smtp_settings",
                table: "admin_smtp_settings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_admin_sms_settings",
                table: "admin_sms_settings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_admin_email_templates",
                table: "admin_email_templates");


            migrationBuilder.RenameColumn(
                name: "p256dh",
                table: "notify_push_subscriptions",
                newName: "p256_dh");

            migrationBuilder.RenameIndex(
                name: "ix_notify_post_reactions_post_id_user_id_emoji",
                table: "notify_post_reactions",
                newName: "ix_notify_post_reactions_post_user_emoji");

            migrationBuilder.RenameColumn(
                name: "type",
                table: "notify_inbox",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "notify_inbox",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "link",
                table: "notify_inbox",
                newName: "Link");

            migrationBuilder.RenameColumn(
                name: "body",
                table: "notify_inbox",
                newName: "Body");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "notify_inbox",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "notify_inbox",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "read_at",
                table: "notify_inbox",
                newName: "ReadAt");

            migrationBuilder.RenameColumn(
                name: "payload_json",
                table: "notify_inbox",
                newName: "PayloadJson");

            migrationBuilder.RenameColumn(
                name: "is_read",
                table: "notify_inbox",
                newName: "IsRead");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "notify_inbox",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_notify_inbox_user_id_is_read",
                table: "notify_inbox",
                newName: "IX_notify_inbox_UserId_IsRead");

            migrationBuilder.RenameIndex(
                name: "ix_notify_inbox_created_at",
                table: "notify_inbox",
                newName: "IX_notify_inbox_CreatedAt");

            migrationBuilder.AlterColumn<bool>(
                name: "sms",
                table: "user_task_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "push",
                table: "user_task_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<int>(
                name: "start_hour",
                table: "notify_dnd_settings",
                type: "integer",
                nullable: false,
                defaultValue: 22,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<bool>(
                name: "is_enabled",
                table: "notify_dnd_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<int>(
                name: "end_hour",
                table: "notify_dnd_settings",
                type: "integer",
                nullable: false,
                defaultValue: 8,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<bool>(
                name: "apply_to_sms",
                table: "notify_dnd_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "apply_to_push",
                table: "notify_dnd_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "admin_vapid_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "admin_smtp_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "admin_sms_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<bool>(
                name: "is_mandatory_sms",
                table: "admin_notification_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_mandatory_push",
                table: "admin_notification_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_mandatory_in_app",
                table: "admin_notification_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_mandatory_email",
                table: "admin_notification_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_active",
                table: "admin_notification_templates",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddPrimaryKey(
                name: "pk_notify_sms_log",
                table: "notify_sms_log",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_notify_push_subscriptions",
                table: "notify_push_subscriptions",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_notify_inbox",
                table: "notify_inbox",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_notify_action_tokens",
                table: "notify_action_tokens",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_admin_vapid_settings",
                table: "admin_vapid_settings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_admin_smtp_settings",
                table: "admin_smtp_settings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_admin_sms_settings",
                table: "admin_sms_settings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_admin_email_templates",
                table: "admin_email_templates",
                column: "id");
        }
    }
}
