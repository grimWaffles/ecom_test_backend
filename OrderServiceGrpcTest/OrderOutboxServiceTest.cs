using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Repository;
using OrderServiceGrpc.Services;

namespace OrderServiceGrpcTest
{
    public class OrderOutboxServiceTest
    {
        private readonly Mock<IOrderOutboxRepository> _mockRepo;
        private readonly Mock<ILogger<OrderOutboxService>> _mockLogger;
        private readonly OrderOutboxService _service;

        public OrderOutboxServiceTest()
        {
            _mockRepo = new Mock<IOrderOutboxRepository>();
            _mockLogger = new Mock<ILogger<OrderOutboxService>>();
            _service = new OrderOutboxService(_mockRepo.Object, _mockLogger.Object);
        }

        #region GetRecentRecordsToPublishAfterDateAsync
        [Fact]
        public async Task GetRecentRecordsToPublishAfterDateAsync_ReturnsList()
        {
            //Arrange 
            DateTime date = DateTime.UtcNow.AddDays(-1);
            List<OrderOutbox> recordsToReturn = new List<OrderOutbox>
            {
                new OrderOutbox{Id = 1},
                new OrderOutbox{Id = 2}
            };

            _mockRepo.Setup(x => x.GetRecentRecordsToPublishAfterDateAsync(date)).ReturnsAsync(recordsToReturn);

            //Act
            var result = await _service.GetRecentRecordsToPublishAfterDateAsync(date);

            //Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());

            _mockRepo.Verify(x => x.GetRecentRecordsToPublishAfterDateAsync(date), Times.Once);
        }

        [Fact]
        public async Task GetRecentRecordsToPublishAfterDateAsync_ReturnsEmptyList_ForFutureDate()
        {
            //Arrange
            DateTime futureDate = DateTime.UtcNow.AddDays(10);

            //Act
            var result = await _service.GetRecentRecordsToPublishAfterDateAsync(futureDate);

            //Assert
            Assert.NotNull(result);
            Assert.Empty(result);

            _mockRepo.Verify(x => x.GetRecentRecordsToPublishAfterDateAsync(futureDate), Times.Never);
        }

        [Fact]
        public async Task GetRecentRecordsToPublishAfterDateAsync_ThrowsException()
        {
            //Arrange
            var date = DateTime.UtcNow;
            Exception e = new Exception("DB error");

            _mockRepo.Setup(x => x.GetRecentRecordsToPublishAfterDateAsync(date)).Throws(e);

            //Act
            var result = await Assert.ThrowsAsync<Exception>(() => _service.GetRecentRecordsToPublishAfterDateAsync(date));

            //Assert
            Assert.Equal("DB error", result.Message);
        }

        #endregion

        #region GetAllRecordsAsync
        [Fact]
        public async Task GetAllRecords_ReturnsResult()
        {
            //Arrange
            var results = new List<OrderOutbox> {
                new OrderOutbox(){Id = 1},
                new OrderOutbox(){Id = 2}
            };

            var repositoryResult = (Items: results.AsEnumerable(), TotalCount: 2);

            _mockRepo.Setup(x => x.GetAllRecordsAsync(1, 2, 1, "Order")).ReturnsAsync(repositoryResult);

            //Act
            var response = await _service.GetAllRecordsAsync(1, 2, 1, "Order");

            //Assert
            Assert.Equal(2,response.Items.Count());
            Assert.Equal(2, response.TotalCount);

            _mockRepo.Verify(x => x.GetAllRecordsAsync(1, 2, 1, "Order"), Times.Once);
        }

        [Fact]
        public async Task GetAllRecords_ReturnsEmptyForInvalidPageNumber()
        {
            //Arrange
            int pageSize = 15, pageNumber = -2;

            //Act
            var result = await _service.GetAllRecordsAsync(pageNumber, pageSize);

            //Assert
            Assert.NotNull(result.Items);
            Assert.Equal(0, result.TotalCount);
            Assert.Empty(result.Items);

            _mockRepo.Verify(x => x.GetAllRecordsAsync(pageNumber, pageSize, null, null), Times.Never);

        }

