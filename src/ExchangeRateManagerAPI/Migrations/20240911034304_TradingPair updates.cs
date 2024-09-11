using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExchangeRateManagerAPI.Migrations
{
    /// <inheritdoc />
    public partial class TradingPairupdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TradingPairInformation",
                table: "TradingPairInformation");

            migrationBuilder.UpdateData(
                table: "TradingPairInformation",
                keyColumn: "ExchangeName",
                keyValue: null,
                column: "ExchangeName",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "ExchangeName",
                table: "TradingPairInformation",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LastSyncStatus",
                table: "TradingPairInformation",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "SyncError",
                table: "TradingPairInformation",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SyncedOn",
                table: "TradingPairInformation",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TradingPairInformation",
                table: "TradingPairInformation",
                columns: new[] { "Symbol", "ExchangeCurrency", "ExchangeName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TradingPairInformation",
                table: "TradingPairInformation");

            migrationBuilder.DropColumn(
                name: "LastSyncStatus",
                table: "TradingPairInformation");

            migrationBuilder.DropColumn(
                name: "SyncError",
                table: "TradingPairInformation");

            migrationBuilder.DropColumn(
                name: "SyncedOn",
                table: "TradingPairInformation");

            migrationBuilder.AlterColumn<string>(
                name: "ExchangeName",
                table: "TradingPairInformation",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TradingPairInformation",
                table: "TradingPairInformation",
                columns: new[] { "Symbol", "ExchangeCurrency" });
        }
    }
}
