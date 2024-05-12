using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MirageQueue.Postgres.Databases.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mirage_queue");

            migrationBuilder.CreateTable(
                name: "InboundMessage",
                schema: "mirage_queue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    MessageContract = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledInboundMessage",
                schema: "mirage_queue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExecuteAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    MessageContract = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledInboundMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundMessage",
                schema: "mirage_queue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConsumerEndpoint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    InboundMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    MessageContract = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboundMessage_InboundMessage_InboundMessageId",
                        column: x => x.InboundMessageId,
                        principalSchema: "mirage_queue",
                        principalTable: "InboundMessage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_InboundMessageId",
                schema: "mirage_queue",
                table: "OutboundMessage",
                column: "InboundMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboundMessage",
                schema: "mirage_queue");

            migrationBuilder.DropTable(
                name: "ScheduledInboundMessage",
                schema: "mirage_queue");

            migrationBuilder.DropTable(
                name: "InboundMessage",
                schema: "mirage_queue");
        }
    }
}
