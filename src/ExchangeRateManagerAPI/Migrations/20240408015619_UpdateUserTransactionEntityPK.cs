using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExchangeRateManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserTransactionEntityPK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CryptoUserTransactions",
                table: "CryptoUserTransactions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CryptoUserTransactions",
                table: "CryptoUserTransactions",
                columns: new[] { "Sequence", "TransactionType" });

            migrationBuilder.CreateIndex(
                name: "Idx_Sequence",
                table: "CryptoUserTransactions",
                column: "Sequence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CryptoUserTransactions",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_Sequence",
                table: "CryptoUserTransactions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CryptoUserTransactions",
                table: "CryptoUserTransactions",
                column: "Sequence");
        }
    }
}
