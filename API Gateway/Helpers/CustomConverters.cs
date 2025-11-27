namespace API_Gateway.Helpers
{
    public class CustomConverters
    {
        public Google.Protobuf.WellKnownTypes.Timestamp ConvertDateTimeToGoogleTimeStamp(DateTime date)
        {
            return Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind(date, DateTimeKind.Utc)) ;
        }
    }
}
