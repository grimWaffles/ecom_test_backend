using ApiGateway.Protos;

namespace API_Gateway.Models
{
    public class InventoryLifecycleResult
    {
        public InventoryMessage OriginalItem { get; set; } = null!;
        public bool DeleteSucceeded { get; set; }
        public InventoryMessage RecreatedItem { get; set; } = null!;
        public InventoryMessage UpdatedItem { get; set; } = null!;
        public int OriginalListCount { get; set; }
        public int PostDeleteListCount { get; set; }
    }
}
