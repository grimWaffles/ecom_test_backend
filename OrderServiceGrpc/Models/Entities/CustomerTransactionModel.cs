namespace OrderServiceGrpc.Models.Entities
{
    public class CustomerTransactionModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedDate { get; set; }
        public int CreatedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public int ModifiedBy { get; set; }
    }
}
