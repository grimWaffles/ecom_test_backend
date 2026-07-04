using API_Gateway.Helpers;
using ApiGateway.Protos;
using Azure.Core;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client.Configuration;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace API_Gateway.Interceptors
{
    public class JwtForwardingInterceptor : Interceptor
    {
        private readonly ILogger<JwtForwardingInterceptor> _logger;
        private readonly ITokenHelper _tokenHelper;

        public JwtForwardingInterceptor(ILogger<JwtForwardingInterceptor> logger, ITokenHelper tokenHelper)
        {
            _logger = logger;
            _tokenHelper = tokenHelper;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            _logger.LogInformation($"Interceptor hit — Method: {context.Method.Name}, Request type: {typeof(TRequest).Name}");

            //Sends the Service Auth Token from the RequirePermission filter
            Metadata? authHeadersFromHttpContext = _tokenHelper.GetGrpcHeaders();

            if (authHeadersFromHttpContext != null)
            {
                CallOptions options = context.Options.WithHeaders(authHeadersFromHttpContext);

                ClientInterceptorContext<TRequest, TResponse> newContext = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);

                return continuation(request, newContext);
            }

            return continuation(request, context);
        }
    }
}
