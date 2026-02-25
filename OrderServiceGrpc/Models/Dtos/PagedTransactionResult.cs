using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Models.Dtos
{
    public class PagedTransactionResultRepo
    {
        public List<CustomerTransactionModel> ListOfTransactions { get; set; }
        public bool Status { get; set; }
        public string ErrorMessage { get; set; } = "";
        public int TotalPages { get; set; }
        public int TotalTransactions { get; set; }
    }

    public class PagedTransactionResultService
    {
        public List<CustomerTransactionDto> ListOfTransactions { get; set; }
        public bool Status { get; set; }
        public string ErrorMessage { get; set; } = "";
        public int TotalPages { get; set; }
        public int TotalTransactions { get; set; }
    }
}
