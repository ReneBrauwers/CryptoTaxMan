using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExchangeRateManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class addpercentagedifferencecolumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LowHighPercentageDifference",
                table: "ExchangeRates",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OpenClosePercentageDifference",
                table: "ExchangeRates",
                type: "decimal(65,30)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LowHighPercentageDifference",
                table: "ExchangeRates");

            migrationBuilder.DropColumn(
                name: "OpenClosePercentageDifference",
                table: "ExchangeRates");
        }
    }
}
