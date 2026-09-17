USE [ECommercePlatform]
GO

/****** Object:  Table [dbo].[CustomerTransaction]    Script Date: 9/16/2026 10:48:27 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[CustomerTransaction](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NOT NULL,
	[TransactionType] [nvarchar](50) NOT NULL,
	[Amount] [decimal](18, 2) NOT NULL,
	[CreatedDate] [datetime2](7) NOT NULL,
	[CreatedBy] [int] NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[TransactionDate] [datetime2](7) NOT NULL,
	[ModifiedDate] [datetime2](7) NULL,
	[ModifiedBy] [int] NULL,
	[TransactionKey] [nvarchar](70) NULL,
	[OrderId] [int] NULL,
 CONSTRAINT [PK_CustomerTransaction] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[CustomerTransaction] ADD  DEFAULT (getutcdate()) FOR [CreatedDate]
GO

ALTER TABLE [dbo].[CustomerTransaction] ADD  DEFAULT ((0)) FOR [IsDeleted]
GO

ALTER TABLE [dbo].[CustomerTransaction] ADD  DEFAULT (getutcdate()) FOR [ModifiedDate]
GO

ALTER TABLE [dbo].[CustomerTransaction]  WITH CHECK ADD FOREIGN KEY([CreatedBy])
REFERENCES [dbo].[Users] ([Id])
GO

ALTER TABLE [dbo].[CustomerTransaction]  WITH CHECK ADD FOREIGN KEY([ModifiedBy])
REFERENCES [dbo].[Users] ([Id])
GO

ALTER TABLE [dbo].[CustomerTransaction]  WITH CHECK ADD  CONSTRAINT [FK_CustomerTransaction_Order] FOREIGN KEY([OrderId])
REFERENCES [dbo].[Orders] ([Id])
GO

ALTER TABLE [dbo].[CustomerTransaction] CHECK CONSTRAINT [FK_CustomerTransaction_Order]
GO


