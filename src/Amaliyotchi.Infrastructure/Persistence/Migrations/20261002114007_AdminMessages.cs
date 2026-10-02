using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "telegram_bot_blocked_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "telegram_linked_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // Mavjud bog'langan talabalar: bog'lash vaqti audit jurnalidan (AuditAction.TelegramLinked = 64) tiklanadi;
            // yozuvi yo'qlarida (masalan seed yoki audit'dan oldingi bog'lash) null qoladi — kontraktda ruxsat etilgan.
            migrationBuilder.Sql("""
                UPDATE users u
                SET telegram_linked_at = a.linked_at
                FROM (
                    SELECT entity_id, max(occurred_at) AS linked_at
                    FROM audit_logs
                    WHERE action = 64 AND entity_name = 'User' AND entity_id IS NOT NULL
                    GROUP BY entity_id
                ) a
                WHERE u.telegram_user_id IS NOT NULL
                  AND u.telegram_linked_at IS NULL
                  AND a.entity_id = u.id::text;
                """);

            migrationBuilder.CreateTable(
                name: "broadcast_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    attach_app_button = table.Column<bool>(type: "boolean", nullable: false),
                    audience_kind = table.Column<int>(type: "integer", nullable: false),
                    audience_label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    audience_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_broadcast_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "broadcast_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    telegram_message_id = table.Column<long>(type: "bigint", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_broadcast_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_broadcast_deliveries_broadcast_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "broadcast_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_broadcast_deliveries_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_broadcast_deliveries_message_id_recipient_user_id",
                table: "broadcast_deliveries",
                columns: new[] { "message_id", "recipient_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_broadcast_deliveries_message_id_status",
                table: "broadcast_deliveries",
                columns: new[] { "message_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_broadcast_deliveries_recipient_user_id",
                table: "broadcast_deliveries",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_broadcast_deliveries_status_next_attempt_at",
                table: "broadcast_deliveries",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_broadcast_messages_created_at",
                table: "broadcast_messages",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "broadcast_deliveries");

            migrationBuilder.DropTable(
                name: "broadcast_messages");

            migrationBuilder.DropColumn(
                name: "telegram_bot_blocked_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "telegram_linked_at",
                table: "users");
        }
    }
}
