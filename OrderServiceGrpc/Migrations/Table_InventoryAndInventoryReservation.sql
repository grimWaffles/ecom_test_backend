------------------------------------------------------------------
-- Create tables
------------------------------------------------------------------
drop table if exists Inventory
create table Inventory
(
	Id bigint primary key identity(1,1),
	
	ProductId int foreign key references Products(Id) not null,
	ProductCategoryId int foreign key references ProductCategories(Id) not null,
	Quantity int not null default 0,
	
	CreatedBy int foreign key references Users(Id) not null,
	CreatedDate datetime not null,
	ModifiedBy int foreign key references Users(Id),
	ModifiedDate datetime,

	IsDeleted bit default 0
)

drop table if exists InventoryReservation
create table InventoryReservation
(
	Id bigint primary key identity(1,1),
	
	ProductId int foreign key references Products(Id) not null,
	OrderId int foreign key references Orders(Id) not null,
	LockQuantity int not null,
	LockExpirationDate datetime not null,
	
	CreatedBy int foreign key references Users(Id) not null,
	CreatedDate datetime not null,
	ModifiedBy int foreign key references Users(Id),
	ModifiedDate datetime,

	IsDeleted bit default 0
)

------------------------------------------------------------------
-- Populate tables
------------------------------------------------------------------

truncate table Inventory;

with TotalInventory as 
(
	select 
		ot.ProductId, sum(ot.Quantity) TQuantity, sum(ot.UnitPrice*ot.Quantity) TotalSale 
	from OrderItems ot
	group by ot.ProductId
)
Insert into Inventory(ProductId, ProductCategoryId, Quantity, CreatedBy, CreatedDate, IsDeleted)
select 
	t.ProductId, pc.Id, Convert(int,t.TQuantity*1.40) Quantity, 1, GETDATE(), 0
from TotalInventory t
	inner join Products p on p.Id  = t.ProductId
	inner join ProductCategories pc on pc.Id = p.ProductCategoryId
order by t.TotalSale desc;

Insert into Inventory(ProductId, ProductCategoryId, Quantity, CreatedBy, CreatedDate, IsDeleted)
select 
	p.Id, pc.Id, 250,1, GETDATE(), 0 
from Products p 
	inner join ProductCategories pc on pc.Id = p.ProductCategoryId
where p.Id not in (select ProductId from Inventory)

select * from Products where Id not in (select ProductId from Inventory)

select * from Inventory
select * from InventoryReservation