using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UsersFiveLives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The maximum is 5 lives now: those who had the old full 3 get the new full 5 (fewer than 3 refill to 5 as before).
            migrationBuilder.Sql("UPDATE users SET life_count = 5 WHERE life_count = 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
