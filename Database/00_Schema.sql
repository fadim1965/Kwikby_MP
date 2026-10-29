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
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [Actor] nvarchar(256) NOT NULL,
        [Action] nvarchar(80) NOT NULL,
        [EntityType] nvarchar(40) NOT NULL,
        [EntityId] int NULL,
        [Summary] nvarchar(500) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [CarrierRemittances] (
        [Id] int NOT NULL IDENTITY,
        [Carrier] nvarchar(40) NOT NULL,
        [Reference] nvarchar(80) NOT NULL,
        [BankReference] nvarchar(120) NULL,
        [RemittanceDateUtc] datetime2 NOT NULL,
        [ReceivedUtc] datetime2 NOT NULL,
        [ReceivedBy] nvarchar(256) NULL,
        [Notes] nvarchar(500) NULL,
        [Status] int NOT NULL,
        [ExpectedAmount] decimal(18,2) NOT NULL,
        [ActualAmount] decimal(18,2) NOT NULL,
        [Variance] decimal(18,2) NOT NULL,
        [Tolerance] decimal(18,2) NOT NULL,
        [ReconciledUtc] datetime2 NULL,
        [ReconciledBy] nvarchar(256) NULL,
        [ClosedUtc] datetime2 NULL,
        [ClosedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_CarrierRemittances] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [EmailMessages] (
        [Id] int NOT NULL IDENTITY,
        [ToAddress] nvarchar(256) NOT NULL,
        [ToName] nvarchar(120) NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Kind] nvarchar(60) NOT NULL,
        [BodyHtml] nvarchar(max) NOT NULL,
        [BodyText] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [Attempts] int NOT NULL,
        [LastError] nvarchar(1000) NULL,
        [DedupeKey] nvarchar(120) NULL,
        [UserId] nvarchar(450) NULL,
        [OrderId] int NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [SentUtc] datetime2 NULL,
        [NextAttemptUtc] datetime2 NULL,
        CONSTRAINT [PK_EmailMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Log] (
        [Id] bigint NOT NULL IDENTITY,
        [Application] nvarchar(100) NOT NULL,
        [Logged] datetime2 NOT NULL,
        [Level] nvarchar(100) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [Logger] nvarchar(500) NULL,
        [Callsite] nvarchar(max) NULL,
        [Exception] nvarchar(max) NULL,
        CONSTRAINT [PK_Log] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [SiteSettings] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(64) NOT NULL,
        [Value] nvarchar(512) NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(128) NULL,
        CONSTRAINT [PK_SiteSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [TaxClasses] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(450) NOT NULL,
        [Rate] decimal(5,4) NOT NULL,
        [IsActive] bit NOT NULL,
        [IsExempt] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TaxClasses] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Translations] (
        [Id] int NOT NULL IDENTITY,
        [Culture] nvarchar(8) NOT NULL,
        [Key] nvarchar(128) NOT NULL,
        [Value] nvarchar(2000) NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(128) NULL,
        CONSTRAINT [PK_Translations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] nvarchar(450) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [NormalizedEmail] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(120) NOT NULL,
        [Phone] nvarchar(40) NULL,
        [PasswordHash] nvarchar(400) NOT NULL,
        [SecurityStamp] nvarchar(64) NOT NULL,
        [IsActive] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        [LockoutEndUtc] datetime2 NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [LastLoginUtc] datetime2 NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [TwoFactorSecret] nvarchar(64) NULL,
        [TwoFactorRecoveryCodes] nvarchar(max) NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Addresses] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [FullName] nvarchar(120) NOT NULL,
        [Line1] nvarchar(200) NOT NULL,
        [Line2] nvarchar(200) NULL,
        [City] nvarchar(100) NOT NULL,
        [State] nvarchar(100) NULL,
        [PostalCode] nvarchar(20) NOT NULL,
        [Country] nvarchar(80) NOT NULL,
        [Phone] nvarchar(40) NULL,
        [IsDefault] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Addresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Addresses_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Carts] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NULL,
        [AnonymousId] nvarchar(64) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Carts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Shops] (
        [Id] int NOT NULL IDENTITY,
        [OwnerUserId] nvarchar(450) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Slug] nvarchar(120) NOT NULL,
        [Description] nvarchar(600) NULL,
        [Tagline] nvarchar(160) NULL,
        [Status] int NOT NULL,
        [StatusReason] nvarchar(500) NULL,
        [ContactName] nvarchar(120) NULL,
        [Phone] nvarchar(40) NULL,
        [Email] nvarchar(256) NULL,
        [Website] nvarchar(200) NULL,
        [AddressLine1] nvarchar(200) NULL,
        [City] nvarchar(100) NULL,
        [PostalCode] nvarchar(40) NULL,
        [Country] nvarchar(80) NULL,
        [PickupLocation] nvarchar(120) NULL,
        [CommissionRate] decimal(5,4) NOT NULL,
        [FlatShippingRate] decimal(18,2) NULL,
        [FreeShippingThreshold] decimal(18,2) NULL,
        [Currency] nvarchar(8) NOT NULL,
        [BankAccountName] nvarchar(120) NULL,
        [BankName] nvarchar(120) NULL,
        [BankIban] nvarchar(40) NULL,
        [ThemeKey] nvarchar(40) NOT NULL,
        [LogoUrl] nvarchar(500) NULL,
        [BannerUrl] nvarchar(500) NULL,
        [IsFeatured] bit NOT NULL,
        [BusinessRegistrationNumber] nvarchar(80) NULL,
        [TaxId] nvarchar(80) NULL,
        [GovernmentIdPath] nvarchar(500) NULL,
        [ProofOfAddressPath] nvarchar(500) NULL,
        [BankStatementPath] nvarchar(500) NULL,
        [VerificationStatus] int NOT NULL,
        [VerificationReason] nvarchar(500) NULL,
        [VerifiedUtc] datetime2 NULL,
        [RejectedUtc] datetime2 NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [ApprovedUtc] datetime2 NULL,
        [SuspendedUtc] datetime2 NULL,
        CONSTRAINT [PK_Shops] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Shops_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [RoleName] nvarchar(40) NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [ShopId] int NULL,
        [IsGlobal] bit NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Slug] nvarchar(80) NOT NULL,
        [Description] nvarchar(500) NULL,
        [ParentId] int NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CommissionRate] decimal(18,2) NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Categories_Categories_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Categories_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Coupons] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(64) NOT NULL,
        [Description] nvarchar(256) NULL,
        [Type] int NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [MinSubtotal] decimal(18,2) NOT NULL,
        [MaxUses] int NULL,
        [MaxUsesPerUser] int NULL,
        [UsedCount] int NOT NULL,
        [StartsUtc] datetime2 NULL,
        [ExpiresUtc] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [AppliesToAll] bit NOT NULL,
        [ShopId] int NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Coupons] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Coupons_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [ShopUsers] (
        [Id] int NOT NULL IDENTITY,
        [ShopId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Role] int NOT NULL,
        [AcceptedTermsUtc] datetime2 NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ShopUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShopUsers_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ShopUsers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [VendorPayouts] (
        [Id] int NOT NULL IDENTITY,
        [ShopId] int NOT NULL,
        [Reference] nvarchar(40) NOT NULL,
        [PeriodStartUtc] datetime2 NOT NULL,
        [PeriodEndUtc] datetime2 NOT NULL,
        [EntryCount] int NOT NULL,
        [GrossAmount] decimal(18,2) NOT NULL,
        [CommissionAmount] decimal(18,2) NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [Method] nvarchar(40) NOT NULL,
        [Note] nvarchar(200) NULL,
        [CreatedBy] nvarchar(256) NULL,
        [Status] int NOT NULL,
        [FailureReason] nvarchar(500) NULL,
        [HandledBy] nvarchar(256) NULL,
        [FailedUtc] datetime2 NULL,
        [ReversedUtc] datetime2 NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorPayouts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorPayouts_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [ShopId] int NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Slug] nvarchar(160) NOT NULL,
        [ShortDescription] nvarchar(200) NULL,
        [Description] nvarchar(max) NULL,
        [Brand] nvarchar(120) NULL,
        [CategoryId] int NOT NULL,
        [TaxClassId] int NULL,
        [IsPublished] bit NOT NULL,
        [ModerationStatus] int NOT NULL,
        [ModerationReason] nvarchar(500) NULL,
        [ModeratedUtc] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [IsFeatured] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Products_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Products_TaxClasses_TaxClassId] FOREIGN KEY ([TaxClassId]) REFERENCES [TaxClasses] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [ShopId] int NOT NULL,
        [OrderNumber] nvarchar(32) NOT NULL,
        [UserId] nvarchar(450) NULL,
        [Email] nvarchar(256) NOT NULL,
        [CustomerName] nvarchar(120) NOT NULL,
        [Phone] nvarchar(40) NULL,
        [Status] int NOT NULL,
        [PaymentMethod] int NOT NULL,
        [CollectionStatus] int NOT NULL,
        [CodAmount] decimal(18,2) NOT NULL,
        [IdempotencyKey] nvarchar(64) NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [ShippingTotal] decimal(18,2) NOT NULL,
        [TaxTotal] decimal(18,2) NOT NULL,
        [DiscountTotal] decimal(18,2) NOT NULL,
        [GrandTotal] decimal(18,2) NOT NULL,
        [RefundedTotal] decimal(18,2) NOT NULL,
        [CouponCode] nvarchar(64) NULL,
        [CouponId] int NULL,
        [ShippingName] nvarchar(120) NOT NULL,
        [ShippingLine1] nvarchar(200) NOT NULL,
        [ShippingLine2] nvarchar(200) NULL,
        [ShippingCity] nvarchar(100) NOT NULL,
        [ShippingState] nvarchar(100) NULL,
        [ShippingPostalCode] nvarchar(20) NOT NULL,
        [ShippingCountry] nvarchar(80) NOT NULL,
        [CustomerNote] nvarchar(1000) NULL,
        [PlacedUtc] datetime2 NOT NULL,
        [PaidUtc] datetime2 NULL,
        [ShippedUtc] datetime2 NULL,
        [DeliveredUtc] datetime2 NULL,
        [CancelledUtc] datetime2 NULL,
        [TrackingNumber] nvarchar(64) NULL,
        [Carrier] nvarchar(40) NULL,
        [CarrierShipmentId] nvarchar(120) NULL,
        [LabelUrl] nvarchar(max) NULL,
        [CarrierError] nvarchar(500) NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Orders_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Orders_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [ProductImages] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [Url] nvarchar(max) NOT NULL,
        [AltText] nvarchar(200) NULL,
        [DisplayOrder] int NOT NULL,
        [IsPrimary] bit NOT NULL,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [ProductVariants] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [Sku] nvarchar(64) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [CompareAtPrice] decimal(18,2) NULL,
        [StockQuantity] int NOT NULL,
        [IsActive] bit NOT NULL,
        [LowStockThreshold] int NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductVariants_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [ShopId] int NOT NULL,
        [UserId] nvarchar(450) NULL,
        [ReviewerName] nvarchar(120) NOT NULL,
        [Rating] int NOT NULL,
        [Title] nvarchar(140) NULL,
        [Body] nvarchar(4000) NULL,
        [IsVerifiedPurchase] bit NOT NULL,
        [Status] int NOT NULL,
        [HelpfulCount] int NOT NULL,
        [NotHelpfulCount] int NOT NULL,
        [VendorReply] nvarchar(2000) NULL,
        [VendorReplyUtc] datetime2 NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [PublishedUtc] datetime2 NULL,
        [UpdatedUtc] datetime2 NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reviews_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [WishlistItems] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ProductId] int NOT NULL,
        [AddedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_WishlistItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WishlistItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_WishlistItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [CarrierRemittanceLines] (
        [Id] int NOT NULL IDENTITY,
        [CarrierRemittanceId] int NOT NULL,
        [OrderId] int NULL,
        [TrackingNumber] nvarchar(120) NULL,
        [ExpectedAmount] decimal(18,2) NOT NULL,
        [ActualAmount] decimal(18,2) NOT NULL,
        [Variance] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [Note] nvarchar(300) NULL,
        [SettledUtc] datetime2 NULL,
        CONSTRAINT [PK_CarrierRemittanceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CarrierRemittanceLines_CarrierRemittances_CarrierRemittanceId] FOREIGN KEY ([CarrierRemittanceId]) REFERENCES [CarrierRemittances] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CarrierRemittanceLines_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Conversations] (
        [Id] int NOT NULL IDENTITY,
        [Reference] nvarchar(32) NOT NULL,
        [ShopId] int NOT NULL,
        [OrderId] int NULL,
        [UserId] nvarchar(450) NULL,
        [CustomerEmail] nvarchar(256) NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Status] int NOT NULL,
        [LastMessagePreview] nvarchar(300) NOT NULL,
        [LastAuthor] int NOT NULL,
        [LastMessageUtc] datetime2 NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [ClosedUtc] datetime2 NULL,
        [CustomerLastReadUtc] datetime2 NULL,
        [VendorLastReadUtc] datetime2 NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conversations_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Conversations_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Conversations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [CouponUsages] (
        [Id] int NOT NULL IDENTITY,
        [CouponId] int NOT NULL,
        [UserId] nvarchar(450) NULL,
        [OrderId] int NULL,
        [RedeemedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CouponUsages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CouponUsages_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CouponUsages_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_CouponUsages_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [OrderShipments] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ShopId] int NOT NULL,
        [Carrier] nvarchar(40) NOT NULL,
        [CarrierShipmentId] nvarchar(120) NULL,
        [TrackingNumber] nvarchar(120) NULL,
        [LabelUrl] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [WeightGrams] int NOT NULL,
        [ShippingCost] decimal(18,2) NOT NULL,
        [CodAmount] decimal(18,2) NOT NULL,
        [RecipientName] nvarchar(120) NULL,
        [RecipientPhone] nvarchar(40) NULL,
        [DeliveryAddress] nvarchar(400) NULL,
        [PickupLocation] nvarchar(120) NULL,
        [LastError] nvarchar(600) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [DispatchedUtc] datetime2 NULL,
        [DeliveredUtc] datetime2 NULL,
        CONSTRAINT [PK_OrderShipments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderShipments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderShipments_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [PaymentEvents] (
        [Id] int NOT NULL IDENTITY,
        [Provider] nvarchar(40) NOT NULL,
        [EventId] nvarchar(120) NOT NULL,
        [Type] nvarchar(60) NOT NULL,
        [OrderId] int NULL,
        [OrderNumber] nvarchar(32) NULL,
        [TransactionId] nvarchar(120) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(8) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [SignatureValid] bit NOT NULL,
        [Outcome] nvarchar(40) NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentEvents_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [Provider] nvarchar(40) NOT NULL,
        [TransactionId] nvarchar(120) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(8) NOT NULL,
        [Status] int NOT NULL,
        [FailureReason] nvarchar(500) NULL,
        [IsTest] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [CompletedUtc] datetime2 NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [ReturnRequests] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ShopId] int NOT NULL,
        [Reference] nvarchar(32) NOT NULL,
        [Status] int NOT NULL,
        [UserId] nvarchar(450) NULL,
        [Email] nvarchar(256) NOT NULL,
        [CustomerNote] nvarchar(1000) NULL,
        [DecisionNote] nvarchar(1000) NULL,
        [HandledBy] nvarchar(256) NULL,
        [RefundAmount] decimal(18,2) NOT NULL,
        [RequestedUtc] datetime2 NOT NULL,
        [DecidedUtc] datetime2 NULL,
        [ReceivedUtc] datetime2 NULL,
        [RefundedUtc] datetime2 NULL,
        CONSTRAINT [PK_ReturnRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReturnRequests_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReturnRequests_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReturnRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [SellerFulfilments] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ShopId] int NOT NULL,
        [PackageCount] int NOT NULL,
        [HasNylonPockets] bit NOT NULL,
        [InvoiceAttached] bit NOT NULL,
        [LabelsGenerated] int NOT NULL,
        [SellerConfirmedUtc] datetime2 NULL,
        [LabelsGeneratedUtc] datetime2 NULL,
        [PickedUpByShipperUtc] datetime2 NULL,
        [Status] int NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SellerFulfilments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SellerFulfilments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SellerFulfilments_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [CartItems] (
        [Id] int NOT NULL IDENTITY,
        [CartId] int NOT NULL,
        [ProductVariantId] int NOT NULL,
        [Quantity] int NOT NULL,
        [AddedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CartItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CartItems_Carts_CartId] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CartItems_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [OrderLines] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ProductVariantId] int NULL,
        [ProductId] int NULL,
        [ProductName] nvarchar(160) NOT NULL,
        [VariantName] nvarchar(80) NOT NULL,
        [Sku] nvarchar(64) NOT NULL,
        [ImageUrl] nvarchar(max) NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderLines_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderLines_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [ShopListings] (
        [Id] int NOT NULL IDENTITY,
        [ShopId] int NOT NULL,
        [ProductId] int NOT NULL,
        [ProductVariantId] int NULL,
        [Price] decimal(18,2) NULL,
        [CompareAtPrice] decimal(18,2) NULL,
        [StockQuantity] int NOT NULL,
        [LowStockThreshold] int NOT NULL,
        [IsActive] bit NOT NULL,
        [Sku] nvarchar(64) NULL,
        [Note] nvarchar(500) NULL,
        [RowVersion] varbinary(max) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ShopListings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShopListings_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ShopListings_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShopListings_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [StockMovements] (
        [Id] int NOT NULL IDENTITY,
        [ProductVariantId] int NOT NULL,
        [QuantityDelta] int NOT NULL,
        [StockAfter] int NOT NULL,
        [Reason] int NOT NULL,
        [Note] nvarchar(200) NULL,
        [OrderId] int NULL,
        [CreatedBy] nvarchar(100) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_StockMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockMovements_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [ReviewVotes] (
        [Id] int NOT NULL IDENTITY,
        [ReviewId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [IsHelpful] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ReviewVotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewVotes_Reviews_ReviewId] FOREIGN KEY ([ReviewId]) REFERENCES [Reviews] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ReviewVotes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [ConversationMessages] (
        [Id] int NOT NULL IDENTITY,
        [ConversationId] int NOT NULL,
        [Author] int NOT NULL,
        [SenderName] nvarchar(120) NOT NULL,
        [SenderUserId] nvarchar(max) NULL,
        [Body] nvarchar(4000) NOT NULL,
        [SentUtc] datetime2 NOT NULL,
        [ResponseUtc] datetime2 NULL,
        CONSTRAINT [PK_ConversationMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConversationMessages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [ShipmentEvents] (
        [Id] int NOT NULL IDENTITY,
        [ShipmentId] int NOT NULL,
        [Status] int NOT NULL,
        [Location] nvarchar(80) NULL,
        [Description] nvarchar(300) NULL,
        [OccurredUtc] datetime2 NOT NULL,
        [RecordedUtc] datetime2 NOT NULL,
        [FromCarrier] bit NOT NULL,
        CONSTRAINT [PK_ShipmentEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShipmentEvents_OrderShipments_ShipmentId] FOREIGN KEY ([ShipmentId]) REFERENCES [OrderShipments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [PostSaleRequests] (
        [Id] int NOT NULL IDENTITY,
        [Reference] nvarchar(32) NOT NULL,
        [OrderId] int NOT NULL,
        [OrderLineId] int NULL,
        [ShopId] int NOT NULL,
        [CustomerId] nvarchar(450) NOT NULL,
        [Type] int NOT NULL,
        [Reason] int NOT NULL,
        [ReasonDetail] nvarchar(1000) NULL,
        [Status] int NOT NULL,
        [IsOrderLevel] bit NOT NULL,
        [ItemPriceBearer] int NOT NULL,
        [ShippingFeeBearer] int NOT NULL,
        [CreditCardFeeBearer] int NOT NULL,
        [PickupFeeBearer] int NOT NULL,
        [ReturnFeeBearer] int NOT NULL,
        [ReplacementFeeBearer] int NOT NULL,
        [RefundAmount] decimal(18,2) NOT NULL,
        [IsPromoCodeRefund] bit NOT NULL,
        [PromoCode] nvarchar(64) NULL,
        [ReplacementVariantId] int NULL,
        [SerialNumber] nvarchar(100) NULL,
        [PurchaseDateUtc] datetime2 NULL,
        [GracePeriodEndUtc] datetime2 NULL,
        [WithinGracePeriod] bit NOT NULL,
        [CanBeRepaired] bit NOT NULL,
        [CanBeReplaced] bit NOT NULL,
        [Carrier] nvarchar(40) NULL,
        [CallAttempts] int NOT NULL,
        [ManifestNumber] nvarchar(50) NULL,
        [QualityReport] nvarchar(2000) NULL,
        [RedeliveryAttempted] bit NOT NULL,
        [PickupTrackingNumber] nvarchar(100) NULL,
        [PickupScheduledUtc] datetime2 NULL,
        [PickedUpUtc] datetime2 NULL,
        [ReceivedAtWarehouseUtc] datetime2 NULL,
        [AdminNotes] nvarchar(2000) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        [ProcessedUtc] datetime2 NULL,
        [ClosedUtc] datetime2 NULL,
        [ProcessedBy] nvarchar(256) NULL,
        [ClosedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_PostSaleRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PostSaleRequests_OrderLines_OrderLineId] FOREIGN KEY ([OrderLineId]) REFERENCES [OrderLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PostSaleRequests_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PostSaleRequests_ProductVariants_ReplacementVariantId] FOREIGN KEY ([ReplacementVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_PostSaleRequests_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PostSaleRequests_Users_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [ReturnLines] (
        [Id] int NOT NULL IDENTITY,
        [ReturnRequestId] int NOT NULL,
        [OrderLineId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_ReturnLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReturnLines_OrderLines_OrderLineId] FOREIGN KEY ([OrderLineId]) REFERENCES [OrderLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReturnLines_ReturnRequests_ReturnRequestId] FOREIGN KEY ([ReturnRequestId]) REFERENCES [ReturnRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [PostSaleMessages] (
        [Id] int NOT NULL IDENTITY,
        [PostSaleRequestId] int NOT NULL,
        [SenderId] nvarchar(64) NOT NULL,
        [SenderName] nvarchar(256) NOT NULL,
        [IsFromCustomer] bit NOT NULL,
        [Content] nvarchar(2000) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PostSaleMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PostSaleMessages_PostSaleRequests_PostSaleRequestId] FOREIGN KEY ([PostSaleRequestId]) REFERENCES [PostSaleRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [PostSaleStatusHistory] (
        [Id] int NOT NULL IDENTITY,
        [PostSaleRequestId] int NOT NULL,
        [FromStatus] int NOT NULL,
        [ToStatus] int NOT NULL,
        [ChangedBy] nvarchar(256) NULL,
        [Note] nvarchar(1000) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PostSaleStatusHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PostSaleStatusHistory_PostSaleRequests_PostSaleRequestId] FOREIGN KEY ([PostSaleRequestId]) REFERENCES [PostSaleRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE TABLE [VendorLedgerEntries] (
        [Id] int NOT NULL IDENTITY,
        [ShopId] int NOT NULL,
        [OrderId] int NULL,
        [Type] int NOT NULL,
        [GrossAmount] decimal(18,2) NOT NULL,
        [CommissionAmount] decimal(18,2) NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [Note] nvarchar(200) NULL,
        [SettledUtc] datetime2 NULL,
        [PayoutReference] nvarchar(120) NULL,
        [ReturnRequestId] int NULL,
        [PostSaleRequestId] int NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorLedgerEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorLedgerEntries_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_VendorLedgerEntries_PostSaleRequests_PostSaleRequestId] FOREIGN KEY ([PostSaleRequestId]) REFERENCES [PostSaleRequests] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_VendorLedgerEntries_ReturnRequests_ReturnRequestId] FOREIGN KEY ([ReturnRequestId]) REFERENCES [ReturnRequests] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_VendorLedgerEntries_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Addresses_UserId] ON [Addresses] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Actor] ON [AuditLogs] ([Actor]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedUtc] ON [AuditLogs] ([CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [AuditLogs] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CarrierRemittanceLines_CarrierRemittanceId] ON [CarrierRemittanceLines] ([CarrierRemittanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CarrierRemittanceLines_OrderId] ON [CarrierRemittanceLines] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CartItems_CartId] ON [CartItems] ([CartId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CartItems_CartId_ProductVariantId] ON [CartItems] ([CartId], [ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CartItems_ProductVariantId] ON [CartItems] ([ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Carts_AnonymousId] ON [Carts] ([AnonymousId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Carts_UserId] ON [Carts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Categories_IsGlobal] ON [Categories] ([IsGlobal]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Categories_ParentId] ON [Categories] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Categories_ShopId_Slug] ON [Categories] ([ShopId], [Slug]) WHERE [ShopId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ConversationMessages_ConversationId] ON [ConversationMessages] ([ConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ConversationMessages_SentUtc] ON [ConversationMessages] ([SentUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Conversations_LastMessageUtc] ON [Conversations] ([LastMessageUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Conversations_OrderId] ON [Conversations] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Conversations_Reference] ON [Conversations] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Conversations_ShopId] ON [Conversations] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Conversations_Status] ON [Conversations] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Conversations_UserId] ON [Conversations] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Coupons_Code] ON [Coupons] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Coupons_IsActive] ON [Coupons] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Coupons_ShopId] ON [Coupons] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CouponUsages_CouponId] ON [CouponUsages] ([CouponId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CouponUsages_OrderId] ON [CouponUsages] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CouponUsages_UserId] ON [CouponUsages] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EmailMessages_CreatedUtc] ON [EmailMessages] ([CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EmailMessages_DedupeKey] ON [EmailMessages] ([DedupeKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EmailMessages_Kind] ON [EmailMessages] ([Kind]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EmailMessages_OrderId] ON [EmailMessages] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EmailMessages_Status] ON [EmailMessages] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EmailMessages_UserId] ON [EmailMessages] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Log_Logged] ON [Log] ([Logged]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderLines_OrderId] ON [OrderLines] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderLines_ProductVariantId] ON [OrderLines] ([ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_CouponId] ON [Orders] ([CouponId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Orders_IdempotencyKey] ON [Orders] ([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_OrderNumber] ON [Orders] ([OrderNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_ShopId] ON [Orders] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_Status] ON [Orders] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderShipments_OrderId] ON [OrderShipments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderShipments_ShopId] ON [OrderShipments] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderShipments_TrackingNumber] ON [OrderShipments] ([TrackingNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PaymentEvents_CreatedUtc] ON [PaymentEvents] ([CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PaymentEvents_OrderId] ON [PaymentEvents] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentEvents_Provider_EventId] ON [PaymentEvents] ([Provider], [EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Payments_OrderId] ON [Payments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PostSaleMessages_PostSaleRequestId] ON [PostSaleMessages] ([PostSaleRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PostSaleRequests_CustomerId] ON [PostSaleRequests] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PostSaleRequests_OrderId] ON [PostSaleRequests] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PostSaleRequests_OrderLineId] ON [PostSaleRequests] ([OrderLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PostSaleRequests_Reference] ON [PostSaleRequests] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PostSaleRequests_ReplacementVariantId] ON [PostSaleRequests] ([ReplacementVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PostSaleRequests_ShopId] ON [PostSaleRequests] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PostSaleRequests_Status] ON [PostSaleRequests] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PostSaleRequests_Type] ON [PostSaleRequests] ([Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PostSaleStatusHistory_PostSaleRequestId] ON [PostSaleStatusHistory] ([PostSaleRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Products_IsDeleted] ON [Products] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Products_IsPublished] ON [Products] ([IsPublished]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Products_ModerationStatus] ON [Products] ([ModerationStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Products_ShopId] ON [Products] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_ShopId_Slug] ON [Products] ([ShopId], [Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Products_TaxClassId] ON [Products] ([TaxClassId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductVariants_ProductId] ON [ProductVariants] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductVariants_Sku] ON [ProductVariants] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReturnLines_OrderLineId] ON [ReturnLines] ([OrderLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReturnLines_ReturnRequestId] ON [ReturnLines] ([ReturnRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReturnRequests_OrderId] ON [ReturnRequests] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReturnRequests_Reference] ON [ReturnRequests] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReturnRequests_ShopId] ON [ReturnRequests] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReturnRequests_Status] ON [ReturnRequests] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReturnRequests_UserId] ON [ReturnRequests] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reviews_CreatedUtc] ON [Reviews] ([CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reviews_ProductId] ON [Reviews] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reviews_ProductId_Status] ON [Reviews] ([ProductId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reviews_ShopId] ON [Reviews] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reviews_Status] ON [Reviews] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reviews_UserId] ON [Reviews] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReviewVotes_ReviewId] ON [ReviewVotes] ([ReviewId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReviewVotes_ReviewId_UserId] ON [ReviewVotes] ([ReviewId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReviewVotes_UserId] ON [ReviewVotes] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SellerFulfilments_OrderId] ON [SellerFulfilments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SellerFulfilments_ShopId] ON [SellerFulfilments] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SellerFulfilments_Status] ON [SellerFulfilments] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ShipmentEvents_ShipmentId] ON [ShipmentEvents] ([ShipmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ShopListings_IsActive] ON [ShopListings] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ShopListings_ProductId] ON [ShopListings] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ShopListings_ProductVariantId] ON [ShopListings] ([ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShopListings_ShopId_ProductId] ON [ShopListings] ([ShopId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Shops_OwnerUserId] ON [Shops] ([OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Shops_Slug] ON [Shops] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Shops_Status] ON [Shops] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShopUsers_ShopId_UserId] ON [ShopUsers] ([ShopId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ShopUsers_UserId] ON [ShopUsers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SiteSettings_Key] ON [SiteSettings] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StockMovements_ProductVariantId] ON [StockMovements] ([ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaxClasses_Name] ON [TaxClasses] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Translations_Culture_Key] ON [Translations] ([Culture], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserRoles_UserId_RoleName] ON [UserRoles] ([UserId], [RoleName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_NormalizedEmail] ON [Users] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_VendorLedgerEntries_OrderId] ON [VendorLedgerEntries] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_VendorLedgerEntries_PostSaleRequestId] ON [VendorLedgerEntries] ([PostSaleRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_VendorLedgerEntries_ReturnRequestId] ON [VendorLedgerEntries] ([ReturnRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_VendorLedgerEntries_SettledUtc] ON [VendorLedgerEntries] ([SettledUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_VendorLedgerEntries_ShopId] ON [VendorLedgerEntries] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VendorPayouts_Reference] ON [VendorPayouts] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_VendorPayouts_ShopId] ON [VendorPayouts] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_VendorPayouts_Status] ON [VendorPayouts] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_WishlistItems_ProductId] ON [WishlistItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WishlistItems_UserId_ProductId] ON [WishlistItems] ([UserId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029080128_InitialSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029080128_InitialSchema', N'9.0.22');
END;

COMMIT;
GO

