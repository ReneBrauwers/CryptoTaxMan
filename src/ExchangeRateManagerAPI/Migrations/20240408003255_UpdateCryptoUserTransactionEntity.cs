using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExchangeRateManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCryptoUserTransactionEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "Idx_CurrencyIn",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionDateAndCurrencyIn",
                table: "CryptoUserTransactions");

            migrationBuilder.DropColumn(
                name: "AmountIn",
                table: "CryptoUserTransactions");

            migrationBuilder.DropColumn(
                name: "CurrencyIn",
                table: "CryptoUserTransactions");

            migrationBuilder.DropColumn(
                name: "TransactionEvent",
                table: "CryptoUserTransactions");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "CryptoUserTransactions",
                newName: "ValueAssetType");

            migrationBuilder.RenameColumn(
                name: "FeeCurrency",
                table: "CryptoUserTransactions",
                newName: "InternalNotes");

            migrationBuilder.RenameColumn(
                name: "Fee",
                table: "CryptoUserTransactions",
                newName: "Value");

            migrationBuilder.RenameColumn(
                name: "ExchangeRate",
                table: "CryptoUserTransactions",
                newName: "ExchangeRateValue");

            migrationBuilder.RenameColumn(
                name: "ExchangeCurrency",
                table: "CryptoUserTransactions",
                newName: "ExchangeRateCurrency");

            migrationBuilder.RenameColumn(
                name: "CurrencyOut",
                table: "CryptoUserTransactions",
                newName: "AmountAssetType");

            migrationBuilder.RenameColumn(
                name: "AmountOut",
                table: "CryptoUserTransactions",
                newName: "Amount");

            migrationBuilder.RenameIndex(
                name: "Idx_CurrencyOut",
                table: "CryptoUserTransactions",
                newName: "Idx_AmountAssetType");

            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                table: "CryptoUserTransactions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsNFT",
                table: "CryptoUserTransactions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TaxableEvent",
                table: "CryptoUserTransactions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOn",
                table: "CryptoUserTransactions",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "UsesManualAssignedExchangeRate",
                table: "CryptoUserTransactions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "Idx_Amount",
                table: "CryptoUserTransactions",
                column: "Amount");

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDateAndAmountAssetType",
                table: "CryptoUserTransactions",
                columns: new[] { "TransactionDate", "AmountAssetType" });

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionType",
                table: "CryptoUserTransactions",
                column: "TransactionType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "Idx_Amount",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionDateAndAmountAssetType",
                table: "CryptoUserTransactions");

            migrationBuilder.DropIndex(
                name: "Idx_TransactionType",
                table: "CryptoUserTransactions");

            migrationBuilder.DropColumn(
                name: "Approved",
                table: "CryptoUserTransactions");

            migrationBuilder.DropColumn(
                name: "IsNFT",
                table: "CryptoUserTransactions");

            migrationBuilder.DropColumn(
                name: "TaxableEvent",
                table: "CryptoUserTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedOn",
                table: "CryptoUserTransactions");

            migrationBuilder.DropColumn(
                name: "UsesManualAssignedExchangeRate",
                table: "CryptoUserTransactions");

            migrationBuilder.RenameColumn(
                name: "ValueAssetType",
                table: "CryptoUserTransactions",
                newName: "Notes");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "CryptoUserTransactions",
                newName: "Fee");

            migrationBuilder.RenameColumn(
                name: "InternalNotes",
                table: "CryptoUserTransactions",
                newName: "FeeCurrency");

            migrationBuilder.RenameColumn(
                name: "ExchangeRateValue",
                table: "CryptoUserTransactions",
                newName: "ExchangeRate");

            migrationBuilder.RenameColumn(
                name: "ExchangeRateCurrency",
                table: "CryptoUserTransactions",
                newName: "ExchangeCurrency");

            migrationBuilder.RenameColumn(
                name: "AmountAssetType",
                table: "CryptoUserTransactions",
                newName: "CurrencyOut");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "CryptoUserTransactions",
                newName: "AmountOut");

            migrationBuilder.RenameIndex(
                name: "Idx_AmountAssetType",
                table: "CryptoUserTransactions",
                newName: "Idx_CurrencyOut");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountIn",
                table: "CryptoUserTransactions",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyIn",
                table: "CryptoUserTransactions",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "TransactionEvent",
                table: "CryptoUserTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "Idx_CurrencyIn",
                table: "CryptoUserTransactions",
                column: "CurrencyIn");

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDateAndCurrencyIn",
                table: "CryptoUserTransactions",
                columns: new[] { "TransactionDate", "CurrencyIn" });
        }
    }
}
