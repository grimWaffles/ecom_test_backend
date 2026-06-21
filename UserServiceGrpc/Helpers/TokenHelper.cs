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

        public static int GetRoleIdFromToken(HttpContext httpContext)
        {
            try
            {
                string roleIdClaim = httpContext.User.Claims.Where(x => x.Type == "RoleId").First().Value ?? "";
                return Convert.ToInt32(roleIdClaim);
            }
            catch (Exception e)
            {
                return -1;
            }
        }
    }
}
