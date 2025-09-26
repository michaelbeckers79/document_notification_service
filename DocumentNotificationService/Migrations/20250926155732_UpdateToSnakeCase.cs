using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentNotificationService.Migrations
{
    /// <inheritdoc />
    public partial class UpdateToSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ProcessedDocuments",
                table: "ProcessedDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LastQueryTimestamps",
                table: "LastQueryTimestamps");

            migrationBuilder.RenameTable(
                name: "ProcessedDocuments",
                newName: "processed_documents");

            migrationBuilder.RenameTable(
                name: "LastQueryTimestamps",
                newName: "last_query_timestamps");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "processed_documents",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "processed_documents",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ProcessedAt",
                table: "processed_documents",
                newName: "processed_at");

            migrationBuilder.RenameColumn(
                name: "PortfolioId",
                table: "processed_documents",
                newName: "portfolio_id");

            migrationBuilder.RenameColumn(
                name: "MessageSent",
                table: "processed_documents",
                newName: "message_sent");

            migrationBuilder.RenameColumn(
                name: "ErrorMessage",
                table: "processed_documents",
                newName: "error_message");

            migrationBuilder.RenameColumn(
                name: "DocumentId",
                table: "processed_documents",
                newName: "document_id");

            migrationBuilder.RenameColumn(
                name: "DocumentDate",
                table: "processed_documents",
                newName: "document_date");

            migrationBuilder.RenameIndex(
                name: "IX_ProcessedDocuments_PortfolioId",
                table: "processed_documents",
                newName: "IX_processed_documents_portfolio_id");

            migrationBuilder.RenameIndex(
                name: "IX_ProcessedDocuments_DocumentId",
                table: "processed_documents",
                newName: "IX_processed_documents_document_id");

            migrationBuilder.RenameIndex(
                name: "IX_ProcessedDocuments_DocumentDate",
                table: "processed_documents",
                newName: "IX_processed_documents_document_date");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "last_query_timestamps",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "last_query_timestamps",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "LastSuccessfulQuery",
                table: "last_query_timestamps",
                newName: "last_successful_query");

            migrationBuilder.AddPrimaryKey(
                name: "pk_processed_documents",
                table: "processed_documents",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_last_query_timestamps",
                table: "last_query_timestamps",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_processed_documents",
                table: "processed_documents");

            migrationBuilder.DropPrimaryKey(
                name: "pk_last_query_timestamps",
                table: "last_query_timestamps");

            migrationBuilder.RenameTable(
                name: "processed_documents",
                newName: "ProcessedDocuments");

            migrationBuilder.RenameTable(
                name: "last_query_timestamps",
                newName: "LastQueryTimestamps");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "ProcessedDocuments",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ProcessedDocuments",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "processed_at",
                table: "ProcessedDocuments",
                newName: "ProcessedAt");

            migrationBuilder.RenameColumn(
                name: "portfolio_id",
                table: "ProcessedDocuments",
                newName: "PortfolioId");

            migrationBuilder.RenameColumn(
                name: "message_sent",
                table: "ProcessedDocuments",
                newName: "MessageSent");

            migrationBuilder.RenameColumn(
                name: "error_message",
                table: "ProcessedDocuments",
                newName: "ErrorMessage");

            migrationBuilder.RenameColumn(
                name: "document_id",
                table: "ProcessedDocuments",
                newName: "DocumentId");

            migrationBuilder.RenameColumn(
                name: "document_date",
                table: "ProcessedDocuments",
                newName: "DocumentDate");

            migrationBuilder.RenameIndex(
                name: "IX_processed_documents_portfolio_id",
                table: "ProcessedDocuments",
                newName: "IX_ProcessedDocuments_PortfolioId");

            migrationBuilder.RenameIndex(
                name: "IX_processed_documents_document_id",
                table: "ProcessedDocuments",
                newName: "IX_ProcessedDocuments_DocumentId");

            migrationBuilder.RenameIndex(
                name: "IX_processed_documents_document_date",
                table: "ProcessedDocuments",
                newName: "IX_ProcessedDocuments_DocumentDate");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "LastQueryTimestamps",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "LastQueryTimestamps",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "last_successful_query",
                table: "LastQueryTimestamps",
                newName: "LastSuccessfulQuery");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProcessedDocuments",
                table: "ProcessedDocuments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LastQueryTimestamps",
                table: "LastQueryTimestamps",
                column: "Id");
        }
    }
}
