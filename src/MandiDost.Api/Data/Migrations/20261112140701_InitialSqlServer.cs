using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MandiDost.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Section = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IconKey = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    HasMilkProduction = table.Column<bool>(type: "bit", nullable: false),
                    AllowsQurbaniShares = table.Column<bool>(type: "bit", nullable: false),
                    DefaultPriceUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DistrictUr = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    IsLaunchArea = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhoneNumber = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Verification = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PhoneVerified = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryPurposes",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryPurposes", x => new { x.CategoryId, x.Purpose });
                    table.ForeignKey(
                        name: "FK_CategoryPurposes_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Verification = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    VerificationRequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedById = table.Column<int>(type: "int", nullable: true),
                    VerificationNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessProfiles_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessProfiles_Users_VerifiedById",
                        column: x => x.VerifiedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Followers",
                columns: table => new
                {
                    FollowerId = table.Column<int>(type: "int", nullable: false),
                    SellerId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Followers", x => new { x.FollowerId, x.SellerId });
                    table.CheckConstraint("CK_Followers_NotSelf", "FollowerId <> SellerId");
                    table.ForeignKey(
                        name: "FK_Followers_Users_FollowerId",
                        column: x => x.FollowerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Followers_Users_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Listings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SellerId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    AreaText = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Price = table.Column<long>(type: "bigint", nullable: true),
                    PriceUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsNegotiable = table.Column<bool>(type: "bit", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    QuantityUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MinOrderQuantity = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    IsBulkAvailable = table.Column<bool>(type: "bit", nullable: false),
                    DeliveryAvailable = table.Column<bool>(type: "bit", nullable: false),
                    VoiceNoteUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    VoiceNoteSeconds = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SoldAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Listings", x => x.Id);
                    table.CheckConstraint("CK_Listings_Price", "Price IS NULL OR Price > 0");
                    table.CheckConstraint("CK_Listings_Quantity", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_Listings_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Listings_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Listings_Users_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuyerId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    QuantityUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MinWeightKg = table.Column<int>(type: "int", nullable: true),
                    MaxWeightKg = table.Column<int>(type: "int", nullable: true),
                    TargetPrice = table.Column<long>(type: "bigint", nullable: true),
                    TargetPriceUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    NeededBy = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveryNeeded = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OfferCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequests", x => x.Id);
                    table.CheckConstraint("CK_PurchaseRequests_Quantity", "Quantity > 0");
                    table.CheckConstraint("CK_PurchaseRequests_Weight", "MinWeightKg IS NULL OR MaxWeightKg IS NULL OR MinWeightKg <= MaxWeightKg");
                    table.ForeignKey(
                        name: "FK_PurchaseRequests_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequests_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequests_Users_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedByIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Bio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    WhatsAppNumber = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ShowPhoneNumber = table.Column<bool>(type: "bit", nullable: false),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    FollowerCount = table.Column<int>(type: "int", nullable: false),
                    SoldCount = table.Column<int>(type: "int", nullable: false),
                    RatingAverage = table.Column<double>(type: "float", nullable: false),
                    RatingCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Chats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuyerId = table.Column<int>(type: "int", nullable: false),
                    SellerId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chats", x => x.Id);
                    table.CheckConstraint("CK_Chats_DifferentParties", "BuyerId <> SellerId");
                    table.ForeignKey(
                        name: "FK_Chats_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Chats_Users_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Chats_Users_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CropDetails",
                columns: table => new
                {
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Variety = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    HarvestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PickupAvailable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropDetails", x => x.ListingId);
                    table.ForeignKey(
                        name: "FK_CropDetails_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Favorites",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => new { x.UserId, x.ListingId });
                    table.ForeignKey(
                        name: "FK_Favorites_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Favorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListingMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingMedia_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListingPurposes",
                columns: table => new
                {
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingPurposes", x => new { x.ListingId, x.Purpose });
                    table.ForeignKey(
                        name: "FK_ListingPurposes_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LivestockDetails",
                columns: table => new
                {
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Breed = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AgeMonths = table.Column<int>(type: "int", nullable: true),
                    Teeth = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    LiveWeightKg = table.Column<int>(type: "int", nullable: true),
                    IsVaccinated = table.Column<bool>(type: "bit", nullable: true),
                    IsPregnant = table.Column<bool>(type: "bit", nullable: true),
                    MilkLitresPerDay = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    HealthNotes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LivestockDetails", x => x.ListingId);
                    table.ForeignKey(
                        name: "FK_LivestockDetails_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeatSupplyDetails",
                columns: table => new
                {
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PricePerKgLive = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeatSupplyDetails", x => x.ListingId);
                    table.ForeignKey(
                        name: "FK_MeatSupplyDetails_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QurbaniBookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    BuyerId = table.Column<int>(type: "int", nullable: false),
                    Shares = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QurbaniBookings", x => x.Id);
                    table.CheckConstraint("CK_QurbaniBookings_Shares", "Shares BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_QurbaniBookings_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QurbaniBookings_Users_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QurbaniDetails",
                columns: table => new
                {
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    TotalShares = table.Column<int>(type: "int", nullable: false),
                    PricePerShare = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QurbaniDetails", x => x.ListingId);
                    table.CheckConstraint("CK_Qurbani_Shares", "TotalShares IN (1, 7)");
                    table.ForeignKey(
                        name: "FK_QurbaniDetails_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    ReporterId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedById = table.Column<int>(type: "int", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reports_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reports_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Users_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SellerId = table.Column<int>(type: "int", nullable: false),
                    ReviewerId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.CheckConstraint("CK_Reviews_NotSelf", "ReviewerId <> SellerId");
                    table.CheckConstraint("CK_Reviews_Rating", "Rating BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Reviews_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplyDetails",
                columns: table => new
                {
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyDetails", x => x.ListingId);
                    table.ForeignKey(
                        name: "FK_SupplyDetails_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Videos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SellerId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Caption = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    LikeCount = table.Column<int>(type: "int", nullable: false),
                    CommentCount = table.Column<int>(type: "int", nullable: false),
                    ShareCount = table.Column<int>(type: "int", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Videos_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Videos_Users_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequestOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseRequestId = table.Column<int>(type: "int", nullable: false),
                    SellerId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: true),
                    Price = table.Column<long>(type: "bigint", nullable: true),
                    PriceUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestOffers_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RequestOffers_PurchaseRequests_PurchaseRequestId",
                        column: x => x.PurchaseRequestId,
                        principalTable: "PurchaseRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestOffers_Users_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<int>(type: "int", nullable: false),
                    SenderId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MediaUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comments_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Likes",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    VideoId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Likes", x => new { x.UserId, x.VideoId });
                    table.ForeignKey(
                        name: "FK_Likes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Likes_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "AllowsQurbaniShares", "DefaultPriceUnit", "HasMilkProduction", "IconKey", "IsActive", "NameEn", "NameUr", "ParentId", "Section", "Slug", "SortOrder" },
                values: new object[,]
                {
                    { 1, true, "PerItem", true, "cow", true, "Cow / bull", "گائے / بیل", null, "Livestock", "cow", 1 },
                    { 2, true, "PerItem", true, "cow", true, "Buffalo", "بھینس / کٹا", null, "Livestock", "buffalo", 2 },
                    { 3, false, "PerItem", true, "sheep", true, "Goat", "بکری / بکرا", null, "Livestock", "goat", 3 },
                    { 4, false, "PerItem", false, "sheep", true, "Sheep", "بھیڑ / دنبہ", null, "Livestock", "sheep", 4 },
                    { 5, true, "PerItem", true, "horse-variant", true, "Camel", "اونٹ", null, "Livestock", "camel", 5 },
                    { 6, false, "PerItem", false, "paw", true, "Other livestock", "دیگر جانور", null, "Livestock", "other", 6 },
                    { 7, false, "PerMaund", false, "barley", true, "Wheat", "گندم", null, "Crops", "wheat", 7 },
                    { 8, false, "PerMaund", false, "flower-outline", true, "Cotton", "کپاس", null, "Crops", "cotton", 8 },
                    { 9, false, "PerMaund", false, "rice", true, "Rice / paddy", "چاول / دھان", null, "Crops", "rice", 9 },
                    { 10, false, "PerMaund", false, "corn", true, "Maize", "مکئی", null, "Crops", "maize", 10 },
                    { 11, false, "PerMaund", false, "grass", true, "Sugarcane", "گنا", null, "Crops", "sugarcane", 11 },
                    { 12, false, "PerKg", false, "carrot", true, "Vegetables", "سبزیاں", null, "Crops", "vegetables", 12 },
                    { 13, false, "PerKg", false, "fruit-cherries", true, "Fruits", "پھل", null, "Crops", "fruits", 13 },
                    { 14, false, "PerMaund", false, "basket-outline", true, "Other produce", "دیگر پیداوار", null, "Crops", "other-produce", 14 },
                    { 15, false, "PerBag", false, "seed-outline", true, "Seeds", "بیج", null, "Supplies", "seeds", 15 },
                    { 16, false, "PerBag", false, "sack", true, "Fertilizer", "کھاد", null, "Supplies", "fertilizer", 16 },
                    { 17, false, "PerItem", false, "spray", true, "Pesticides", "زرعی ادویات", null, "Supplies", "pesticides", 17 },
                    { 18, false, "PerBag", false, "grain", true, "Animal feed and fodder", "چارہ اور ونڈا", null, "Supplies", "feed", 18 },
                    { 19, false, "PerItem", false, "shovel", true, "Farming tools", "اوزار", null, "Supplies", "tools", 19 },
                    { 20, false, "PerItem", false, "tractor", true, "Machinery", "مشینری", null, "Supplies", "machinery", 20 },
                    { 21, false, "PerItem", false, "bucket-outline", true, "Dairy and livestock equipment", "ڈیری کا سامان", null, "Supplies", "dairy-equipment", 21 }
                });

            migrationBuilder.InsertData(
                table: "Locations",
                columns: new[] { "Id", "DistrictUr", "IsActive", "IsLaunchArea", "Latitude", "Longitude", "NameEn", "NameUr", "Province", "Slug" },
                values: new object[,]
                {
                    { 1, "لودھراں", true, true, 29.533899999999999, 71.632400000000004, "Lodhran", "لودھراں", "Punjab", "lodhran" },
                    { 2, "لودھراں", true, true, 29.802, 71.742999999999995, "Dunyapur", "دنیا پور", "Punjab", "dunyapur" },
                    { 3, "لودھراں", true, true, 29.623699999999999, 71.916499999999999, "Kahror Pakka", "کہروڑ پکا", "Punjab", "kahror" },
                    { 4, "ملتان", true, false, 29.504999999999999, 71.221000000000004, "Jalalpur Pirwala", "جلال پور پیر والا", "Punjab", "jalalpur" },
                    { 5, "ملتان", true, false, 30.157499999999999, 71.524900000000002, "Multan", "ملتان", "Punjab", "multan" },
                    { 6, "ملتان", true, false, 29.879000000000001, 71.295000000000002, "Shujabad", "شجاع آباد", "Punjab", "shujabad" },
                    { 7, "بہاولپور", true, false, 29.395600000000002, 71.683599999999998, "Bahawalpur", "بہاولپور", "Punjab", "bahawalpur" },
                    { 8, "خانیوال", true, false, 30.3017, 71.932100000000005, "Khanewal", "خانیوال", "Punjab", "khanewal" },
                    { 9, "وہاڑی", true, false, 30.045200000000001, 72.3489, "Vehari", "وہاڑی", "Punjab", "vehari" },
                    { 10, "وہاڑی", true, false, 29.800000000000001, 72.174999999999997, "Mailsi", "میلسی", "Punjab", "mailsi" }
                });

            migrationBuilder.InsertData(
                table: "CategoryPurposes",
                columns: new[] { "CategoryId", "Purpose" },
                values: new object[,]
                {
                    { 1, "Breeding" },
                    { 1, "Dairy" },
                    { 1, "General" },
                    { 1, "MeatSupply" },
                    { 1, "Qurbani" },
                    { 2, "Breeding" },
                    { 2, "Dairy" },
                    { 2, "General" },
                    { 2, "MeatSupply" },
                    { 2, "Qurbani" },
                    { 3, "Breeding" },
                    { 3, "Dairy" },
                    { 3, "General" },
                    { 3, "MeatSupply" },
                    { 3, "Qurbani" },
                    { 4, "Breeding" },
                    { 4, "General" },
                    { 4, "MeatSupply" },
                    { 4, "Qurbani" },
                    { 5, "Breeding" },
                    { 5, "Dairy" },
                    { 5, "General" },
                    { 5, "MeatSupply" },
                    { 5, "Qurbani" },
                    { 6, "Breeding" },
                    { 6, "General" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_LocationId",
                table: "BusinessProfiles",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_UserId",
                table: "BusinessProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_Verification_VerificationRequestedAtUtc",
                table: "BusinessProfiles",
                columns: new[] { "Verification", "VerificationRequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_VerifiedById",
                table: "BusinessProfiles",
                column: "VerifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Section_SortOrder",
                table: "Categories",
                columns: new[] { "Section", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chats_BuyerId_LastMessageAtUtc",
                table: "Chats",
                columns: new[] { "BuyerId", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Chats_BuyerId_SellerId_ListingId",
                table: "Chats",
                columns: new[] { "BuyerId", "SellerId", "ListingId" });

            migrationBuilder.CreateIndex(
                name: "IX_Chats_ListingId",
                table: "Chats",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_SellerId_LastMessageAtUtc",
                table: "Chats",
                columns: new[] { "SellerId", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId",
                table: "Comments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_VideoId_CreatedAtUtc",
                table: "Comments",
                columns: new[] { "VideoId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_ListingId",
                table: "Favorites",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_UserId_CreatedAtUtc",
                table: "Favorites",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Followers_SellerId",
                table: "Followers",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_VideoId",
                table: "Likes",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingMedia_ListingId_SortOrder",
                table: "ListingMedia",
                columns: new[] { "ListingId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ListingPurposes_Purpose_ListingId",
                table: "ListingPurposes",
                columns: new[] { "Purpose", "ListingId" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_CategoryId",
                table: "Listings",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_LocationId",
                table: "Listings",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_SellerId_Status",
                table: "Listings",
                columns: new[] { "SellerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status_CategoryId_PublishedAtUtc",
                table: "Listings",
                columns: new[] { "Status", "CategoryId", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status_ExpiresAtUtc",
                table: "Listings",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status_LocationId_PublishedAtUtc",
                table: "Listings",
                columns: new[] { "Status", "LocationId", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status_Price",
                table: "Listings",
                columns: new[] { "Status", "Price" });

            migrationBuilder.CreateIndex(
                name: "IX_LivestockDetails_Breed",
                table: "LivestockDetails",
                column: "Breed");

            migrationBuilder.CreateIndex(
                name: "IX_LivestockDetails_Gender_Teeth",
                table: "LivestockDetails",
                columns: new[] { "Gender", "Teeth" });

            migrationBuilder.CreateIndex(
                name: "IX_LivestockDetails_LiveWeightKg",
                table: "LivestockDetails",
                column: "LiveWeightKg");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Slug",
                table: "Locations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatId_CreatedAtUtc",
                table: "Messages",
                columns: new[] { "ChatId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId",
                table: "Messages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_BuyerId_CreatedAtUtc",
                table: "PurchaseRequests",
                columns: new[] { "BuyerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_CategoryId",
                table: "PurchaseRequests",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_LocationId",
                table: "PurchaseRequests",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_Status_CategoryId",
                table: "PurchaseRequests",
                columns: new[] { "Status", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_Status_ExpiresAtUtc_CreatedAtUtc",
                table: "PurchaseRequests",
                columns: new[] { "Status", "ExpiresAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QurbaniBookings_BuyerId_CreatedAtUtc",
                table: "QurbaniBookings",
                columns: new[] { "BuyerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QurbaniBookings_ListingId_Status",
                table: "QurbaniBookings",
                columns: new[] { "ListingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAtUtc",
                table: "RefreshTokens",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ListingId_ReporterId",
                table: "Reports",
                columns: new[] { "ListingId", "ReporterId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterId",
                table: "Reports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ResolvedById",
                table: "Reports",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status_CreatedAtUtc",
                table: "Reports",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestOffers_ListingId",
                table: "RequestOffers",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestOffers_PurchaseRequestId_SellerId",
                table: "RequestOffers",
                columns: new[] { "PurchaseRequestId", "SellerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestOffers_SellerId_UpdatedAtUtc",
                table: "RequestOffers",
                columns: new[] { "SellerId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ListingId",
                table: "Reviews",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewerId_SellerId_ListingId",
                table: "Reviews",
                columns: new[] { "ReviewerId", "SellerId", "ListingId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_SellerId_CreatedAtUtc",
                table: "Reviews",
                columns: new[] { "SellerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_LocationId",
                table: "UserProfiles",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedAtUtc",
                table: "Users",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumber",
                table: "Users",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Videos_ListingId",
                table: "Videos",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_SellerId_CreatedAtUtc",
                table: "Videos",
                columns: new[] { "SellerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Videos_Status_CreatedAtUtc",
                table: "Videos",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessProfiles");

            migrationBuilder.DropTable(
                name: "CategoryPurposes");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "CropDetails");

            migrationBuilder.DropTable(
                name: "Favorites");

            migrationBuilder.DropTable(
                name: "Followers");

            migrationBuilder.DropTable(
                name: "Likes");

            migrationBuilder.DropTable(
                name: "ListingMedia");

            migrationBuilder.DropTable(
                name: "ListingPurposes");

            migrationBuilder.DropTable(
                name: "LivestockDetails");

            migrationBuilder.DropTable(
                name: "MeatSupplyDetails");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "QurbaniBookings");

            migrationBuilder.DropTable(
                name: "QurbaniDetails");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "RequestOffers");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "SupplyDetails");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "Videos");

            migrationBuilder.DropTable(
                name: "Chats");

            migrationBuilder.DropTable(
                name: "PurchaseRequests");

            migrationBuilder.DropTable(
                name: "Listings");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
