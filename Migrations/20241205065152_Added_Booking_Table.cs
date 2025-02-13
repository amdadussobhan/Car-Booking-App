using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wafi.SampleTest.Migrations
{
    /// <inheritdoc />
    public partial class Added_Booking_Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    RepeatOption = table.Column<int>(type: "int", nullable: false),
                    EndRepeatDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DaysToRepeatOn = table.Column<int>(type: "int", nullable: true),
                    RequestedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bookings");
        }
    }
}
