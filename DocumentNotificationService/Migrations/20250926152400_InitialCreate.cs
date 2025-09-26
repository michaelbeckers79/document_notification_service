using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentNotificationService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LastQueryTimestamps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LastSuccessfulQuery = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LastQueryTimestamps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PortfolioId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MessageSent = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedDocuments", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "LastQueryTimestamps",
                columns: new[] { "Id", "LastSuccessfulQuery", "UpdatedAt" },
                values: new object[] { 1, new DateTime(2025, 9, 25, 15, 23, 59, 656, DateTimeKind.Utc).AddTicks(2855), new DateTime(2025, 9, 26, 15, 23, 59, 656, DateTimeKind.Utc).AddTicks(3179) });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedDocuments_DocumentDate",
                table: "ProcessedDocuments",
                column: "DocumentDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedDocuments_DocumentId",
                table: "ProcessedDocuments",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedDocuments_PortfolioId",
                table: "ProcessedDocuments",
                column: "PortfolioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LastQueryTimestamps");

            migrationBuilder.DropTable(
                name: "ProcessedDocuments");
        }
    }
}
