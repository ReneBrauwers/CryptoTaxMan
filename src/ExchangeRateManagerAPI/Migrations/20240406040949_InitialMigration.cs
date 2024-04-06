using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExchangeRateManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CryptoUserTransactions",
                columns: table => new
                {
                    Sequence = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TransactionDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    AmountIn = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    CurrencyIn = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AmountOut = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    CurrencyOut = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TransactionEvent = table.Column<int>(type: "int", nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    FeeCurrency = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExchangeRate = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    ExchangeCurrency = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoUserTransactions", x => x.Sequence);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CryptoUserTransactionsStaging",
                columns: table => new
                {
                    Sequence = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TransactionDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    AmountIn = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    CurrencyIn = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AmountOut = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    CurrencyOut = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TransactionEvent = table.Column<int>(type: "int", nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    FeeCurrency = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExchangeRate = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    ExchangeCurrency = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsProcessed = table.Column<bool>(type: "tinyint(1)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoUserTransactionsStaging", x => x.Sequence);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExchangeCurrency = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Open = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Close = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Low = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    High = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OpenCloseAverage = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LowHighAverage = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DataSource = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LookupOptional = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => new { x.Date, x.Symbol, x.ExchangeCurrency });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "Idx_CurrencyIn",
                table: "CryptoUserTransactions",
                column: "CurrencyIn");

            migrationBuilder.CreateIndex(
                name: "Idx_CurrencyOut",
                table: "CryptoUserTransactions",
                column: "CurrencyOut");

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDate",
                table: "CryptoUserTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "Idx_TransactionDateAndCurrencyIn",
                table: "CryptoUserTransactions",
                columns: new[] { "TransactionDate", "CurrencyIn" });

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
                name: "Idx_Date",
                table: "ExchangeRates",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "Idx_ExchangeCurrency",
                table: "ExchangeRates",
                column: "ExchangeCurrency");

            migrationBuilder.CreateIndex(
                name: "Idx_Symbol",
                table: "ExchangeRates",
                column: "Symbol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CryptoUserTransactions");

            migrationBuilder.DropTable(
                name: "CryptoUserTransactionsStaging");

            migrationBuilder.DropTable(
                name: "ExchangeRates");
        }
    }
}
