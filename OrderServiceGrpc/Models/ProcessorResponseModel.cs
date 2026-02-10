using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Models
{
    public class ProcessorResponseModel
    {
        public string Message { get; set; }
        public bool Status { get; set; }
        public string StackTrace { get; set; }
        public OrderDto Order { get; set; }
        public int TotalPages { get; set; }
        public int TotalOrders { get; set; }
        public List<OrderDto> ListOfOrders { get; set; }
    }
}
