using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcademicRegistration.Infrastructure.Migrations.MySql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    EventType = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    PartitionKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "longtext", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Error = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_CreatedAtUtc",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");
        }
    }
}
