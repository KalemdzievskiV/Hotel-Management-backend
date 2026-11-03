using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelManagement.Migrations
{
    /// <inheritdoc />
    public partial class ShortStayFlatPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaximumShortStayHours",
                table: "Rooms");

            migrationBuilder.RenameColumn(
                name: "ShortStayHourlyRate",
                table: "Rooms",
                newName: "ShortStayPrice");

            migrationBuilder.RenameColumn(
                name: "MinimumShortStayHours",
                table: "Rooms",
                newName: "ShortStayHours");

            // Hourly rate -> flat price for a standard 4-hour short stay
            migrationBuilder.Sql("""
                UPDATE "Rooms"
                SET "ShortStayHours" = CASE WHEN "AllowsShortStay" THEN 4 ELSE NULL END,
                    "ShortStayPrice" = CASE WHEN "AllowsShortStay" THEN "ShortStayPrice" * 4 ELSE NULL END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Rooms"
                SET "ShortStayPrice" = "ShortStayPrice" / COALESCE(NULLIF("ShortStayHours", 0), 4);
                """);

            migrationBuilder.RenameColumn(
                name: "ShortStayPrice",
                table: "Rooms",
                newName: "ShortStayHourlyRate");

            migrationBuilder.RenameColumn(
                name: "ShortStayHours",
                table: "Rooms",
                newName: "MinimumShortStayHours");

            migrationBuilder.AddColumn<int>(
                name: "MaximumShortStayHours",
                table: "Rooms",
                type: "integer",
                nullable: true);
        }
    }
}
