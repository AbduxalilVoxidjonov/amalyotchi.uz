using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amaliyotchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentMessageInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_broadcast_deliveries_recipient_user_id",
                table: "broadcast_deliveries");

            migrationBuilder.AlterColumn<long>(
                name: "chat_id",
                table: "broadcast_deliveries",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "read_at",
                table: "broadcast_deliveries",
                type: "timestamp with time zone",
                nullable: true);

            // Ilova qutisi paydo bo'lishidan oldingi xabarlar talabalarga Telegram orqali allaqachon yetkazilgan —
            // qutida "o'qilmagan" bo'lib to'planib qolmasin: o'qilgan deb belgilanadi (yuborilgan vaqt, bo'lmasa hozir).
            migrationBuilder.Sql("UPDATE broadcast_deliveries SET read_at = COALESCE(sent_at, now()) WHERE read_at IS NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_broadcast_deliveries_recipient_user_id_read_at",
                table: "broadcast_deliveries",
                columns: new[] { "recipient_user_id", "read_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_broadcast_deliveries_recipient_user_id_read_at",
                table: "broadcast_deliveries");

            migrationBuilder.DropColumn(
                name: "read_at",
                table: "broadcast_deliveries");

            // Telegram'siz (app-only) yetkazishlarni eski sxemada ifodalab bo'lmaydi — ular o'chiriladi.
            migrationBuilder.Sql("DELETE FROM broadcast_deliveries WHERE chat_id IS NULL;");

            migrationBuilder.AlterColumn<long>(
                name: "chat_id",
                table: "broadcast_deliveries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_broadcast_deliveries_recipient_user_id",
                table: "broadcast_deliveries",
                column: "recipient_user_id");
        }
    }
}