        [Fact]
        public async Task GetAllRecords_ThrowsException()
        {
            //Arrange
            int pageSize = 15, pageNumber = -2;
            Exception e = new Exception("DB Error");
            _mockRepo.Setup(x => x.GetAllRecordsAsync(1, 2, 1, "Order")).ThrowsAsync(e);

            //Act
            var result = await Assert.ThrowsAsync<Exception>(() => _service.GetAllRecordsAsync(1, 2, 1, "Order"));

            Assert.Equal(e.Message, result.Message);
        }
        #endregion

        #region GetByIdAsync
        [Fact]
        public async Task GetByIdAsync_ReturnsResult()
        {
            //Arrange
            var result = new OrderOutbox() { Id = 1 };

            _mockRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(result);

            //Act
            var response = await _service.GetByIdAsync(1);

            //Assert
            Assert.NotNull(response);
            Assert.Equal(1, response.Id );

            _mockRepo.Verify(x => x.GetByIdAsync(1), Times.Once);
        }
        [Fact]
        public async Task GetByIdAsync_ReturnsNullIfInvalidId()
        {
            int id = -1; OrderOutbox res = (OrderOutbox?) null;

            _mockRepo.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(res);

            var response = await _service.GetByIdAsync(id);

            Assert.Equal(res,response);

            _mockRepo.Verify(x => x.GetByIdAsync(id), Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNullIfNotExists()
        {
            int id = 999; var expectedResult = (OrderOutbox?)null;
            _mockRepo.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(expectedResult);

            var response = await _service.GetByIdAsync(id);

            Assert.Null(response);
            _mockRepo.Verify(x => x.GetByIdAsync(id), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_ThrowsException()
        {
            Exception e = new Exception("DB Error");
            _mockRepo.Setup(x => x.GetByIdAsync(1)).ThrowsAsync(e);

            var response = await Assert.ThrowsAsync<Exception>(()=> _service.GetByIdAsync(1));

            Assert.Equal(e.Message, response.Message);
        }
        #endregion

        #region CreateAsync
        //Happy Path
        [Fact]
        public async Task CreateAsync_ReturnsResult()
        {
            //Arrange 
            OrderOutbox entity = new OrderOutbox
            {
                AggregateId = 1,
                AggregateType = "Order",
                EventType = "OrderCreated",
                Topic = "order-created",
                Payload = "{}",
                PartitionKey = "1"
            };

            _mockRepo.Setup(x => x.CreateAsync(entity)).ReturnsAsync(entity);

            //Act
            var response = await _service.CreateAsync(entity);

            //Assert
            Assert.NotNull(response);

            _mockRepo.Verify(x => x.CreateAsync(entity), Times.Once);
        }

        //check generic exception
        [Fact]
        public async Task CreateAsync_ThrowsDbException()
        {
            //Arrange 
            Exception e = new Exception("DB Error");
            OrderOutbox entity = new OrderOutbox
            {
                AggregateId = 1,
                AggregateType = "Order",
                EventType = "OrderCreated",
                Topic = "order-created",
                Payload = "{}",
                PartitionKey = "1"
            };

            _mockRepo.Setup(x => x.CreateAsync(entity)).ThrowsAsync(e);

            //Act
            var response = await Assert.ThrowsAsync<Exception>(() => _service.CreateAsync(entity));

            //Assert
            Assert.Equal(e.Message, response.Message);

            _mockRepo.Verify(x => x.CreateAsync(entity), Times.Once);

        }
        //Check arg null
        [Fact]
        public async Task CreateAsync_ArgNullException()
        {
            //Arrange 
            ArgumentNullException e = new ArgumentNullException("Value cannot be null. (Parameter 'entity')");
            OrderOutbox entity = (OrderOutbox?)null;

            _mockRepo.Setup(x => x.CreateAsync(entity)).ThrowsAsync(e);

            //Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateAsync(entity));

            _mockRepo.Verify(x => x.CreateAsync(entity), Times.Never);

        }
        //check invalid entry (Arg Exception) using member
        #endregion
    }
}