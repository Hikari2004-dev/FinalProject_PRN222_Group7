using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FinalProject_PRN222_Group7.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingWalletAiCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "PackageId",
                table: "Payments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "CreditPackageId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiredAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayOrderCode",
                table: "Payments",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseType",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Payments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "UserSubscriptionId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingPeriod",
                table: "Packages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Packages",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Packages",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Packages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DurationInDays",
                table: "Packages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Packages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFree",
                table: "Packages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyCredit",
                table: "Packages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Packages",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "AiActionCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActionCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreditCost = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiActionCosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditWallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubscriptionCreditBalance = table.Column<int>(type: "int", nullable: false),
                    PurchasedCreditBalance = table.Column<int>(type: "int", nullable: false),
                    InternalCreditBalance = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditWallets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackageId = table.Column<int>(type: "int", nullable: false),
                    FeatureCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FeatureName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FeatureValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageFeatures_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentCallbackLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    GatewayProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GatewayOrderCode = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Signature = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsSignatureValid = table.Column<bool>(type: "bit", nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentCallbackLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentCallbackLogs_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PackageId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AutoRenew = table.Column<bool>(type: "bit", nullable: false),
                    CancelAtPeriodEnd = table.Column<bool>(type: "bit", nullable: false),
                    NextPackageId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Packages_NextPackageId",
                        column: x => x.NextPackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditWalletId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReferenceCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditTransactions_CreditWallets_CreditWalletId",
                        column: x => x.CreditWalletId,
                        principalTable: "CreditWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiUsageLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ActionCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditsCharged = table.Column<int>(type: "int", nullable: false),
                    TokensUsed = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditTransactionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiUsageLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiUsageLogs_CreditTransactions_CreditTransactionId",
                        column: x => x.CreditTransactionId,
                        principalTable: "CreditTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AiActionCosts",
                columns: new[] { "Id", "ActionCode", "ActionName", "CreditCost", "Description", "DisplayOrder", "IsActive" },
                values: new object[,]
                {
                    { 1, "chat.ask", "Hỏi đáp AI", 1, "Mỗi lượt hỏi chat AI tiêu tốn 1 credit.", 1, true },
                    { 2, "quiz.generate", "Sinh quiz bằng AI", 5, "Sinh một bài quiz từ tài liệu tiêu tốn 5 credit.", 2, true },
                    { 3, "flashcard.generate", "Sinh flashcard", 3, "Sinh flashcard từ tài liệu tiêu tốn 3 credit.", 3, true },
                    { 4, "summary.generate", "Tóm tắt tài liệu", 2, "Tóm tắt tài liệu tiêu tốn 2 credit.", 4, true }
                });

            migrationBuilder.InsertData(
                table: "CreditPackages",
                columns: new[] { "Id", "Code", "CreatedAt", "Credits", "Description", "DisplayOrder", "IsActive", "IsFeatured", "Name", "Price", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "credit-500", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc), 500, "Bổ sung nhanh cho các tác vụ học tập hằng ngày.", 1, true, false, "500 AI Credit", 29000m, new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "credit-1500", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1500, "Gói tiết kiệm cho kỳ học có cường độ sử dụng AI cao.", 2, true, true, "1.500 AI Credit", 79000m, new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "credit-4000", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc), 4000, "Phù hợp khi cần học tăng tốc hoặc ôn thi dài hạn.", 3, true, false, "4.000 AI Credit", 179000m, new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.UpdateData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BillingPeriod", "Code", "CreatedAt", "Description", "DisplayOrder", "DurationInDays", "IsActive", "IsFeatured", "IsFree", "MonthlyCredit", "Name", "UpdatedAt" },
                values: new object[] { 1, "legacy-basic", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Gói cũ để giữ tương thích dữ liệu hiện tại.", 90, 30, false, false, true, 50, "Basic (Legacy)", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "BillingPeriod", "Code", "CreatedAt", "Description", "DisplayOrder", "DurationInDays", "IsActive", "IsFeatured", "IsFree", "MonthlyCredit", "Name", "UpdatedAt" },
                values: new object[] { 1, "legacy-pro", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Gói cũ để giữ tương thích dữ liệu hiện tại.", 91, 30, false, false, false, 500, "Pro (Legacy)", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "BillingPeriod", "Code", "CreatedAt", "Description", "DisplayOrder", "DurationInDays", "IsActive", "IsFeatured", "IsFree", "MonthlyCredit", "Name", "UpdatedAt" },
                values: new object[] { 1, "legacy-ultra", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Gói cũ để giữ tương thích dữ liệu hiện tại.", 92, 30, false, false, false, 2000, "Ultra (Legacy)", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "Packages",
                columns: new[] { "Id", "BillingPeriod", "Code", "CreatedAt", "Description", "DisplayOrder", "DurationInDays", "HasBenchmark", "HasQuizGeneration", "IsActive", "IsFeatured", "IsFree", "MaxDocuments", "MonthlyAiQueries", "MonthlyCredit", "Name", "Price", "Tier", "UpdatedAt" },
                values: new object[,]
                {
                    { 10, 1, "free", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Gói miễn phí cho sinh viên với credit cơ bản hằng tháng.", 1, 30, false, false, true, false, true, 0, 50, 50, "Free", 0m, 3, new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, 1, "basic", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Phù hợp cho sinh viên cần dùng AI thường xuyên trong một môn học.", 2, 30, false, true, true, false, false, 0, 250, 250, "Basic", 49000m, 0, new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, 1, "plus", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Gói phổ biến nhất cho sinh viên dùng chat, quiz và phân tích học tập nâng cao.", 3, 30, false, true, true, true, false, 0, 800, 800, "Plus", 99000m, 4, new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, 1, "research", new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Gói cao nhất cho nhu cầu nghiên cứu, tổng hợp và phân tích AI chuyên sâu.", 4, 30, true, true, true, false, false, 0, 2000, 2000, "Research", 199000m, 5, new DateTime(2026, 7, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "PackageFeatures",
                columns: new[] { "Id", "DisplayOrder", "FeatureCode", "FeatureName", "FeatureValue", "IsEnabled", "PackageId" },
                values: new object[,]
                {
                    { 1, 1, "ai.chat.single_kb", "Chat AI theo môn học", "basic", true, 10 },
                    { 2, 2, "ai.quiz.basic", "Làm quiz AI", "false", false, 10 },
                    { 3, 1, "ai.chat.single_kb", "Chat AI theo môn học", "standard", true, 11 },
                    { 4, 2, "ai.quiz.basic", "Sinh quiz cơ bản", "true", true, 11 },
                    { 5, 1, "ai.chat.multi_doc", "Chat AI đa tài liệu", "true", true, 12 },
                    { 6, 2, "ai.quiz.basic", "Sinh quiz AI", "true", true, 12 },
                    { 7, 3, "ai.flashcard.basic", "Flashcard AI", "true", true, 12 },
                    { 8, 1, "ai.deep_analysis", "Phân tích AI chuyên sâu", "true", true, 13 },
                    { 9, 2, "benchmark.advanced", "Benchmark nâng cao", "true", true, 13 },
                    { 10, 3, "priority.processing", "Ưu tiên xử lý", "high", true, 13 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreditPackageId",
                table: "Payments",
                column: "CreditPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_GatewayOrderCode",
                table: "Payments",
                column: "GatewayOrderCode",
                unique: true,
                filter: "[GatewayOrderCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UserSubscriptionId",
                table: "Payments",
                column: "UserSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Code",
                table: "Packages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiActionCosts_ActionCode",
                table: "AiActionCosts",
                column: "ActionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_CreditTransactionId",
                table: "AiUsageLogs",
                column: "CreditTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_RequestId",
                table: "AiUsageLogs",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_UserId",
                table: "AiUsageLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPackages_Code",
                table: "CreditPackages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_CreditWalletId",
                table: "CreditTransactions",
                column: "CreditWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_RequestId",
                table: "CreditTransactions",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditWallets_UserId",
                table: "CreditWallets",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageFeatures_PackageId_FeatureCode",
                table: "PackageFeatures",
                columns: new[] { "PackageId", "FeatureCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCallbackLogs_GatewayProvider_GatewayOrderCode_Signature",
                table: "PaymentCallbackLogs",
                columns: new[] { "GatewayProvider", "GatewayOrderCode", "Signature" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentCallbackLogs_PaymentId",
                table: "PaymentCallbackLogs",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_NextPackageId",
                table: "UserSubscriptions",
                column: "NextPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PackageId",
                table: "UserSubscriptions",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId",
                table: "UserSubscriptions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_CreditPackages_CreditPackageId",
                table: "Payments",
                column: "CreditPackageId",
                principalTable: "CreditPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_UserSubscriptions_UserSubscriptionId",
                table: "Payments",
                column: "UserSubscriptionId",
                principalTable: "UserSubscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_CreditPackages_CreditPackageId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_UserSubscriptions_UserSubscriptionId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "AiActionCosts");

            migrationBuilder.DropTable(
                name: "AiUsageLogs");

            migrationBuilder.DropTable(
                name: "CreditPackages");

            migrationBuilder.DropTable(
                name: "PackageFeatures");

            migrationBuilder.DropTable(
                name: "PaymentCallbackLogs");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "CreditTransactions");

            migrationBuilder.DropTable(
                name: "CreditWallets");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CreditPackageId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_GatewayOrderCode",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_UserSubscriptionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Packages_Code",
                table: "Packages");

            migrationBuilder.DeleteData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DropColumn(
                name: "CreditPackageId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ExpiredAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "GatewayOrderCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PurchaseType",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "UserSubscriptionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BillingPeriod",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "DurationInDays",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "IsFree",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "MonthlyCredit",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Packages");

            migrationBuilder.AlterColumn<int>(
                name: "PackageId",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "IsActive", "Name" },
                values: new object[] { "Gói miễn phí cho sinh viên", true, "Basic" });

            migrationBuilder.UpdateData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "IsActive", "Name" },
                values: new object[] { "Gói nâng cao cho giảng viên", true, "Pro" });

            migrationBuilder.UpdateData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "IsActive", "Name" },
                values: new object[] { "Không giới hạn - full features", true, "Ultra" });
        }
    }
}
