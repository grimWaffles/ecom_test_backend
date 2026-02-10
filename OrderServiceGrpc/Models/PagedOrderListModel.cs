using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Models
{
    public class PagedOrderListModel
    {
        public int TotalPages { get; set; }
        public int TotalOrders { get; set; }
        public List<OrderModel> OrderList { get; set; }
    }
}
