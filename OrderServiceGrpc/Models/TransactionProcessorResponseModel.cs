using OrderServiceGrpc.Models.Dtos;

namespace OrderServiceGrpc.Models
{
    public class TransactionProcessorResponseModel
    {
        public CustomerTransactionDto CustomerTransactionDto { get; set; }
        public List<CustomerTransactionDto> ListOfTransactions { get; set; }
        public int InsertedTrxId { get; set; }
        public bool Status { get; set; }
        public PagedTransactionResultService PagedTrxResults { get; set; }
    }
}
