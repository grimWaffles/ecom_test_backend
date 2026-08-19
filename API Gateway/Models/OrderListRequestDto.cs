using Google.Protobuf;

namespace API_Gateway.Models
{
    public class OrderListRequestDto
    {
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int UserId { get; set; }
    }
    //message OrderListRequest
    //    {
    //        int32 pageSize = 1;
    //        int32 pageNumber = 2;
    //        google.protobuf.Timestamp startDate = 3;
    //        google.protobuf.Timestamp endDate = 4;

    //    }
}
