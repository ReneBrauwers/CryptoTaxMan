using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExchangeRateManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class ModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReportableAsIncome",
                table: "CryptoUserTransactionsStaging",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReportableAsIncome",
                table: "CryptoUserTransactions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReportableAsIncome",
                table: "CryptoUserTransactionsStaging");

            migrationBuilder.DropColumn(
                name: "ReportableAsIncome",
                table: "CryptoUserTransactions");
        }
    }
}
