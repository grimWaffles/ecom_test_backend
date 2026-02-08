namespace API_Gateway.Helpers
{
    public static class CustomConverters
    {
        public static Google.Protobuf.WellKnownTypes.Timestamp ConvertDateTimeToGoogleTimeStamp(DateTime date)
        {
            return Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind(date, DateTimeKind.Utc)) ;
        }
    }
}
