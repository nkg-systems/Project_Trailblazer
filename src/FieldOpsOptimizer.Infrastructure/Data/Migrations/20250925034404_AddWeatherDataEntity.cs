using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldOpsOptimizer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeatherDataEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeatherData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Location_Latitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: false, comment: "Geographic latitude"),
                    Location_Longitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: false, comment: "Geographic longitude"),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "When this weather data was recorded"),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "When this weather data expires"),
                    Condition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Primary weather condition"),
                    Temperature = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false, comment: "Temperature in Celsius"),
                    FeelsLike = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false, comment: "Perceived temperature in Celsius"),
                    Humidity = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false, comment: "Relative humidity percentage (0-100)"),
                    WindSpeed = table.Column<double>(type: "double precision", precision: 6, scale: 2, nullable: false, comment: "Wind speed in km/h"),
                    WindDirection = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false, comment: "Wind direction in degrees (0-360)"),
                    Pressure = table.Column<double>(type: "double precision", precision: 7, scale: 2, nullable: false, comment: "Atmospheric pressure in hPa"),
                    Visibility = table.Column<double>(type: "double precision", precision: 6, scale: 2, nullable: false, comment: "Visibility distance in kilometers"),
                    UvIndex = table.Column<double>(type: "double precision", precision: 4, scale: 2, nullable: false, comment: "UV index (0-11+)"),
                    PrecipitationChance = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false, comment: "Chance of precipitation percentage (0-100)"),
                    PrecipitationAmount = table.Column<double>(type: "double precision", precision: 6, scale: 2, nullable: false, comment: "Expected precipitation amount in mm"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Human-readable weather description"),
                    IconCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Weather icon identifier from the provider"),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Weather severity level for field work assessment"),
                    IsSuitableForFieldWork = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Whether weather conditions are suitable for field work"),
                    WorkSafetyNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Generated safety notes for field workers"),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Weather data provider (e.g., OpenWeatherMap, AccuWeather)"),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Tenant ID for multi-tenant isolation"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherData", x => x.Id);
                },
                comment: "Weather data for field operation planning and safety assessment");

            migrationBuilder.CreateIndex(
                name: "IX_WeatherData_Source_Time",
                table: "WeatherData",
                columns: new[] { "Source", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherData_Tenant_Condition_Severity",
                table: "WeatherData",
                columns: new[] { "TenantId", "Condition", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherData_Tenant_Suitability_Valid",
                table: "WeatherData",
                columns: new[] { "TenantId", "IsSuitableForFieldWork", "ValidUntil" },
                filter: "ValidUntil > GETUTCDATE()");

            migrationBuilder.CreateIndex(
                name: "IX_WeatherData_Tenant_Validity",
                table: "WeatherData",
                columns: new[] { "TenantId", "ValidUntil", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherData_TenantId",
                table: "WeatherData",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeatherData");
        }
    }
}
