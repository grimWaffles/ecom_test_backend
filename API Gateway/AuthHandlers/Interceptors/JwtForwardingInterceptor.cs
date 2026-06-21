using Grpc.Core;
using Grpc.Core.Interceptors;

namespace API_Gateway.AuthHandlers.Interceptors
{
    public class JwtForwardingInterceptor : Interceptor
    {
        private readonly IHttpContextAccessor _httpContext;

        public JwtForwardingInterceptor(IHttpContextAccessor contextAccessor)
        {
            _httpContext = contextAccessor;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            string authHeadersFromToken = _httpContext.HttpContext.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            
            Metadata headers = new Metadata();

            headers.Add("Authorization", authHeadersFromToken);

            CallOptions options = context.Options.WithHeaders(headers);
            
            ClientInterceptorContext<TRequest, TResponse> newContext = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);

            return continuation(request, newContext);
        }
    }
}
