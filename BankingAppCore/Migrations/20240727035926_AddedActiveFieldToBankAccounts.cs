using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingAppCore.Migrations
{
    /// <inheritdoc />
    public partial class AddedActiveFieldToBankAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "BankAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Active",
                table: "BankAccounts");
        }
    }
}
