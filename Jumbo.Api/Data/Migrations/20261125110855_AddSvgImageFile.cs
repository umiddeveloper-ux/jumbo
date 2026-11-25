using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jumbo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSvgImageFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_file",
                table: "svg_images",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_file",
                table: "svg_images");
        }
    }
}
