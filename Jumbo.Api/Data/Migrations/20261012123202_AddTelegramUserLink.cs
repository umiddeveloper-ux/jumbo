using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramUserLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_alert_at",
                table: "telegram_connections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "telegram_connections",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_telegram_connections_user_id",
                table: "telegram_connections",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_telegram_connections_user_id",
                table: "telegram_connections");

            migrationBuilder.DropColumn(
                name: "last_alert_at",
                table: "telegram_connections");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "telegram_connections");
        }
    }
}
