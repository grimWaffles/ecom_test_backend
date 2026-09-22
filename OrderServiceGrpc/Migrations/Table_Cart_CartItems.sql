--drop table if exists CartStatus
--drop table if exists CartItemStatus

--CREATE TABLE CartStatus (
--    Id INT NOT NULL PRIMARY KEY,
--    CartStatusName VARCHAR(50) NOT NULL
--);

--CREATE TABLE CartItemStatus (
--    Id INT NOT NULL PRIMARY KEY,
--    CartItemStatusName VARCHAR(50) NOT NULL
--);

--INSERT INTO CartStatus (Id, CartStatusName) VALUES
--(1, 'Active'),
--(2, 'CheckedOut'),
--(3, 'Abandoned');

--INSERT INTO CartItemStatus (Id, CartItemStatusName) VALUES
--(1, 'Reserved'),
--(2, 'Expired'),
--(3, 'Unavailable'),
--(4, 'Removed'),
--(5, 'Purchased');

--select * from CartStatus
--select * from CartItemStatus

--drop table if exists CartItem
--drop table if exists Cart

--CREATE TABLE Cart (
--    Id          INT IDENTITY(1,1) NOT NULL,
--    UserId      INT NOT NULL,
--    StatusId    INT NOT NULL,
--    CreatedAt   DATETIME NOT NULL CONSTRAINT DF_Cart_CreatedAt DEFAULT (GETUTCDATE()),
--    CreatedBy   INT NOT NULL,
--    UpdatedAt   DATETIME NULL,
--    UpdatedBy   INT NULL,
--    IsDeleted   BIT NOT NULL CONSTRAINT DF_Cart_IsDeleted DEFAULT (0),

--    CONSTRAINT PK_Cart PRIMARY KEY (Id),
--    CONSTRAINT FK_Cart_Users_UserId FOREIGN KEY (UserId) REFERENCES Users(Id),
--    CONSTRAINT FK_Cart_CartStatus FOREIGN KEY (StatusId) REFERENCES CartStatus(Id),
--    CONSTRAINT FK_Cart_Users_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(Id),
--    CONSTRAINT FK_Cart_Users_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES Users(Id)
--);

--CREATE TABLE CartItem (
--    Id                    INT IDENTITY(1,1) NOT NULL,
--    CartId                INT NOT NULL,
--    ProductId             INT NOT NULL,
--    Quantity              INT NOT NULL,
--    UnitPrice             DECIMAL(18,4) NOT NULL,
--    ReservationExpiresAt  DATETIME NOT NULL,
--    StatusId              INT NOT NULL,
--    CreatedAt             DATETIME NOT NULL CONSTRAINT DF_CartItem_CreatedAt DEFAULT (GETUTCDATE()),
--    CreatedBy             INT NOT NULL,
--    UpdatedAt             DATETIME NULL,
--    UpdatedBy             INT NULL,
--    IsDeleted             BIT NOT NULL CONSTRAINT DF_CartItem_IsDeleted DEFAULT (0),

--    CONSTRAINT PK_CartItem PRIMARY KEY (Id),
--    CONSTRAINT FK_CartItem_Cart FOREIGN KEY (CartId) REFERENCES Cart(Id),
--    CONSTRAINT FK_CartItem_Product FOREIGN KEY (ProductId) REFERENCES Products(Id),
--    CONSTRAINT FK_CartItem_CartItemStatus FOREIGN KEY (StatusId) REFERENCES CartItemStatus(Id),
--    CONSTRAINT FK_CartItem_Users_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(Id),
--    CONSTRAINT FK_CartItem_Users_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES Users(Id)
--);

--select * from Cart
--select * from CartItem