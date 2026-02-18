using Google.Protobuf.WellKnownTypes;

namespace OrderServiceGrpc.Helpers.cs
{
    public static class DateTimeHelper
    {
        public static DateTime ConvertTimestampToDateTime(Google.Protobuf.WellKnownTypes.Timestamp timestamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds((long)timestamp.Seconds).DateTime;
        }

        public static Timestamp ConvertDateTimeToTimestamp(DateTime dateTime)
        {
            return Timestamp.FromDateTimeOffset(new DateTimeOffset(dateTime.ToUniversalTime()));
        }
    }
}
