using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ads",
                columns: table => new
                {
                    ad_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    html = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    image_data = table.Column<byte[]>(type: "bytea", nullable: true),
                    image_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    lang = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ads", x => x.ad_id);
                });

            migrationBuilder.CreateTable(
                name: "ad_views",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    ad_id = table.Column<int>(type: "integer", nullable: false),
                    last_shown_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ad_views", x => new { x.user_id, x.ad_id });
                    table.ForeignKey(
                        name: "fk_ad_views_ads_ad_id",
                        column: x => x.ad_id,
                        principalTable: "ads",
                        principalColumn: "ad_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ad_views_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ad_views_ad_id",
                table: "ad_views",
                column: "ad_id");

            migrationBuilder.CreateIndex(
                name: "ix_ads_is_active_starts_at",
                table: "ads",
                columns: new[] { "is_active", "starts_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ad_views");

            migrationBuilder.DropTable(
                name: "ads");
        }
    }
}
