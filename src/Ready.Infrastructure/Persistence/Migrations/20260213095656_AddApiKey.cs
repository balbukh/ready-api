using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ready.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParamsJson",
                table: "jobs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_CustomerId_Sha256",
                table: "documents",
                columns: new[] { "CustomerId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_Key",
                table: "api_keys",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropIndex(
                name: "IX_documents_CustomerId_Sha256",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "ParamsJson",
                table: "jobs");
        }
    }
}
