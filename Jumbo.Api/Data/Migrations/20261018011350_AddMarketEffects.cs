using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketEffects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avatar_default_svg",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "avatar_item_key",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "badge_key",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "frame_key",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "freeze_count",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "pet_key",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "shield_count",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "theme_key",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "key",
                table: "markets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "markets",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "avatar_default_svg",
                table: "users");

            migrationBuilder.DropColumn(
                name: "avatar_item_key",
                table: "users");

            migrationBuilder.DropColumn(
                name: "badge_key",
                table: "users");

            migrationBuilder.DropColumn(
                name: "frame_key",
                table: "users");

            migrationBuilder.DropColumn(
                name: "freeze_count",
                table: "users");

            migrationBuilder.DropColumn(
                name: "pet_key",
                table: "users");

            migrationBuilder.DropColumn(
                name: "shield_count",
                table: "users");

            migrationBuilder.DropColumn(
                name: "theme_key",
                table: "users");

            migrationBuilder.DropColumn(
                name: "key",
                table: "markets");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "markets");
        }
    }
}
