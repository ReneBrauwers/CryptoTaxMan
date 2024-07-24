using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExchangeRateManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class addflagforreviewrequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReviewRequired",
                table: "CryptoUserTransactions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewRequired",
                table: "CryptoUserTransactions");
        }
    }
}
