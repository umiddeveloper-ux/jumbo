using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telegram_connections",
                columns: table => new
                {
                    telegram_connection_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    chat_id = table.Column<long>(type: "bigint", nullable: true),
                    telegram_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lang = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    connected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_digest_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telegram_connections", x => x.telegram_connection_id);
                    table.ForeignKey(
                        name: "fk_telegram_connections_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_telegram_connections_chat_id",
                table: "telegram_connections",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "ix_telegram_connections_code",
                table: "telegram_connections",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_telegram_connections_school_id",
                table: "telegram_connections",
                column: "school_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telegram_connections");
        }
    }
}
