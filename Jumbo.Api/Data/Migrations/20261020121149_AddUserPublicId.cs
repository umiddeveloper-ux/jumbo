using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "public_id",
                table: "users",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                computedColumnSql: "chr(65 + user_id / 10000000) || lpad((user_id % 10000000)::text, 7, '0')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_public_id",
                table: "users",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_public_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "users");
        }
    }
}
