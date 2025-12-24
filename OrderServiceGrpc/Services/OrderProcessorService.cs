using OrderServiceGrpc.Helpers.cs;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Repository;
using System.ComponentModel;

namespace OrderServiceGrpc.Services
{
    public interface IOrderProcessorService
    {
        Task<ProcessorResponseModel> CreateOrder(OrderModel model, int userId);
        Task<ProcessorResponseModel> UpdateOrder(OrderModel model, int userId);
        Task<ProcessorResponseModel> DeleteOrder(int orderId, int userId);
        Task<ProcessorResponseModel> GetAllOrders(DateTime startDate, DateTime endDate, int pageSize, int pageNumber);
        Task<ProcessorResponseModel> GetOrderById(int orderId);
    }
    public class OrderProcessorService : IOrderProcessorService
    {
        private readonly IOrderRepository _repo;

        public OrderProcessorService(IOrderRepository orderRepository)
        {
            _repo = orderRepository;
        }

        public async Task<ProcessorResponseModel> CreateOrder(OrderModel model, int userId)
        {
            await _repo.InsertOrderCreateEvent(model.Id);

            bool orderAdded = await _repo.AddOrder(model, userId);

            return new ProcessorResponseModel()
            {
                Status = orderAdded,
                Message = orderAdded ? "Added successfully" : "Failed to add"
            };
        }

        public async Task<ProcessorResponseModel> DeleteOrder(int orderId, int userId)
        {
            await _repo.InsertOrderCreateEvent(orderId);
            bool orderAdded = await _repo.DeleteOrder(orderId, userId);

            return new ProcessorResponseModel()
            {
                Status = orderAdded,
                Message = orderAdded ? "Deleted successfully" : "Failed to delete"
            };
        }

        public async Task<ProcessorResponseModel> GetAllOrders(DateTime startDate, DateTime endDate, int pageSize, int pageNumber)
        {
            Tuple<int, int, List<OrderModel>> result = await _repo.GetAllOrdersWithPagination(startDate, endDate, pageSize, pageNumber);

            if (result == null) { return new ProcessorResponseModel() { Message = "Failed to get orders", Status = false }; }

            try
            {
                ProcessorResponseModel response = new ProcessorResponseModel()
                {
                    Status = true,
                    Message = "Success",
                    TotalPages = result.Item1,
                    TotalOrders = result.Item2,
                    ListOfOrders = result.Item3
                };

                return response;
            }
            catch (Exception e)
            {
                return new ProcessorResponseModel() { Message = "Failed to get orders", Status = false, StackTrace = e.StackTrace };
            }
        }

        public async Task<ProcessorResponseModel> GetOrderById(int orderId)
        {
            OrderModel model = await _repo.GetOrderById(orderId);

            if (model == null) { return new ProcessorResponseModel() { Message = "Failed to get order", Status = false }; }

            return new ProcessorResponseModel()
            {
                Status = true,
                Message = "Success",
                Order = model
            };
        }

        public async Task<ProcessorResponseModel> UpdateOrder(OrderModel requestModel, int userId)
        {
            await _repo.InsertOrderCreateEvent(requestModel.Id);

            OrderModel dbModel = await _repo.GetOrderById(requestModel.Id);

            List<OrderItemModel> deleteList = new();
            List<OrderItemModel> addList = new();
            List<OrderItemModel> updateList = new();

            //Check for deleted items
            deleteList = dbModel.OrderItems.Where(d => !requestModel.OrderItems.Any(r => r.Id == d.Id)).Select(x => PrepareItemToDelete(x, requestModel.Id, userId)).ToList();

            //Check for added items
            addList = requestModel.OrderItems.Where(i => i.Id == 0).Select(x => PrepareItemToAdd(x, requestModel.Id, userId)).ToList();

            //Check for updated AND existing items
            Dictionary<int, OrderItemModel> dbLookup = dbModel.OrderItems.ToDictionary(i => i.Id);

            foreach (OrderItemModel req in requestModel.OrderItems.Where(i => i.Id != 0))
            {
                if (dbLookup.TryGetValue(req.Id, out var db))
                {
                    if (HasChanges(req, db))
                    {
                        req.GrossAmount = req.Quantity * req.UnitPrice;
                        req.ModifiedBy = userId;
                        req.ModifiedDate = DateTime.Now;

                        updateList.Add(req);
                    }
                }
            }

            bool orderAdded = await _repo.UpdateOrder(requestModel, addList, deleteList, updateList, userId);

            return new ProcessorResponseModel()
            {
                Status = orderAdded,
                Message = orderAdded ? "Updated successfully" : "Failed to updated"
            };
        }

        //Helper Functions
        private bool HasChanges(OrderItemModel req, OrderItemModel db)
        {
            return req.Quantity != db.Quantity ||
                   req.UnitPrice != db.UnitPrice ||
                   req.ProductId != db.ProductId ||
                   req.GrossAmount != db.GrossAmount;
        }

        private OrderItemModel PrepareItemToAdd(OrderItemModel item, int orderId, int userId)
        {
            item.OrderId = orderId;
            item.CreatedBy = userId;
            item.CreatedDate = DateTime.UtcNow;
            item.GrossAmount = item.UnitPrice * item.Quantity;

            return item;
        }

        private OrderItemModel PrepareItemToDelete(OrderItemModel item, int orderId, int userId)
        {
            item.IsDeleted = true;
            item.ModifiedBy = userId;
            item.ModifiedDate = DateTime.UtcNow;
            return item;
        }
    }
}
