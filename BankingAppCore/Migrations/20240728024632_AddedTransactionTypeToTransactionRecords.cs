using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingAppCore.Migrations
{
    /// <inheritdoc />
    public partial class AddedTransactionTypeToTransactionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransactionType",
                table: "TransactionRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "TransactionRecords");
        }
    }
}
