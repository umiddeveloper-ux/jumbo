using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdDailyTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "daily_at_minutes",
                table: "ads",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "daily_at_minutes",
                table: "ads");
        }
    }
}
