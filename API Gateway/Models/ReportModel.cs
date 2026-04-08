namespace API_Gateway.Models
{
    public class ReportModel
    {
        public int Id { get; set; }
        public int ReportId { get; set; }
        public string ReportName { get; set; } = "";
        public bool IsPublic { get; set; }
        public int OwnerId { get; set; }
    }
}
