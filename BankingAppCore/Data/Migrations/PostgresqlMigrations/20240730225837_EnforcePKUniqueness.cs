using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlushBanking.Data.Migrations.PostgresqlMigrations
{
    /// <inheritdoc />
    public partial class EnforcePKUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_UserID",
                table: "Users",
                column: "UserID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRecords_TransactionID",
                table: "TransactionRecords",
                column: "TransactionID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_CardID",
                table: "Cards",
                column: "CardID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_UserID",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TransactionRecords_TransactionID",
                table: "TransactionRecords");

            migrationBuilder.DropIndex(
                name: "IX_Cards_CardID",
                table: "Cards");
        }
    }
}
