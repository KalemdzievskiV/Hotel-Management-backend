using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddShortStaySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowsShortStay",
                table: "Rooms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaximumShortStayHours",
                table: "Rooms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumShortStayHours",
                table: "Rooms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShortStayHourlyRate",
                table: "Rooms",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowsShortStay",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "MaximumShortStayHours",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "MinimumShortStayHours",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "ShortStayHourlyRate",
                table: "Rooms");
        }
    }
}
