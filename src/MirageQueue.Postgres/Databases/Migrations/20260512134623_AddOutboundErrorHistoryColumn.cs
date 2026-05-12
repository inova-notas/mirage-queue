using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MirageQueue.Postgres.Databases.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundErrorHistoryColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorHistory",
                schema: "mirage_queue",
                table: "OutboundMessage",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorHistory",
                schema: "mirage_queue",
                table: "OutboundMessage");
        }
    }
}
