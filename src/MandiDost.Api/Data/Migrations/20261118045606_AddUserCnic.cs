using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MandiDost.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCnic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cnic",
                table: "Users",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Cnic",
                table: "Users",
                column: "Cnic",
                unique: true,
                filter: "[Cnic] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Cnic",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Cnic",
                table: "Users");
        }
    }
}
