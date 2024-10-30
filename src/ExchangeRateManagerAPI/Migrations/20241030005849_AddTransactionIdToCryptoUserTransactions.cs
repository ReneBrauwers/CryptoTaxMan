using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExchangeRateManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionIdToCryptoUserTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CryptoUserTransactionsStaging",
                table: "CryptoUserTransactionsStaging");

            migrationBuilder.DropIndex(
                name: "Idx_IsProcessed_Staging",
                table: "CryptoUserTransactionsStaging");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionDate_Staging",
                table: "CryptoUserTransactionsStaging");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionDateAndCurrencyIn_Staging",
                table: "CryptoUserTransactionsStaging");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CryptoUserTransactions",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_Amount",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_AmountAssetType",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_Sequence",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionDate",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionDateAndAmountAssetType",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionType",
                table: "CryptoUserTransactions");

            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "CryptoUserTransactionsStaging",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "CryptoUserTransactions",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CryptoUserTransactionsStaging",
                table: "CryptoUserTransactionsStaging",
                columns: new[] { "TransactionId", "Sequence" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CryptoUserTransactions",
                table: "CryptoUserTransactions",
                columns: new[] { "TransactionId", "Sequence", "TransactionType" });

            migrationBuilder.CreateIndex(
                name: "Idx_IsProcessed_Staging",
                table: "CryptoUserTransactionsStaging",
                columns: new[] { "TransactionId", "IsProcessed" });

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDate_Staging",
                table: "CryptoUserTransactionsStaging",
                columns: new[] { "TransactionId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDateAndCurrencyIn_Staging",
                table: "CryptoUserTransactionsStaging",
                columns: new[] { "TransactionId", "TransactionDate", "CurrencyIn" });

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionId_Staging",
                table: "CryptoUserTransactionsStaging",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "Idx_Amount",
                table: "CryptoUserTransactions",
                columns: new[] { "TransactionId", "Amount" });

            migrationBuilder.CreateIndex(
                name: "Idx_AmountAssetType",
                table: "CryptoUserTransactions",
                columns: new[] { "TransactionId", "AmountAssetType" });

            migrationBuilder.CreateIndex(
                name: "Idx_Sequence",
                table: "CryptoUserTransactions",
                columns: new[] { "TransactionId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDate",
                table: "CryptoUserTransactions",
                columns: new[] { "TransactionId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDateAndAmountAssetType",
                table: "CryptoUserTransactions",
                columns: new[] { "TransactionId", "TransactionDate", "AmountAssetType" });

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionId",
                table: "CryptoUserTransactions",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionType",
                table: "CryptoUserTransactions",
                columns: new[] { "TransactionId", "TransactionType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CryptoUserTransactionsStaging",
                table: "CryptoUserTransactionsStaging");

            migrationBuilder.DropIndex(
                name: "Idx_IsProcessed_Staging",
                table: "CryptoUserTransactionsStaging");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionDate_Staging",
                table: "CryptoUserTransactionsStaging");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionDateAndCurrencyIn_Staging",
                table: "CryptoUserTransactionsStaging");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionId_Staging",
                table: "CryptoUserTransactionsStaging");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CryptoUserTransactions",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_Amount",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_AmountAssetType",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_Sequence",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionDate",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionDateAndAmountAssetType",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionId",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionType",
                table: "CryptoUserTransactions");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "CryptoUserTransactionsStaging");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "CryptoUserTransactions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CryptoUserTransactionsStaging",
                table: "CryptoUserTransactionsStaging",
                column: "Sequence");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CryptoUserTransactions",
                table: "CryptoUserTransactions",
                columns: new[] { "Sequence", "TransactionType" });

            migrationBuilder.CreateIndex(
                name: "Idx_IsProcessed_Staging",
                table: "CryptoUserTransactionsStaging",
                column: "IsProcessed");

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDate_Staging",
                table: "CryptoUserTransactionsStaging",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDateAndCurrencyIn_Staging",
                table: "CryptoUserTransactionsStaging",
                columns: new[] { "TransactionDate", "CurrencyIn" });

            migrationBuilder.CreateIndex(
                name: "Idx_Amount",
                table: "CryptoUserTransactions",
                column: "Amount");

            migrationBuilder.CreateIndex(
                name: "Idx_AmountAssetType",
                table: "CryptoUserTransactions",
                column: "AmountAssetType");

            migrationBuilder.CreateIndex(
                name: "Idx_Sequence",
                table: "CryptoUserTransactions",
                column: "Sequence");

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDate",
                table: "CryptoUserTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDateAndAmountAssetType",
                table: "CryptoUserTransactions",
                columns: new[] { "TransactionDate", "AmountAssetType" });

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionType",
                table: "CryptoUserTransactions",
                column: "TransactionType");
        }
    }
}
