namespace UserServiceGrpc.Helpers
{
    public static class TokenHelper
    {
        public static int GetUserIdFromToken(HttpContext httpContext)
        {
            try
            {
                string userIdClaim = httpContext.User.Claims.Where(x => x.Type == "UserId").First().Value ?? "";
                return Convert.ToInt32(userIdClaim);
            }
            catch (Exception e)
            {
                return -1;
            }
        }
    }
}
