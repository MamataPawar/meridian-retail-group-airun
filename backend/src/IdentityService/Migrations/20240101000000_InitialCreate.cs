using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "active"),
                    MergedIntoId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Customers", x => x.Id));

            migrationBuilder.CreateTable(
                name: "IdentityLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    System = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityLinks", x => x.Id);
                    table.ForeignKey("FK_IdentityLinks_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Line1 = table.Column<string>(type: "text", nullable: false),
                    Line2 = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: false),
                    PostalCode = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                    table.ForeignKey("FK_Addresses_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Consents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Jurisdiction = table.Column<string>(type: "text", nullable: false),
                    ConsentType = table.Column<string>(type: "text", nullable: false),
                    Granted = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consents", x => x.Id);
                    table.ForeignKey("FK_Consents_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MergeAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MergedCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MergeType = table.Column<string>(type: "text", nullable: false),
                    ReviewerId = table.Column<string>(type: "text", nullable: true),
                    MergedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UndoneAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_MergeAudits", x => x.Id));

            migrationBuilder.CreateTable(
                name: "MergeCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerAId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerBId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    MatchReason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    ReviewerId = table.Column<string>(type: "text", nullable: true),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MergeCandidates", x => x.Id);
                    table.ForeignKey("FK_MergeCandidates_Customers_CustomerAId", x => x.CustomerAId, "Customers", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_MergeCandidates_Customers_CustomerBId", x => x.CustomerBId, "Customers", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DsarRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Jurisdiction = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueBy = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExportPath = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DsarRequests", x => x.Id);
                    table.ForeignKey("FK_DsarRequests_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    TraceId = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    Channel = table.Column<string>(type: "text", nullable: true),
                    StoreId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));

            migrationBuilder.CreateIndex("IX_Customers_Email", "Customers", "Email");
            migrationBuilder.CreateIndex("IX_Customers_Phone", "Customers", "Phone");
            migrationBuilder.CreateIndex("IX_Customers_Status", "Customers", "Status");
            migrationBuilder.CreateIndex("IX_IdentityLinks_System_ExternalId", "IdentityLinks", new[] { "System", "ExternalId" }, unique: true);
            migrationBuilder.CreateIndex("IX_MergeCandidates_Status", "MergeCandidates", "Status");
            migrationBuilder.CreateIndex("IX_MergeCandidates_ConfidenceScore", "MergeCandidates", "ConfidenceScore");
            migrationBuilder.CreateIndex("IX_DsarRequests_Status", "DsarRequests", "Status");
            migrationBuilder.CreateIndex("IX_DsarRequests_DueBy", "DsarRequests", "DueBy");
            migrationBuilder.CreateIndex("IX_OutboxMessages_ProcessedAt", "OutboxMessages", "ProcessedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("OutboxMessages");
            migrationBuilder.DropTable("DsarRequests");
            migrationBuilder.DropTable("MergeCandidates");
            migrationBuilder.DropTable("MergeAudits");
            migrationBuilder.DropTable("Consents");
            migrationBuilder.DropTable("Addresses");
            migrationBuilder.DropTable("IdentityLinks");
            migrationBuilder.DropTable("Customers");
        }
    }
}
