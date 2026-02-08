using Google.Protobuf.WellKnownTypes;
using OrderServiceGrpc.Protos;

namespace OrderServiceGrpc.Models.Entities
{
    public class OrderModel
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public int OrderCounter { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = "";
        public decimal NetAmount { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int ModifiedBy { get; set; }
        public DateTime ModifiedDate { get; set; }
        public bool IsDeleted { get; set; }
        public List<OrderItemModel> OrderItems { get; set; } = new ();
    }
}
