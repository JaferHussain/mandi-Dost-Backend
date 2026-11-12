IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [ParentId] int NULL,
        [Section] nvarchar(16) NOT NULL,
        [Slug] nvarchar(32) NOT NULL,
        [NameUr] nvarchar(64) NOT NULL,
        [NameEn] nvarchar(64) NOT NULL,
        [IconKey] nvarchar(48) NULL,
        [HasMilkProduction] bit NOT NULL,
        [AllowsQurbaniShares] bit NOT NULL,
        [DefaultPriceUnit] nvarchar(16) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Categories_Categories_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Locations] (
        [Id] int NOT NULL IDENTITY,
        [Slug] nvarchar(48) NOT NULL,
        [NameUr] nvarchar(64) NOT NULL,
        [NameEn] nvarchar(64) NOT NULL,
        [DistrictUr] nvarchar(64) NOT NULL,
        [Province] nvarchar(32) NOT NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [IsLaunchArea] bit NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Locations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [PhoneNumber] nvarchar(16) NOT NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [FullName] nvarchar(80) NOT NULL,
        [Role] nvarchar(16) NOT NULL,
        [Verification] nvarchar(16) NOT NULL,
        [PhoneVerified] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [FailedLoginCount] int NOT NULL,
        [LockoutEndUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [LastLoginAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [CategoryPurposes] (
        [CategoryId] int NOT NULL,
        [Purpose] nvarchar(16) NOT NULL,
        CONSTRAINT [PK_CategoryPurposes] PRIMARY KEY ([CategoryId], [Purpose]),
        CONSTRAINT [FK_CategoryPurposes_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [BusinessProfiles] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [BusinessName] nvarchar(80) NOT NULL,
        [Type] nvarchar(16) NOT NULL,
        [LocationId] int NOT NULL,
        [Address] nvarchar(200) NULL,
        [Description] nvarchar(500) NULL,
        [ContactPhone] nvarchar(16) NULL,
        [Verification] nvarchar(16) NOT NULL,
        [VerificationRequestedAtUtc] datetime2 NULL,
        [VerifiedAtUtc] datetime2 NULL,
        [VerifiedById] int NULL,
        [VerificationNote] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_BusinessProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessProfiles_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_BusinessProfiles_Users_VerifiedById] FOREIGN KEY ([VerifiedById]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Followers] (
        [FollowerId] int NOT NULL,
        [SellerId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Followers] PRIMARY KEY ([FollowerId], [SellerId]),
        CONSTRAINT [CK_Followers_NotSelf] CHECK (FollowerId <> SellerId),
        CONSTRAINT [FK_Followers_Users_FollowerId] FOREIGN KEY ([FollowerId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Followers_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Listings] (
        [Id] int NOT NULL IDENTITY,
        [SellerId] int NOT NULL,
        [CategoryId] int NOT NULL,
        [LocationId] int NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [Description] nvarchar(1500) NULL,
        [AreaText] nvarchar(120) NULL,
        [Price] bigint NULL,
        [PriceUnit] nvarchar(16) NOT NULL,
        [IsNegotiable] bit NOT NULL,
        [Quantity] decimal(12,2) NOT NULL,
        [QuantityUnit] nvarchar(16) NOT NULL,
        [MinOrderQuantity] decimal(12,2) NULL,
        [IsBulkAvailable] bit NOT NULL,
        [DeliveryAvailable] bit NOT NULL,
        [VoiceNoteUrl] nvarchar(512) NULL,
        [VoiceNoteSeconds] int NULL,
        [Status] nvarchar(16) NOT NULL,
        [ViewCount] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [PublishedAtUtc] datetime2 NULL,
        [ExpiresAtUtc] datetime2 NULL,
        [SoldAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Listings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Listings_Price] CHECK (Price IS NULL OR Price > 0),
        CONSTRAINT [CK_Listings_Quantity] CHECK (Quantity > 0),
        CONSTRAINT [FK_Listings_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Listings_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Listings_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [PurchaseRequests] (
        [Id] int NOT NULL IDENTITY,
        [BuyerId] int NOT NULL,
        [CategoryId] int NOT NULL,
        [Purpose] nvarchar(16) NULL,
        [LocationId] int NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [Quantity] decimal(12,2) NOT NULL,
        [QuantityUnit] nvarchar(16) NOT NULL,
        [Frequency] nvarchar(16) NOT NULL,
        [MinWeightKg] int NULL,
        [MaxWeightKg] int NULL,
        [TargetPrice] bigint NULL,
        [TargetPriceUnit] nvarchar(16) NULL,
        [NeededBy] date NULL,
        [DeliveryNeeded] bit NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [OfferCount] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [ClosedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_PurchaseRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PurchaseRequests_Quantity] CHECK (Quantity > 0),
        CONSTRAINT [CK_PurchaseRequests_Weight] CHECK (MinWeightKg IS NULL OR MaxWeightKg IS NULL OR MinWeightKg <= MaxWeightKg),
        CONSTRAINT [FK_PurchaseRequests_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseRequests_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseRequests_Users_BuyerId] FOREIGN KEY ([BuyerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        [ReplacedByTokenHash] nvarchar(64) NULL,
        [CreatedByIp] nvarchar(64) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [UserProfiles] (
        [UserId] int NOT NULL,
        [Bio] nvarchar(500) NULL,
        [AvatarUrl] nvarchar(512) NULL,
        [LocationId] int NULL,
        [WhatsAppNumber] nvarchar(16) NULL,
        [ShowPhoneNumber] bit NOT NULL,
        [PreferredLanguage] nvarchar(8) NOT NULL,
        [FollowerCount] int NOT NULL,
        [SoldCount] int NOT NULL,
        [RatingAverage] float NOT NULL,
        [RatingCount] int NOT NULL,
        CONSTRAINT [PK_UserProfiles] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_UserProfiles_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_UserProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Chats] (
        [Id] int NOT NULL IDENTITY,
        [BuyerId] int NOT NULL,
        [SellerId] int NOT NULL,
        [ListingId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [LastMessageAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Chats] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Chats_DifferentParties] CHECK (BuyerId <> SellerId),
        CONSTRAINT [FK_Chats_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Chats_Users_BuyerId] FOREIGN KEY ([BuyerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Chats_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [CropDetails] (
        [ListingId] int NOT NULL,
        [Variety] nvarchar(48) NULL,
        [Grade] nvarchar(24) NULL,
        [HarvestDate] date NULL,
        [PickupAvailable] bit NOT NULL,
        CONSTRAINT [PK_CropDetails] PRIMARY KEY ([ListingId]),
        CONSTRAINT [FK_CropDetails_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Favorites] (
        [UserId] int NOT NULL,
        [ListingId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Favorites] PRIMARY KEY ([UserId], [ListingId]),
        CONSTRAINT [FK_Favorites_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Favorites_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [ListingMedia] (
        [Id] int NOT NULL IDENTITY,
        [ListingId] int NOT NULL,
        [Kind] nvarchar(16) NOT NULL,
        [Path] nvarchar(256) NOT NULL,
        [ContentType] nvarchar(32) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ListingMedia] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ListingMedia_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [ListingPurposes] (
        [ListingId] int NOT NULL,
        [Purpose] nvarchar(16) NOT NULL,
        CONSTRAINT [PK_ListingPurposes] PRIMARY KEY ([ListingId], [Purpose]),
        CONSTRAINT [FK_ListingPurposes_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [LivestockDetails] (
        [ListingId] int NOT NULL,
        [Breed] nvarchar(48) NULL,
        [Gender] nvarchar(16) NOT NULL,
        [AgeMonths] int NULL,
        [Teeth] nvarchar(16) NULL,
        [LiveWeightKg] int NULL,
        [IsVaccinated] bit NULL,
        [IsPregnant] bit NULL,
        [MilkLitresPerDay] decimal(4,1) NULL,
        [HealthNotes] nvarchar(300) NULL,
        CONSTRAINT [PK_LivestockDetails] PRIMARY KEY ([ListingId]),
        CONSTRAINT [FK_LivestockDetails_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [MeatSupplyDetails] (
        [ListingId] int NOT NULL,
        [Frequency] nvarchar(16) NOT NULL,
        [PricePerKgLive] int NULL,
        CONSTRAINT [PK_MeatSupplyDetails] PRIMARY KEY ([ListingId]),
        CONSTRAINT [FK_MeatSupplyDetails_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [QurbaniBookings] (
        [Id] int NOT NULL IDENTITY,
        [ListingId] int NOT NULL,
        [BuyerId] int NOT NULL,
        [Shares] int NOT NULL,
        [Note] nvarchar(300) NULL,
        [Status] nvarchar(16) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_QurbaniBookings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_QurbaniBookings_Shares] CHECK (Shares BETWEEN 1 AND 7),
        CONSTRAINT [FK_QurbaniBookings_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_QurbaniBookings_Users_BuyerId] FOREIGN KEY ([BuyerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [QurbaniDetails] (
        [ListingId] int NOT NULL,
        [TotalShares] int NOT NULL,
        [PricePerShare] bigint NULL,
        CONSTRAINT [PK_QurbaniDetails] PRIMARY KEY ([ListingId]),
        CONSTRAINT [CK_Qurbani_Shares] CHECK (TotalShares IN (1, 7)),
        CONSTRAINT [FK_QurbaniDetails_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Reports] (
        [Id] int NOT NULL IDENTITY,
        [ListingId] int NOT NULL,
        [ReporterId] int NOT NULL,
        [Reason] nvarchar(32) NOT NULL,
        [Note] nvarchar(500) NULL,
        [Status] nvarchar(16) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ResolvedAtUtc] datetime2 NULL,
        [ResolvedById] int NULL,
        [ResolutionNote] nvarchar(500) NULL,
        CONSTRAINT [PK_Reports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reports_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Reports_Users_ReporterId] FOREIGN KEY ([ReporterId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reports_Users_ResolvedById] FOREIGN KEY ([ResolvedById]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] int NOT NULL IDENTITY,
        [SellerId] int NOT NULL,
        [ReviewerId] int NOT NULL,
        [ListingId] int NULL,
        [Rating] int NOT NULL,
        [Text] nvarchar(1000) NULL,
        [IsHidden] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Reviews_NotSelf] CHECK (ReviewerId <> SellerId),
        CONSTRAINT [CK_Reviews_Rating] CHECK (Rating BETWEEN 1 AND 5),
        CONSTRAINT [FK_Reviews_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Reviews_Users_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [SupplyDetails] (
        [ListingId] int NOT NULL,
        [Brand] nvarchar(48) NULL,
        [Condition] nvarchar(16) NOT NULL,
        CONSTRAINT [PK_SupplyDetails] PRIMARY KEY ([ListingId]),
        CONSTRAINT [FK_SupplyDetails_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Videos] (
        [Id] int NOT NULL IDENTITY,
        [SellerId] int NOT NULL,
        [ListingId] int NULL,
        [Url] nvarchar(512) NOT NULL,
        [ThumbnailUrl] nvarchar(512) NULL,
        [Caption] nvarchar(300) NULL,
        [DurationSeconds] int NOT NULL,
        [LikeCount] int NOT NULL,
        [CommentCount] int NOT NULL,
        [ShareCount] int NOT NULL,
        [ViewCount] int NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Videos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Videos_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Videos_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [RequestOffers] (
        [Id] int NOT NULL IDENTITY,
        [PurchaseRequestId] int NOT NULL,
        [SellerId] int NOT NULL,
        [ListingId] int NULL,
        [Price] bigint NULL,
        [PriceUnit] nvarchar(16) NULL,
        [Quantity] decimal(12,2) NULL,
        [Message] nvarchar(500) NULL,
        [Status] nvarchar(16) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RequestOffers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RequestOffers_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_RequestOffers_PurchaseRequests_PurchaseRequestId] FOREIGN KEY ([PurchaseRequestId]) REFERENCES [PurchaseRequests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RequestOffers_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Messages] (
        [Id] bigint NOT NULL IDENTITY,
        [ChatId] int NOT NULL,
        [SenderId] int NOT NULL,
        [Type] nvarchar(16) NOT NULL,
        [Text] nvarchar(2000) NULL,
        [MediaUrl] nvarchar(512) NULL,
        [DurationSeconds] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ReadAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Messages_Chats_ChatId] FOREIGN KEY ([ChatId]) REFERENCES [Chats] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Messages_Users_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Comments] (
        [Id] int NOT NULL IDENTITY,
        [VideoId] int NOT NULL,
        [UserId] int NOT NULL,
        [Text] nvarchar(500) NOT NULL,
        [IsDeleted] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Comments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Comments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Comments_Videos_VideoId] FOREIGN KEY ([VideoId]) REFERENCES [Videos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE TABLE [Likes] (
        [UserId] int NOT NULL,
        [VideoId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Likes] PRIMARY KEY ([UserId], [VideoId]),
        CONSTRAINT [FK_Likes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Likes_Videos_VideoId] FOREIGN KEY ([VideoId]) REFERENCES [Videos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AllowsQurbaniShares', N'DefaultPriceUnit', N'HasMilkProduction', N'IconKey', N'IsActive', N'NameEn', N'NameUr', N'ParentId', N'Section', N'Slug', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] ON;
    EXEC(N'INSERT INTO [Categories] ([Id], [AllowsQurbaniShares], [DefaultPriceUnit], [HasMilkProduction], [IconKey], [IsActive], [NameEn], [NameUr], [ParentId], [Section], [Slug], [SortOrder])
    VALUES (1, CAST(1 AS bit), N''PerItem'', CAST(1 AS bit), N''cow'', CAST(1 AS bit), N''Cow / bull'', N''گائے / بیل'', NULL, N''Livestock'', N''cow'', 1),
    (2, CAST(1 AS bit), N''PerItem'', CAST(1 AS bit), N''cow'', CAST(1 AS bit), N''Buffalo'', N''بھینس / کٹا'', NULL, N''Livestock'', N''buffalo'', 2),
    (3, CAST(0 AS bit), N''PerItem'', CAST(1 AS bit), N''sheep'', CAST(1 AS bit), N''Goat'', N''بکری / بکرا'', NULL, N''Livestock'', N''goat'', 3),
    (4, CAST(0 AS bit), N''PerItem'', CAST(0 AS bit), N''sheep'', CAST(1 AS bit), N''Sheep'', N''بھیڑ / دنبہ'', NULL, N''Livestock'', N''sheep'', 4),
    (5, CAST(1 AS bit), N''PerItem'', CAST(1 AS bit), N''horse-variant'', CAST(1 AS bit), N''Camel'', N''اونٹ'', NULL, N''Livestock'', N''camel'', 5),
    (6, CAST(0 AS bit), N''PerItem'', CAST(0 AS bit), N''paw'', CAST(1 AS bit), N''Other livestock'', N''دیگر جانور'', NULL, N''Livestock'', N''other'', 6),
    (7, CAST(0 AS bit), N''PerMaund'', CAST(0 AS bit), N''barley'', CAST(1 AS bit), N''Wheat'', N''گندم'', NULL, N''Crops'', N''wheat'', 7),
    (8, CAST(0 AS bit), N''PerMaund'', CAST(0 AS bit), N''flower-outline'', CAST(1 AS bit), N''Cotton'', N''کپاس'', NULL, N''Crops'', N''cotton'', 8),
    (9, CAST(0 AS bit), N''PerMaund'', CAST(0 AS bit), N''rice'', CAST(1 AS bit), N''Rice / paddy'', N''چاول / دھان'', NULL, N''Crops'', N''rice'', 9),
    (10, CAST(0 AS bit), N''PerMaund'', CAST(0 AS bit), N''corn'', CAST(1 AS bit), N''Maize'', N''مکئی'', NULL, N''Crops'', N''maize'', 10),
    (11, CAST(0 AS bit), N''PerMaund'', CAST(0 AS bit), N''grass'', CAST(1 AS bit), N''Sugarcane'', N''گنا'', NULL, N''Crops'', N''sugarcane'', 11),
    (12, CAST(0 AS bit), N''PerKg'', CAST(0 AS bit), N''carrot'', CAST(1 AS bit), N''Vegetables'', N''سبزیاں'', NULL, N''Crops'', N''vegetables'', 12),
    (13, CAST(0 AS bit), N''PerKg'', CAST(0 AS bit), N''fruit-cherries'', CAST(1 AS bit), N''Fruits'', N''پھل'', NULL, N''Crops'', N''fruits'', 13),
    (14, CAST(0 AS bit), N''PerMaund'', CAST(0 AS bit), N''basket-outline'', CAST(1 AS bit), N''Other produce'', N''دیگر پیداوار'', NULL, N''Crops'', N''other-produce'', 14),
    (15, CAST(0 AS bit), N''PerBag'', CAST(0 AS bit), N''seed-outline'', CAST(1 AS bit), N''Seeds'', N''بیج'', NULL, N''Supplies'', N''seeds'', 15),
    (16, CAST(0 AS bit), N''PerBag'', CAST(0 AS bit), N''sack'', CAST(1 AS bit), N''Fertilizer'', N''کھاد'', NULL, N''Supplies'', N''fertilizer'', 16),
    (17, CAST(0 AS bit), N''PerItem'', CAST(0 AS bit), N''spray'', CAST(1 AS bit), N''Pesticides'', N''زرعی ادویات'', NULL, N''Supplies'', N''pesticides'', 17),
    (18, CAST(0 AS bit), N''PerBag'', CAST(0 AS bit), N''grain'', CAST(1 AS bit), N''Animal feed and fodder'', N''چارہ اور ونڈا'', NULL, N''Supplies'', N''feed'', 18),
    (19, CAST(0 AS bit), N''PerItem'', CAST(0 AS bit), N''shovel'', CAST(1 AS bit), N''Farming tools'', N''اوزار'', NULL, N''Supplies'', N''tools'', 19),
    (20, CAST(0 AS bit), N''PerItem'', CAST(0 AS bit), N''tractor'', CAST(1 AS bit), N''Machinery'', N''مشینری'', NULL, N''Supplies'', N''machinery'', 20),
    (21, CAST(0 AS bit), N''PerItem'', CAST(0 AS bit), N''bucket-outline'', CAST(1 AS bit), N''Dairy and livestock equipment'', N''ڈیری کا سامان'', NULL, N''Supplies'', N''dairy-equipment'', 21)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AllowsQurbaniShares', N'DefaultPriceUnit', N'HasMilkProduction', N'IconKey', N'IsActive', N'NameEn', N'NameUr', N'ParentId', N'Section', N'Slug', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DistrictUr', N'IsActive', N'IsLaunchArea', N'Latitude', N'Longitude', N'NameEn', N'NameUr', N'Province', N'Slug') AND [object_id] = OBJECT_ID(N'[Locations]'))
        SET IDENTITY_INSERT [Locations] ON;
    EXEC(N'INSERT INTO [Locations] ([Id], [DistrictUr], [IsActive], [IsLaunchArea], [Latitude], [Longitude], [NameEn], [NameUr], [Province], [Slug])
    VALUES (1, N''لودھراں'', CAST(1 AS bit), CAST(1 AS bit), 29.533899999999999E0, 71.632400000000004E0, N''Lodhran'', N''لودھراں'', N''Punjab'', N''lodhran''),
    (2, N''لودھراں'', CAST(1 AS bit), CAST(1 AS bit), 29.802E0, 71.742999999999995E0, N''Dunyapur'', N''دنیا پور'', N''Punjab'', N''dunyapur''),
    (3, N''لودھراں'', CAST(1 AS bit), CAST(1 AS bit), 29.623699999999999E0, 71.916499999999999E0, N''Kahror Pakka'', N''کہروڑ پکا'', N''Punjab'', N''kahror''),
    (4, N''ملتان'', CAST(1 AS bit), CAST(0 AS bit), 29.504999999999999E0, 71.221000000000004E0, N''Jalalpur Pirwala'', N''جلال پور پیر والا'', N''Punjab'', N''jalalpur''),
    (5, N''ملتان'', CAST(1 AS bit), CAST(0 AS bit), 30.157499999999999E0, 71.524900000000002E0, N''Multan'', N''ملتان'', N''Punjab'', N''multan''),
    (6, N''ملتان'', CAST(1 AS bit), CAST(0 AS bit), 29.879000000000001E0, 71.295000000000002E0, N''Shujabad'', N''شجاع آباد'', N''Punjab'', N''shujabad''),
    (7, N''بہاولپور'', CAST(1 AS bit), CAST(0 AS bit), 29.395600000000002E0, 71.683599999999998E0, N''Bahawalpur'', N''بہاولپور'', N''Punjab'', N''bahawalpur''),
    (8, N''خانیوال'', CAST(1 AS bit), CAST(0 AS bit), 30.3017E0, 71.932100000000005E0, N''Khanewal'', N''خانیوال'', N''Punjab'', N''khanewal''),
    (9, N''وہاڑی'', CAST(1 AS bit), CAST(0 AS bit), 30.045200000000001E0, 72.3489E0, N''Vehari'', N''وہاڑی'', N''Punjab'', N''vehari''),
    (10, N''وہاڑی'', CAST(1 AS bit), CAST(0 AS bit), 29.800000000000001E0, 72.174999999999997E0, N''Mailsi'', N''میلسی'', N''Punjab'', N''mailsi'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DistrictUr', N'IsActive', N'IsLaunchArea', N'Latitude', N'Longitude', N'NameEn', N'NameUr', N'Province', N'Slug') AND [object_id] = OBJECT_ID(N'[Locations]'))
        SET IDENTITY_INSERT [Locations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CategoryId', N'Purpose') AND [object_id] = OBJECT_ID(N'[CategoryPurposes]'))
        SET IDENTITY_INSERT [CategoryPurposes] ON;
    EXEC(N'INSERT INTO [CategoryPurposes] ([CategoryId], [Purpose])
    VALUES (1, N''Breeding''),
    (1, N''Dairy''),
    (1, N''General''),
    (1, N''MeatSupply''),
    (1, N''Qurbani''),
    (2, N''Breeding''),
    (2, N''Dairy''),
    (2, N''General''),
    (2, N''MeatSupply''),
    (2, N''Qurbani''),
    (3, N''Breeding''),
    (3, N''Dairy''),
    (3, N''General''),
    (3, N''MeatSupply''),
    (3, N''Qurbani''),
    (4, N''Breeding''),
    (4, N''General''),
    (4, N''MeatSupply''),
    (4, N''Qurbani''),
    (5, N''Breeding''),
    (5, N''Dairy''),
    (5, N''General''),
    (5, N''MeatSupply''),
    (5, N''Qurbani''),
    (6, N''Breeding''),
    (6, N''General'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CategoryId', N'Purpose') AND [object_id] = OBJECT_ID(N'[CategoryPurposes]'))
        SET IDENTITY_INSERT [CategoryPurposes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_BusinessProfiles_LocationId] ON [BusinessProfiles] ([LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessProfiles_UserId] ON [BusinessProfiles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_BusinessProfiles_Verification_VerificationRequestedAtUtc] ON [BusinessProfiles] ([Verification], [VerificationRequestedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_BusinessProfiles_VerifiedById] ON [BusinessProfiles] ([VerifiedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Categories_ParentId] ON [Categories] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Categories_Section_SortOrder] ON [Categories] ([Section], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Chats_BuyerId_LastMessageAtUtc] ON [Chats] ([BuyerId], [LastMessageAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Chats_BuyerId_SellerId_ListingId] ON [Chats] ([BuyerId], [SellerId], [ListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Chats_ListingId] ON [Chats] ([ListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Chats_SellerId_LastMessageAtUtc] ON [Chats] ([SellerId], [LastMessageAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Comments_UserId] ON [Comments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Comments_VideoId_CreatedAtUtc] ON [Comments] ([VideoId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Favorites_ListingId] ON [Favorites] ([ListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Favorites_UserId_CreatedAtUtc] ON [Favorites] ([UserId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Followers_SellerId] ON [Followers] ([SellerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Likes_VideoId] ON [Likes] ([VideoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_ListingMedia_ListingId_SortOrder] ON [ListingMedia] ([ListingId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_ListingPurposes_Purpose_ListingId] ON [ListingPurposes] ([Purpose], [ListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Listings_CategoryId] ON [Listings] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Listings_LocationId] ON [Listings] ([LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Listings_SellerId_Status] ON [Listings] ([SellerId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Listings_Status_CategoryId_PublishedAtUtc] ON [Listings] ([Status], [CategoryId], [PublishedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Listings_Status_ExpiresAtUtc] ON [Listings] ([Status], [ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Listings_Status_LocationId_PublishedAtUtc] ON [Listings] ([Status], [LocationId], [PublishedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Listings_Status_Price] ON [Listings] ([Status], [Price]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_LivestockDetails_Breed] ON [LivestockDetails] ([Breed]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_LivestockDetails_Gender_Teeth] ON [LivestockDetails] ([Gender], [Teeth]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_LivestockDetails_LiveWeightKg] ON [LivestockDetails] ([LiveWeightKg]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Locations_Slug] ON [Locations] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Messages_ChatId_CreatedAtUtc] ON [Messages] ([ChatId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Messages_SenderId] ON [Messages] ([SenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequests_BuyerId_CreatedAtUtc] ON [PurchaseRequests] ([BuyerId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequests_CategoryId] ON [PurchaseRequests] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequests_LocationId] ON [PurchaseRequests] ([LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequests_Status_CategoryId] ON [PurchaseRequests] ([Status], [CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_PurchaseRequests_Status_ExpiresAtUtc_CreatedAtUtc] ON [PurchaseRequests] ([Status], [ExpiresAtUtc], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_QurbaniBookings_BuyerId_CreatedAtUtc] ON [QurbaniBookings] ([BuyerId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_QurbaniBookings_ListingId_Status] ON [QurbaniBookings] ([ListingId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId_ExpiresAtUtc] ON [RefreshTokens] ([UserId], [ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Reports_ListingId_ReporterId] ON [Reports] ([ListingId], [ReporterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Reports_ReporterId] ON [Reports] ([ReporterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Reports_ResolvedById] ON [Reports] ([ResolvedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Reports_Status_CreatedAtUtc] ON [Reports] ([Status], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_RequestOffers_ListingId] ON [RequestOffers] ([ListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RequestOffers_PurchaseRequestId_SellerId] ON [RequestOffers] ([PurchaseRequestId], [SellerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_RequestOffers_SellerId_UpdatedAtUtc] ON [RequestOffers] ([SellerId], [UpdatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Reviews_ListingId] ON [Reviews] ([ListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Reviews_ReviewerId_SellerId_ListingId] ON [Reviews] ([ReviewerId], [SellerId], [ListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Reviews_SellerId_CreatedAtUtc] ON [Reviews] ([SellerId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_UserProfiles_LocationId] ON [UserProfiles] ([LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Users_CreatedAtUtc] ON [Users] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_PhoneNumber] ON [Users] ([PhoneNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Videos_ListingId] ON [Videos] ([ListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Videos_SellerId_CreatedAtUtc] ON [Videos] ([SellerId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    CREATE INDEX [IX_Videos_Status_CreatedAtUtc] ON [Videos] ([Status], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112140701_InitialSqlServer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112140701_InitialSqlServer', N'10.0.14');
END;

COMMIT;
GO

