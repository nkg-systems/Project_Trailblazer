using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldOpsOptimizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Technicians",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HomeAddress_Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HomeAddress_Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HomeAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HomeAddress_State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HomeAddress_PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    HomeAddress_Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HomeAddress_Coordinate_Latitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: true),
                    HomeAddress_Coordinate_Longitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: true),
                    CurrentLocation_Latitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: true),
                    CurrentLocation_Longitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: true),
                    LastLocationUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Skills = table.Column<string>(type: "jsonb", nullable: false),
                    WorkingHours = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Technicians", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedTechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalDistanceKm = table.Column<double>(type: "double precision", precision: 10, scale: 3, nullable: false),
                    EstimatedDuration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OptimizationObjective = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Routes_Technicians_AssignedTechnicianId",
                        column: x => x.AssignedTechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CustomerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ServiceAddress_Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServiceAddress_Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ServiceAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ServiceAddress_State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ServiceAddress_PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ServiceAddress_Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "US"),
                    ServiceAddress_Coordinate_Latitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: true),
                    ServiceAddress_Coordinate_Longitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreferredTimeWindow = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EstimatedDuration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedTechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    EstimatedRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequiredSkills = table.Column<string>(type: "jsonb", nullable: false),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceJobs_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ServiceJobs_Technicians_AssignedTechnicianId",
                        column: x => x.AssignedTechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RouteStop",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    EstimatedTravelTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    DistanceFromPreviousKm = table.Column<double>(type: "double precision", precision: 10, scale: 3, nullable: false),
                    EstimatedArrival = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RouteId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStop", x => new { x.JobId, x.RouteId });
                    table.ForeignKey(
                        name: "FK_RouteStop_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteStop_Routes_RouteId1",
                        column: x => x.RouteId1,
                        principalTable: "Routes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RouteStop_ServiceJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "ServiceJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Routes_AssignedTechnicianId",
                table: "Routes",
                column: "AssignedTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_ScheduledDate",
                table: "Routes",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_Status",
                table: "Routes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_TenantId",
                table: "Routes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStop_EstimatedArrival",
                table: "RouteStop",
                column: "EstimatedArrival");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStop_RouteId",
                table: "RouteStop",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStop_RouteId1",
                table: "RouteStop",
                column: "RouteId1");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStop_SequenceOrder",
                table: "RouteStop",
                column: "SequenceOrder");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobs_AssignedTechnicianId",
                table: "ServiceJobs",
                column: "AssignedTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobs_JobNumber_TenantId",
                table: "ServiceJobs",
                columns: new[] { "JobNumber", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobs_Priority",
                table: "ServiceJobs",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobs_RouteId",
                table: "ServiceJobs",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobs_ScheduledDate",
                table: "ServiceJobs",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobs_Status",
                table: "ServiceJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobs_TenantId",
                table: "ServiceJobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_Email",
                table: "Technicians",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_EmployeeId_TenantId",
                table: "Technicians",
                columns: new[] { "EmployeeId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_Status",
                table: "Technicians",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_TenantId",
                table: "Technicians",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RouteStop");

            migrationBuilder.DropTable(
                name: "ServiceJobs");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropTable(
                name: "Technicians");
        }
    }
}
