using OrderServiceGrpc.Models.Dtos;

namespace OrderServiceGrpc.Models
{
    public class TransactionProcessorResponseModel
    {
        public List<CustomerTransactionDto> ListOfTransactions { get; set; } = new List<CustomerTransactionDto>();
        public int InsertedTrxId { get; set; } = 0;
        public bool Status { get; set; } = false;
        public string Message { get; set; } = "";
        public PagedTransactionResultFromService PagedTrxResults { get; set; } = new PagedTransactionResultFromService();
    }
}
