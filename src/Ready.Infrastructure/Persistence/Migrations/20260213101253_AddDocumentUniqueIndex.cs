using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ready.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_class c
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE c.relname = 'IX_documents_CustomerId_Sha256'
                    ) THEN
                        CREATE UNIQUE INDEX ""IX_documents_CustomerId_Sha256"" ON documents (""CustomerId"", ""Sha256"");
                    END IF;
                END
                $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_documents_CustomerId_Sha256"";");
        }
    }
}
