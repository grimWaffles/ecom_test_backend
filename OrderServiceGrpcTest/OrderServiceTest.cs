using Moq;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Helpers.Converters;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Repository;
using OrderServiceGrpc.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderServiceGrpcTest
{
    public class OrderServiceTest
    {
        private readonly Mock<IOrderRepository> _mockOrderRepo = new Mock<IOrderRepository>();
        private readonly Mock<IUnitOfWork> _mockUow = new Mock<IUnitOfWork>();
        private readonly OrderService _service;

        public OrderServiceTest()
        {
            _service = new OrderService(_mockOrderRepo.Object, _mockUow.Object);
        }

        #region CreateOrder
        [Fact]
        public async Task CreateOrder_OrderInsertFails()
        {
            //Arrange
            (OrderDto orderDtoToInsert, _, OrderOutbox outboxEntry, int userId) = GetTestData();
            string exceptionMessage = "Failed to add order";

            SetupHappyPathUpTo(OrderStep.BeginTransaction, userId, outboxEntry);

            _mockUow.Setup(x => x.Orders.AddSingleOrder(It.IsAny<OrderModel>(), userId))
                    .ThrowsAsync(new Exception(exceptionMessage));

            //Act
            ConsumerResponseModel response = await _service.CreateOrder(orderDtoToInsert, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal(exceptionMessage, response.Message);

            //Verify
            _mockUow.Verify(x => x.Orders.AddSingleOrder(It.IsAny<OrderModel>(), userId), Times.Once);
            _mockUow.Verify(x => x.RollbackAsync(), Times.Once);
            _mockUow.Verify(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>()), Times.Never);
            _mockUow.Verify(x => x.SaveChangesAsync(), Times.Never);
            _mockUow.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task CreateOrder_OutboxAddFails()
        {
            //Arrange
            (OrderDto orderDtoToInsert, _, OrderOutbox outboxEntry, int userId) = GetTestData();
            string exceptionMessage = "Failed to add outbox";

            SetupHappyPathUpTo(OrderStep.AddOrder, userId, outboxEntry);

            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>()))
                    .ThrowsAsync(new Exception(exceptionMessage));

            //Act
            ConsumerResponseModel response = await _service.CreateOrder(orderDtoToInsert, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal(exceptionMessage, response.Message);

            //Verify
            _mockUow.Verify(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>()), Times.Once);
            _mockUow.Verify(x => x.RollbackAsync(), Times.Once);
            _mockUow.Verify(x => x.SaveChangesAsync(), Times.Never);
            _mockUow.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task CreateOrder_SaveChangesFails()
        {
            //Arrange
            (OrderDto orderDtoToInsert, _, OrderOutbox outboxEntry, int userId) = GetTestData();
            string exceptionMessage = "Failed to save changes";

            SetupHappyPathUpTo(OrderStep.AddOutbox, userId, outboxEntry);

            _mockUow.Setup(x => x.SaveChangesAsync())
                    .ThrowsAsync(new Exception(exceptionMessage));

            //Act
            ConsumerResponseModel response = await _service.CreateOrder(orderDtoToInsert, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal(exceptionMessage, response.Message);

            //Verify
            _mockUow.Verify(x => x.SaveChangesAsync(), Times.Once);
            _mockUow.Verify(x => x.RollbackAsync(), Times.Once);
            _mockUow.Verify(x => x.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task CreateOrder_CommitFails()
        {
            //Arrange
            (OrderDto orderDtoToInsert, _, OrderOutbox outboxEntry, int userId) = GetTestData();
            string exceptionMessage = "Failed to commit changes";

            SetupHappyPathUpTo(OrderStep.SaveChanges, userId, outboxEntry);

            _mockUow.Setup(x => x.CommitAsync())
                    .ThrowsAsync(new Exception(exceptionMessage));

            //Act
            ConsumerResponseModel response = await _service.CreateOrder(orderDtoToInsert, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal(exceptionMessage, response.Message);

            //Verify
            _mockUow.Verify(x => x.CommitAsync(), Times.Once);
            _mockUow.Verify(x => x.RollbackAsync(), Times.Once);
        }

        #endregion

        #region UpdateOrder

        /* Test #1
         * 
         * No item changes between request and db
         * addList / deleteList / updateList all empty
         * Update succeeds
         * 
         */
        [Fact]
        public async Task UpdateOrder_ReturnsSuccess_WhenNoItemChanges()
        {
            //Arrange
            (OrderDto requestDto, _, _, int userId) = GetTestData();
            (OrderDto dbDto, _, _, _) = GetTestData();
            OrderModel dbModel = OrderMapper.DtoToEntity(dbDto);

            List<OrderItemModel>? capturedAdd = null;
            List<OrderItemModel>? capturedDelete = null;
            List<OrderItemModel>? capturedUpdate = null;

            _mockOrderRepo.Setup(x => x.GetOrderById(requestDto.Id)).ReturnsAsync(dbModel);
            _mockOrderRepo.Setup(x => x.UpdateOrder(
                    It.IsAny<OrderModel>(),
                    It.IsAny<List<OrderItemModel>>(),
                    It.IsAny<List<OrderItemModel>>(),
                    It.IsAny<List<OrderItemModel>>(),
                    userId))
                .Callback<OrderModel, List<OrderItemModel>, List<OrderItemModel>, List<OrderItemModel>, int>(
                    (model, add, del, upd, uid) =>
                    {
                        capturedAdd = add;
                        capturedDelete = del;
                        capturedUpdate = upd;
                    })
                .ReturnsAsync(true);

            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>())).ReturnsAsync((OrderOutbox o) => o);
            _mockUow.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            _mockUow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            //Act
            ConsumerResponseModel response = await _service.UpdateOrder(requestDto, userId);

            //Assert
            Assert.True(response.Status);
            Assert.Equal("Updated successfully", response.Message);

            Assert.NotNull(capturedAdd);
            Assert.NotNull(capturedDelete);
            Assert.NotNull(capturedUpdate);
            Assert.Empty(capturedAdd!);
            Assert.Empty(capturedDelete!);
            Assert.Empty(capturedUpdate!); // assumes HasChanges returns false for identical item data

            //Verify
            _mockOrderRepo.Verify(x => x.UpdateOrder(
                It.IsAny<OrderModel>(), It.IsAny<List<OrderItemModel>>(), It.IsAny<List<OrderItemModel>>(),
                It.IsAny<List<OrderItemModel>>(), userId), Times.Once);
            _mockUow.Verify(x => x.CommitAsync(), Times.Once);
            _mockUow.Verify(x => x.RollbackAsync(), Times.Never);
        }


        /* Test #2
         * 
         * Request contains a new item (Id == 0) not present in db
         * addList should contain exactly that item
         * 
         */
        [Fact]
        public async Task UpdateOrder_PopulatesAddList_WhenNewItemHasZeroId()
        {
            //Arrange
            (OrderDto requestDto, _, _, int userId) = GetTestData();
            (OrderDto dbDto, _, _, _) = GetTestData();
            OrderModel dbModel = OrderMapper.DtoToEntity(dbDto);

            requestDto.Items.Add(new OrderItemDto
            {
                Id = 0, // new, unsaved item
                OrderId = requestDto.Id,
                ProductId = 77,
                Quantity = 10,
                UnitPrice = 15.0000,
                GrossAmount = 150.0000,
                Status = "AVAILABLE",
                CreatedBy = userId,
                CreatedDate = new TimestampDto { Seconds = 1786838400, Nanos = 0 },
                ModifiedBy = 0,
                ModifiedDate = null,
                IsDeleted = false
            });

            List<OrderItemModel>? capturedAdd = null;

            _mockOrderRepo.Setup(x => x.GetOrderById(requestDto.Id)).ReturnsAsync(dbModel);
            _mockOrderRepo.Setup(x => x.UpdateOrder(
                    It.IsAny<OrderModel>(), It.IsAny<List<OrderItemModel>>(), It.IsAny<List<OrderItemModel>>(),
                    It.IsAny<List<OrderItemModel>>(), userId))
                .Callback<OrderModel, List<OrderItemModel>, List<OrderItemModel>, List<OrderItemModel>, int>(
                    (model, add, del, upd, uid) => capturedAdd = add)
                .ReturnsAsync(true);

            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>())).ReturnsAsync((OrderOutbox o) => o);
            _mockUow.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            _mockUow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            //Act
            ConsumerResponseModel response = await _service.UpdateOrder(requestDto, userId);

            //Assert
            Assert.True(response.Status);
            Assert.NotNull(capturedAdd);
            Assert.Single(capturedAdd!);
            Assert.Equal(77, capturedAdd![0].ProductId);
        }


        /* Test #3
         * 
         * Db has an item that is missing from the request's item list
         * deleteList should contain exactly that item
         * 
         */
        [Fact]
        public async Task UpdateOrder_PopulatesDeleteList_WhenDbItemMissingFromRequest()
        {
            //Arrange
            (OrderDto requestDto, _, _, int userId) = GetTestData();
            (OrderDto dbDto, _, _, _) = GetTestData();
            OrderModel dbModel = OrderMapper.DtoToEntity(dbDto);

            requestDto.Items.RemoveAll(i => i.Id == 4453);

            List<OrderItemModel>? capturedDelete = null;

            _mockOrderRepo.Setup(x => x.GetOrderById(requestDto.Id)).ReturnsAsync(dbModel);
            _mockOrderRepo.Setup(x => x.UpdateOrder(
                    It.IsAny<OrderModel>(), It.IsAny<List<OrderItemModel>>(), It.IsAny<List<OrderItemModel>>(),
                    It.IsAny<List<OrderItemModel>>(), userId))
                .Callback<OrderModel, List<OrderItemModel>, List<OrderItemModel>, List<OrderItemModel>, int>(
                    (model, add, del, upd, uid) => capturedDelete = del)
                .ReturnsAsync(true);

            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>())).ReturnsAsync((OrderOutbox o) => o);
            _mockUow.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            _mockUow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            //Act
            ConsumerResponseModel response = await _service.UpdateOrder(requestDto, userId);

            //Assert
            Assert.True(response.Status);
            Assert.NotNull(capturedDelete);
            Assert.Single(capturedDelete!);
            Assert.Equal(4453, capturedDelete![0].Id);
        }


        /* Test #4
         * 
         * An existing item's data changes (quantity/price)
         * updateList should contain that item with recalculated GrossAmount and ModifiedBy set
         * 
         */
        [Fact]
        public async Task UpdateOrder_PopulatesUpdateList_WhenExistingItemChanges()
        {
            //Arrange
            (OrderDto requestDto, _, _, int userId) = GetTestData();
            (OrderDto dbDto, _, _, _) = GetTestData();
            OrderModel dbModel = OrderMapper.DtoToEntity(dbDto);

            OrderItemDto itemToChange = requestDto.Items.First(i => i.Id == 4450);
            itemToChange.Quantity = 100; // was 30
            itemToChange.UnitPrice = 25.0000; // unchanged price — quantity change alone should trip HasChanges

            List<OrderItemModel>? capturedUpdate = null;

            _mockOrderRepo.Setup(x => x.GetOrderById(requestDto.Id)).ReturnsAsync(dbModel);
            _mockOrderRepo.Setup(x => x.UpdateOrder(
                    It.IsAny<OrderModel>(), It.IsAny<List<OrderItemModel>>(), It.IsAny<List<OrderItemModel>>(),
                    It.IsAny<List<OrderItemModel>>(), userId))
                .Callback<OrderModel, List<OrderItemModel>, List<OrderItemModel>, List<OrderItemModel>, int>(
                    (model, add, del, upd, uid) => capturedUpdate = upd)
                .ReturnsAsync(true);

            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>())).ReturnsAsync((OrderOutbox o) => o);
            _mockUow.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            _mockUow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            //Act
            ConsumerResponseModel response = await _service.UpdateOrder(requestDto, userId);

            //Assert
            Assert.True(response.Status);
            Assert.NotNull(capturedUpdate);
            Assert.Single(capturedUpdate!);

            OrderItemModel updatedItem = capturedUpdate![0];
            Assert.Equal(4450, updatedItem.Id);
            Assert.Equal(100, updatedItem.Quantity);
            Assert.Equal((double)2500.0000, (double) updatedItem.GrossAmount); // 100 * 25 recalculated by the service
            Assert.Equal(userId, updatedItem.ModifiedBy);
        }


        /* Test #5
         * 
         * One item removed, one item changed, one item added — all three lists populated in a single call
         * 
         */
        [Fact]
        public async Task UpdateOrder_HandlesAddDeleteAndUpdate_Together()
        {
            //Arrange
            (OrderDto requestDto, _, _, int userId) = GetTestData();
            (OrderDto dbDto, _, _, _) = GetTestData();
            OrderModel dbModel = OrderMapper.DtoToEntity(dbDto);

            requestDto.Items.RemoveAll(i => i.Id == 4452); // delete

            OrderItemDto itemToChange = requestDto.Items.First(i => i.Id == 4451);
            itemToChange.Quantity = 50; // update

            requestDto.Items.Add(new OrderItemDto // add
            {
                Id = 0,
                OrderId = requestDto.Id,
                ProductId = 88,
                Quantity = 5,
                UnitPrice = 10.0000,
                GrossAmount = 50.0000,
                Status = "AVAILABLE",
                CreatedBy = userId,
                CreatedDate = new TimestampDto { Seconds = 1786838400, Nanos = 0 },
                ModifiedBy = 0,
                ModifiedDate = null,
                IsDeleted = false
            });

            List<OrderItemModel>? capturedAdd = null;
            List<OrderItemModel>? capturedDelete = null;
            List<OrderItemModel>? capturedUpdate = null;

            _mockOrderRepo.Setup(x => x.GetOrderById(requestDto.Id)).ReturnsAsync(dbModel);
            _mockOrderRepo.Setup(x => x.UpdateOrder(
                    It.IsAny<OrderModel>(), It.IsAny<List<OrderItemModel>>(), It.IsAny<List<OrderItemModel>>(),
                    It.IsAny<List<OrderItemModel>>(), userId))
                .Callback<OrderModel, List<OrderItemModel>, List<OrderItemModel>, List<OrderItemModel>, int>(
                    (model, add, del, upd, uid) =>
                    {
                        capturedAdd = add;
                        capturedDelete = del;
                        capturedUpdate = upd;
                    })
                .ReturnsAsync(true);

            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>())).ReturnsAsync((OrderOutbox o) => o);
            _mockUow.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            _mockUow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            //Act
            ConsumerResponseModel response = await _service.UpdateOrder(requestDto, userId);

            //Assert
            Assert.True(response.Status);

            Assert.Single(capturedAdd!);
            Assert.Equal(88, capturedAdd![0].ProductId);

            Assert.Single(capturedDelete!);
            Assert.Equal(4452, capturedDelete![0].Id);

            Assert.Single(capturedUpdate!);
            Assert.Equal(4451, capturedUpdate![0].Id);
        }


        /* Test #6
         * 
         * requestModel.Id == 0 -> short-circuits regardless of db lookup
         * 
         */
        [Fact]
        public async Task UpdateOrder_ReturnsNotFound_WhenRequestIdIsZero()
        {
            //Arrange
            (OrderDto requestDto, _, _, int userId) = GetTestData();
            requestDto.Id = 0;

            //Act
            ConsumerResponseModel response = await _service.UpdateOrder(requestDto, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal("Order not found for update", response.Message);

            //Verify — nothing downstream should run
            _mockOrderRepo.Verify(x => x.GetOrderById(It.IsAny<int>()), Times.Once);
            _mockUow.Verify(x => x.BeginTransactionAsync(), Times.Never);
            _mockOrderRepo.Verify(x => x.UpdateOrder(
                It.IsAny<OrderModel>(), It.IsAny<List<OrderItemModel>>(), It.IsAny<List<OrderItemModel>>(),
                It.IsAny<List<OrderItemModel>>(), It.IsAny<int>()), Times.Never);
        }


        /* Test #7
         * 
         * Order not found in db (GetOrderById returns null)
         * 
         */
        [Fact]
        public async Task UpdateOrder_ReturnsNotFound_WhenOrderDoesNotExistInDb()
        {
            //Arrange
            (OrderDto requestDto, _, _, int userId) = GetTestData();

            _mockOrderRepo.Setup(x => x.GetOrderById(requestDto.Id)).ReturnsAsync((OrderModel?)null);

            //Act
            ConsumerResponseModel response = await _service.UpdateOrder(requestDto, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal("Order not found for update", response.Message);

            _mockUow.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }


        /* Test #8
         * 
         * repo.UpdateOrder throws after a transaction was started
         * DbTransaction is non-null on the mock -> rollback should be called
         * 
         */
        [Fact]
        public async Task UpdateOrder_RollsBack_WhenRepoUpdateFailsAndTransactionIsActive()
        {
            //Arrange
            (OrderDto requestDto, _, _, int userId) = GetTestData();
            (OrderDto dbDto, _, _, _) = GetTestData();
            OrderModel dbModel = OrderMapper.DtoToEntity(dbDto);

            string exceptionMessage = "Failed to update order";

            _mockOrderRepo.Setup(x => x.GetOrderById(requestDto.Id)).ReturnsAsync(dbModel);
            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockOrderRepo.Setup(x => x.UpdateOrder(
                    It.IsAny<OrderModel>(), It.IsAny<List<OrderItemModel>>(), It.IsAny<List<OrderItemModel>>(),
                    It.IsAny<List<OrderItemModel>>(), userId))
                .ThrowsAsync(new Exception(exceptionMessage));

            // The service only rolls back if _uow.DbTransaction is non-null, so an active
            // transaction has to be simulated explicitly — a bare mock returns null by default.
            _mockUow.Setup(x => x.DbTransaction).Returns(Mock.Of<IDbTransaction>());

            //Act
            ConsumerResponseModel response = await _service.UpdateOrder(requestDto, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal(exceptionMessage, response.Message);

            _mockUow.Verify(x => x.RollbackAsync(), Times.Once);
            _mockUow.Verify(x => x.CommitAsync(), Times.Never);
        }


        /* Test #9
         * 
         * Exception occurs before any transaction is established (e.g. GetOrderById fails)
         * DbTransaction is null on the mock -> rollback must NOT be called
         * (documents the current DbTransaction-null-check behavior)
         * 
         */
        [Fact]
        public async Task UpdateOrder_DoesNotRollBack_WhenExceptionOccursBeforeTransactionIsEstablished()
        {
            //Arrange
            (OrderDto requestDto, _, _, int userId) = GetTestData();

            string exceptionMessage = "Order lookup failed";

            _mockOrderRepo.Setup(x => x.GetOrderById(requestDto.Id)).ThrowsAsync(new Exception(exceptionMessage));

            //Act
            ConsumerResponseModel response = await _service.UpdateOrder(requestDto, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal(exceptionMessage, response.Message);

            _mockUow.Verify(x => x.RollbackAsync(), Times.Never);
        }

        #endregion

        #region UpdateDeleteStatusForSingleOrder

        [Fact]
        public async Task UpdateDeleteStatusForSingleOrder_ReturnsSuccess_WhenDeleteSucceeds()
        {
            //Arrange
            int orderId = 8999;
            int userId = 1;

            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockOrderRepo.Setup(x => x.UpdateDeleteStatusForSingleOrder(orderId, userId)).ReturnsAsync(true);
            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>())).ReturnsAsync((OrderOutbox o) => o);
            _mockUow.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            _mockUow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            //Act
            ConsumerResponseModel response = await _service.UpdateDeleteStatusForSingleOrder(orderId, userId);

            //Assert
            Assert.True(response.Status);
            Assert.Equal("Deleted successfully", response.Message);

            //Verify
            _mockUow.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _mockOrderRepo.Verify(x => x.UpdateDeleteStatusForSingleOrder(orderId, userId), Times.Once);
            _mockUow.Verify(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>()), Times.Once);
            _mockUow.Verify(x => x.SaveChangesAsync(), Times.Once);
            _mockUow.Verify(x => x.CommitAsync(), Times.Once);
            _mockUow.Verify(x => x.RollbackAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateDeleteStatusForSingleOrder_ReturnsFailureMessage_WhenRepoReturnsFalse()
        {
            //Arrange
            int orderId = 8999;
            int userId = 1;

            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockOrderRepo.Setup(x => x.UpdateDeleteStatusForSingleOrder(orderId, userId)).ReturnsAsync(false);
            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>())).ReturnsAsync((OrderOutbox o) => o);
            _mockUow.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            _mockUow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            //Act
            ConsumerResponseModel response = await _service.UpdateDeleteStatusForSingleOrder(orderId, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal("Failed to delete", response.Message);

            //Verify
            _mockUow.Verify(x => x.CommitAsync(), Times.Once);
            _mockUow.Verify(x => x.RollbackAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateDeleteStatusForSingleOrder_RollsBack_WhenRepoThrows()
        {
            //Arrange
            int orderId = 8999;
            int userId = 1;
            string exceptionMessage = "Failed to update delete status";

            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockOrderRepo.Setup(x => x.UpdateDeleteStatusForSingleOrder(orderId, userId))
                .ThrowsAsync(new Exception(exceptionMessage));

            //Act
            ConsumerResponseModel response = await _service.UpdateDeleteStatusForSingleOrder(orderId, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal(exceptionMessage, response.Message);

            //Verify
            _mockUow.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _mockOrderRepo.Verify(x => x.UpdateDeleteStatusForSingleOrder(orderId, userId), Times.Once);
            _mockUow.Verify(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>()), Times.Never);
            _mockUow.Verify(x => x.SaveChangesAsync(), Times.Never);
            _mockUow.Verify(x => x.CommitAsync(), Times.Never);
            _mockUow.Verify(x => x.RollbackAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateDeleteStatusForSingleOrder_RollsBack_WhenOutboxCreateFails()
        {
            //Arrange
            int orderId = 8999;
            int userId = 1;
            string exceptionMessage = "Failed to create outbox entry";

            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockOrderRepo.Setup(x => x.UpdateDeleteStatusForSingleOrder(orderId, userId)).ReturnsAsync(true);
            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>())).ThrowsAsync(new Exception(exceptionMessage));

            //Act
            ConsumerResponseModel response = await _service.UpdateDeleteStatusForSingleOrder(orderId, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal(exceptionMessage, response.Message);

            //Verify
            _mockUow.Verify(x => x.SaveChangesAsync(), Times.Never);
            _mockUow.Verify(x => x.CommitAsync(), Times.Never);
            _mockUow.Verify(x => x.RollbackAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateDeleteStatusForSingleOrder_RollsBack_WhenSaveChangesFails()
        {
            //Arrange
            int orderId = 8999;
            int userId = 1;
            string exceptionMessage = "Failed to save changes";

            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockOrderRepo.Setup(x => x.UpdateDeleteStatusForSingleOrder(orderId, userId)).ReturnsAsync(true);
            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>())).ReturnsAsync((OrderOutbox o) => o);
            _mockUow.Setup(x => x.SaveChangesAsync()).ThrowsAsync(new Exception(exceptionMessage));

            //Act
            ConsumerResponseModel response = await _service.UpdateDeleteStatusForSingleOrder(orderId, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal(exceptionMessage, response.Message);

            //Verify
            _mockUow.Verify(x => x.CommitAsync(), Times.Never);
            _mockUow.Verify(x => x.RollbackAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateDeleteStatusForSingleOrder_RollsBack_WhenCommitFails()
        {
            //Arrange
            int orderId = 8999;
            int userId = 1;
            string exceptionMessage = "Failed to commit changes";

            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            _mockOrderRepo.Setup(x => x.UpdateDeleteStatusForSingleOrder(orderId, userId)).ReturnsAsync(true);
            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>())).ReturnsAsync((OrderOutbox o) => o);
            _mockUow.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            _mockUow.Setup(x => x.CommitAsync()).ThrowsAsync(new Exception(exceptionMessage));

            //Act
            ConsumerResponseModel response = await _service.UpdateDeleteStatusForSingleOrder(orderId, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal(exceptionMessage, response.Message);

            //Verify
            _mockUow.Verify(x => x.CommitAsync(), Times.Once);
            _mockUow.Verify(x => x.RollbackAsync(), Times.Once);
        }

        #endregion


        #region GetAllOrders

        [Fact]
        public async Task GetAllOrders_ReturnsSuccess_WhenResultIsNotNull()
        {
            //Arrange
            DateTime startDate = new DateTime(2026, 1, 1);
            DateTime endDate = new DateTime(2026, 1, 31);
            int pageSize = 10;
            int pageNumber = 1;
            int userId = 1;

            (_, OrderModel modelToInsert, _, _) = GetTestData();

            PagedOrderListModel pagedResult = new PagedOrderListModel
            {
                TotalPages = 3,
                TotalOrders = 25,
                OrderList = new List<OrderModel> { modelToInsert }
            };

            _mockOrderRepo.Setup(x => x.GetAllOrdersWithPagination(startDate, endDate, pageSize, pageNumber, userId))
                .ReturnsAsync(pagedResult);

            //Act
            ConsumerResponseModel response = await _service.GetAllOrders(startDate, endDate, pageSize, pageNumber, userId);

            //Assert
            Assert.True(response.Status);
            Assert.Equal("Success", response.Message);
            Assert.Equal(3, response.TotalPages);
            Assert.Equal(25, response.TotalOrders);
            Assert.NotNull(response.ListOfOrders);
            Assert.Single(response.ListOfOrders);

            //Verify
            _mockOrderRepo.Verify(x => x.GetAllOrdersWithPagination(startDate, endDate, pageSize, pageNumber, userId), Times.Once);
        }

        [Fact]
        public async Task GetAllOrders_ReturnsFailure_WhenResultIsNull()
        {
            //Arrange
            DateTime startDate = new DateTime(2026, 1, 1);
            DateTime endDate = new DateTime(2026, 1, 31);
            int pageSize = 10;
            int pageNumber = 1;
            int userId = 1;

            _mockOrderRepo.Setup(x => x.GetAllOrdersWithPagination(startDate, endDate, pageSize, pageNumber, userId))
                .ReturnsAsync((PagedOrderListModel)null!);

            //Act
            ConsumerResponseModel response = await _service.GetAllOrders(startDate, endDate, pageSize, pageNumber, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal("Failed to get orders", response.Message);
        }

        [Fact]
        public async Task GetAllOrders_ReturnsFailure_WhenMappingThrows()
        {
            //Arrange
            DateTime startDate = new DateTime(2026, 1, 1);
            DateTime endDate = new DateTime(2026, 1, 31);
            int pageSize = 10;
            int pageNumber = 1;
            int userId = 1;

            PagedOrderListModel pagedResult = new PagedOrderListModel
            {
                TotalPages = 1,
                TotalOrders = 1,
                OrderList = null! // forces OrderMapper.EntityToOrderDto selection to throw
            };

            _mockOrderRepo.Setup(x => x.GetAllOrdersWithPagination(startDate, endDate, pageSize, pageNumber, userId))
                .ReturnsAsync(pagedResult);

            //Act
            ConsumerResponseModel response = await _service.GetAllOrders(startDate, endDate, pageSize, pageNumber, userId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal("Failed to get orders", response.Message);
        }

        #endregion


        #region GetOrderById

        [Fact]
        public async Task GetOrderById_ReturnsSuccess_WhenOrderExists()
        {
            //Arrange
            (_, OrderModel modelToInsert, _, _) = GetTestData();
            int orderId = modelToInsert.Id;

            _mockOrderRepo.Setup(x => x.GetOrderById(orderId)).ReturnsAsync(modelToInsert);

            //Act
            ConsumerResponseModel response = await _service.GetOrderById(orderId);

            //Assert
            Assert.True(response.Status);
            Assert.Equal("Success", response.Message);
            Assert.NotNull(response.Order);

            //Verify
            _mockOrderRepo.Verify(x => x.GetOrderById(orderId), Times.Once);
        }

        [Fact]
        public async Task GetOrderById_ReturnsFailure_WhenOrderDoesNotExist()
        {
            //Arrange
            int orderId = 8999;

            _mockOrderRepo.Setup(x => x.GetOrderById(orderId)).ReturnsAsync((OrderModel)null!);

            //Act
            ConsumerResponseModel response = await _service.GetOrderById(orderId);

            //Assert
            Assert.False(response.Status);
            Assert.Equal("Failed to get order", response.Message);
            Assert.Null(response.Order);

            //Verify
            _mockOrderRepo.Verify(x => x.GetOrderById(orderId), Times.Once);
        }

        #endregion

        private enum OrderStep
        {
            BeginTransaction,
            AddOrder,
            AddOutbox,
            SaveChanges,
            Commit
        }

        /// <summary>
        /// Configures _mockUow with successful setups for every step up to and including
        /// the given step. Steps after that point are left unconfigured (so Moq's default
        /// loose-mock behavior applies) unless the caller overrides them afterward.
        /// </summary>
        private void SetupHappyPathUpTo(OrderStep step, int userId, OrderOutbox outboxEntry)
        {
            _mockUow.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
            if (step < OrderStep.AddOrder) return;

            _mockUow.Setup(x => x.Orders.AddSingleOrder(It.IsAny<OrderModel>(), userId)).ReturnsAsync(8999);
            if (step < OrderStep.AddOutbox) return;

            _mockUow.Setup(x => x.Outbox.CreateAsync(It.IsAny<OrderOutbox>())).ReturnsAsync(outboxEntry);
            if (step < OrderStep.SaveChanges) return;

            _mockUow.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            if (step < OrderStep.Commit) return;

            _mockUow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
        }

        private (OrderDto, OrderModel, OrderOutbox, int) GetTestData()
        {
            int userId = 1, insertedOrderId = 8999;
            OrderDto orderDtoToInsert = new OrderDto
            {
                Id = insertedOrderId,
                OrderDate = new TimestampDto
                {
                    Seconds = 1786838400,
                    Nanos = 0
                },
                OrderCounter = 1,
                UserId = 15,
                Status = "PENDING",
                NetAmount = 4795.0000,
                CreatedBy = 1,
                CreatedDate = new TimestampDto
                {
                    Seconds = 1786838400,
                    Nanos = 0
                },
                ModifiedDate = null,
                ModifiedBy = 0, // no value provided (NULL) — using default int
                IsDeleted = false,
                Items = new List<OrderItemDto>
                {
                    new OrderItemDto
                    {
                        Id = 4450,
                        OrderId = insertedOrderId,
                        ProductId = 92,
                        Quantity = 30,
                        GrossAmount = 750.0000,
                        Status = "AVAILABLE",
                        CreatedBy = 1,
                        CreatedDate = new TimestampDto
                {
                    Seconds = 1786838400,
                    Nanos = 0
                },
                        ModifiedBy = 0,
                        ModifiedDate = null,
                        IsDeleted = false,
                        UnitPrice = 25.0000
                    },
                    new OrderItemDto
                    {
                        Id = 4451,
                        OrderId = insertedOrderId,
                        ProductId = 24,
                        Quantity = 21,
                        GrossAmount = 2079.0000,
                        Status = "AVAILABLE",
                        CreatedBy = 1,
                        CreatedDate = new TimestampDto
                {
                    Seconds = 1786838400,
                    Nanos = 0
                },
                        ModifiedBy = 0,
                        ModifiedDate = null,
                        IsDeleted = false,
                        UnitPrice = 99.0000
                    },
                    new OrderItemDto
                    {
                        Id = 4452,
                        OrderId = insertedOrderId,
                        ProductId = 34,
                        Quantity = 39,
                        GrossAmount = 741.0000,
                        Status = "AVAILABLE",
                        CreatedBy = 1,
                        CreatedDate = new TimestampDto
                {
                    Seconds = 1786838400,
                    Nanos = 0
                },
                        ModifiedBy = 0,
                        ModifiedDate = null,
                        IsDeleted = false,
                        UnitPrice = 19.0000
                    },
                    new OrderItemDto
                    {
                        Id = 4453,
                        OrderId = insertedOrderId,
                        ProductId = 51,
                        Quantity = 25,
                        GrossAmount = 1225.0000,
                        Status = "AVAILABLE",
                        CreatedBy = 1,
                        CreatedDate = new TimestampDto
                {
                    Seconds = 1786838400,
                    Nanos = 0
                },
                        ModifiedBy = 0,
                        ModifiedDate = null,
                        IsDeleted = false,
                        UnitPrice = 49.0000
                    }
                }
            };

            OrderModel modelToInsert = OrderMapper.DtoToEntity(orderDtoToInsert);

            OrderOutbox outboxEntry = new OrderOutbox()
            {
                AggregateId = insertedOrderId,
                AggregateType = "Order",
                EventType = "OrderCreated",
                Topic = "order-create-success",
                PartitionKey = insertedOrderId.ToString(),
                Payload = System.Text.Json.JsonSerializer.Serialize(OrderMapper.EntityToMessage(modelToInsert)),
                Headers = "{}",
                StatusId = 1, // Assuming 1 is the status for 'Pending'
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow,
                ScheduledAt = DateTime.UtcNow
            };

            return (orderDtoToInsert, modelToInsert, outboxEntry, userId);
        }
    }
}
