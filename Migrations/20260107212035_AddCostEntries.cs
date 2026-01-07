using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordPA.Migrations
{
    /// <inheritdoc />
    public partial class AddCostEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    DateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalCost = table.Column<double>(type: "REAL", nullable: false),
                    RequestCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_GuildId_DateUtc",
                table: "CostEntries",
                columns: new[] { "GuildId", "DateUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostEntries");
        }
    }
}
