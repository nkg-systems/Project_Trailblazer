using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldOpsOptimizer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoreDataFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Technicians_TechnicianId1",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TechnicianId1",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TechnicianId1",
                table: "Users");

            migrationBuilder.RenameIndex(
                name: "IX_Technicians_EmployeeId_TenantId",
                table: "Technicians",
                newName: "IX_Technicians_EmployeeId_TenantId_Unique");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Users",
                type: "timestamp",
                nullable: true,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Technicians",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Technicians",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailabilityChangedAt",
                table: "Technicians",
                type: "timestamp with time zone",
                nullable: true,
                comment: "When the technician's availability status was last changed");

            migrationBuilder.AddColumn<Guid>(
                name: "AvailabilityChangedByUserId",
                table: "Technicians",
                type: "uuid",
                nullable: true,
                comment: "User ID who last changed the technician's availability");

            migrationBuilder.AddColumn<string>(
                name: "AvailabilityChangedByUserName",
                table: "Technicians",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "Name of user who last changed the technician's availability");

            migrationBuilder.AddColumn<string>(
                name: "AvailabilityNotes",
                table: "Technicians",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Additional notes about current availability status");

            migrationBuilder.AddColumn<bool>(
                name: "CanTakeEmergencyJobs",
                table: "Technicians",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                comment: "Whether the technician can be assigned emergency jobs even when unavailable");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedAvailableAt",
                table: "Technicians",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Expected time when technician will be available again (if currently unavailable)");

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrentlyAvailable",
                table: "Technicians",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                comment: "Whether the technician is currently available for new job assignments");

            migrationBuilder.AddColumn<int>(
                name: "MaxConcurrentJobs",
                table: "Technicians",
                type: "integer",
                nullable: false,
                defaultValue: 3,
                comment: "Maximum number of concurrent jobs this technician can handle");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Technicians",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0],
                comment: "Row version for optimistic concurrency control");

            migrationBuilder.AddColumn<string>(
                name: "UnavailabilityReason",
                table: "Technicians",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "Reason for current unavailability (if applicable)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ServiceJobs",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ServiceJobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCost",
                table: "ServiceJobs",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "JobType",
                table: "ServiceJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId",
                table: "ServiceJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Routes",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Routes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<double>(
                name: "EstimatedFuelSavings",
                table: "Routes",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "EstimatedTimeSavings",
                table: "Routes",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "IsOptimized",
                table: "Routes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OptimizationAlgorithm",
                table: "Routes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false, comment: "Note content (encrypted in production using AES-256)"),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Type of note determining visibility and purpose"),
                    IsCustomerVisible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Whether this note should be visible to customers"),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Whether this note contains sensitive information"),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false, comment: "ID of the user who created the note"),
                    AuthorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Name of the user who created the note (cached for performance)"),
                    AuthorRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "Role/position of the note author at time of creation"),
                    ServiceJobId = table.Column<Guid>(type: "uuid", nullable: false, comment: "ID of the service job this note belongs to"),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Tenant ID for multi-tenant isolation"),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true, comment: "IP address of the user who created the note (for audit trail)"),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "User agent of the client that created the note"),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "Session ID when the note was created"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Whether this note has been soft deleted"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "When the note was soft deleted"),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true, comment: "User ID who deleted the note"),
                    DeletedByUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, comment: "Name of user who deleted the note"),
                    DeletionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Reason for deletion"),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false, comment: "Concurrency token for optimistic locking"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobNotes_ServiceJobs",
                        column: x => x.ServiceJobId,
                        principalTable: "ServiceJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Notes attached to service jobs with full audit trail and security features");

            migrationBuilder.CreateTable(
                name: "JobStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceJobId = table.Column<Guid>(type: "uuid", nullable: false, comment: "ID of the service job whose status changed"),
                    JobNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Job number for easier identification in logs and reports"),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Tenant ID for multi-tenant isolation"),
                    FromStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "The status before the change"),
                    ToStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "The status after the change"),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "When the status change occurred (separate from audit timestamps)"),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false, comment: "ID of the user who made the status change"),
                    ChangedByUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Name of the user who made the status change (cached for performance)"),
                    ChangedByUserRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "Role of the user at the time of the change"),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Optional reason or comment for the status change"),
                    IsAutomaticChange = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Whether this was an automatic status change (vs manual)"),
                    ChangeSource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "Source system or component that triggered the change"),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true, comment: "IP address from which the change was made"),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "User agent of the client that made the change"),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "Session ID when the change was made"),
                    PreviousStatusDurationMinutes = table.Column<int>(type: "integer", nullable: true, comment: "Duration the job was in the previous status (in minutes)"),
                    TriggeredNotifications = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Whether this status change triggered any business rules or notifications"),
                    ValidationWarnings = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Any validation warnings that occurred during the status change"),
                    AppliedBusinessRules = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Business rules that were applied during this status change"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobStatusHistory_ServiceJobs",
                        column: x => x.ServiceJobId,
                        principalTable: "ServiceJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Complete history of status changes for service jobs with comprehensive audit information");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_AvailabilityChanged_Tenant",
                table: "Technicians",
                columns: new[] { "AvailabilityChangedAt", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_Available_Status_Tenant",
                table: "Technicians",
                columns: new[] { "IsCurrentlyAvailable", "Status", "TenantId" },
                filter: "Status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_Capacity_Available_Tenant",
                table: "Technicians",
                columns: new[] { "MaxConcurrentJobs", "IsCurrentlyAvailable", "TenantId" },
                filter: "IsCurrentlyAvailable = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_Emergency_Status_Tenant",
                table: "Technicians",
                columns: new[] { "CanTakeEmergencyJobs", "Status", "TenantId" },
                filter: "CanTakeEmergencyJobs = 1 AND Status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_Unavailable_Expected_Tenant",
                table: "Technicians",
                columns: new[] { "UnavailabilityReason", "ExpectedAvailableAt", "TenantId" },
                filter: "IsCurrentlyAvailable = 0 AND ExpectedAvailableAt IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobs_TechnicianId",
                table: "ServiceJobs",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_JobNotes_Author_Tenant_Deleted",
                table: "JobNotes",
                columns: new[] { "AuthorUserId", "TenantId", "IsDeleted" },
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JobNotes_CreatedAt_Tenant",
                table: "JobNotes",
                columns: new[] { "CreatedAt", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobNotes_CustomerVisible",
                table: "JobNotes",
                columns: new[] { "ServiceJobId", "IsCustomerVisible", "IsSensitive", "IsDeleted" },
                filter: "IsCustomerVisible = 1 AND IsSensitive = 0 AND IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JobNotes_ServiceJob_Tenant_Deleted",
                table: "JobNotes",
                columns: new[] { "ServiceJobId", "TenantId", "IsDeleted" },
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JobNotes_SoftDelete",
                table: "JobNotes",
                columns: new[] { "IsDeleted", "DeletedAt", "TenantId" },
                filter: "IsDeleted = 1");

            migrationBuilder.CreateIndex(
                name: "IX_JobNotes_TenantId",
                table: "JobNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JobNotes_Type_Tenant_Deleted",
                table: "JobNotes",
                columns: new[] { "Type", "TenantId", "IsDeleted" },
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_AutoChange_Tenant_ChangedAt",
                table: "JobStatusHistory",
                columns: new[] { "IsAutomaticChange", "TenantId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_ChangedAt_Tenant",
                table: "JobStatusHistory",
                columns: new[] { "ChangedAt", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_Duration_Status_Tenant",
                table: "JobStatusHistory",
                columns: new[] { "PreviousStatusDurationMinutes", "FromStatus", "TenantId" },
                filter: "PreviousStatusDurationMinutes IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_JobNumber_Tenant_ChangedAt",
                table: "JobStatusHistory",
                columns: new[] { "JobNumber", "TenantId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_Notifications_Tenant_ChangedAt",
                table: "JobStatusHistory",
                columns: new[] { "TriggeredNotifications", "TenantId", "ChangedAt" },
                filter: "TriggeredNotifications = 1");

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_ServiceJob_ChangedAt",
                table: "JobStatusHistory",
                columns: new[] { "ServiceJobId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_Source_Auto_Tenant",
                table: "JobStatusHistory",
                columns: new[] { "ChangeSource", "IsAutomaticChange", "TenantId" },
                filter: "ChangeSource IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_StatusTransition_Tenant_ChangedAt",
                table: "JobStatusHistory",
                columns: new[] { "FromStatus", "ToStatus", "TenantId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_TenantId",
                table: "JobStatusHistory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_User_Tenant_ChangedAt",
                table: "JobStatusHistory",
                columns: new[] { "ChangedByUserId", "TenantId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_Warnings_Rules_Tenant",
                table: "JobStatusHistory",
                columns: new[] { "ValidationWarnings", "AppliedBusinessRules", "TenantId" },
                filter: "ValidationWarnings IS NOT NULL OR AppliedBusinessRules IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceJobs_Technicians_TechnicianId",
                table: "ServiceJobs",
                column: "TechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobs_Technicians_TechnicianId",
                table: "ServiceJobs");

            migrationBuilder.DropTable(
                name: "JobNotes");

            migrationBuilder.DropTable(
                name: "JobStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_Technicians_AvailabilityChanged_Tenant",
                table: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_Technicians_Available_Status_Tenant",
                table: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_Technicians_Capacity_Available_Tenant",
                table: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_Technicians_Emergency_Status_Tenant",
                table: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_Technicians_Unavailable_Expected_Tenant",
                table: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_ServiceJobs_TechnicianId",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "AvailabilityChangedAt",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "AvailabilityChangedByUserId",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "AvailabilityChangedByUserName",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "AvailabilityNotes",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "CanTakeEmergencyJobs",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "ExpectedAvailableAt",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "IsCurrentlyAvailable",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "MaxConcurrentJobs",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "UnavailabilityReason",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "EstimatedCost",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "JobType",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "EstimatedFuelSavings",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "EstimatedTimeSavings",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "IsOptimized",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "OptimizationAlgorithm",
                table: "Routes");

            migrationBuilder.RenameIndex(
                name: "IX_Technicians_EmployeeId_TenantId_Unique",
                table: "Technicians",
                newName: "IX_Technicians_EmployeeId_TenantId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Users",
                type: "timestamp",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp",
                oldNullable: true,
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId1",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Technicians",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Technicians",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ServiceJobs",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ServiceJobs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Routes",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Routes",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TechnicianId1",
                table: "Users",
                column: "TechnicianId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Technicians_TechnicianId1",
                table: "Users",
                column: "TechnicianId1",
                principalTable: "Technicians",
                principalColumn: "Id");
        }
    }
}
