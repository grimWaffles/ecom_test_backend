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
        private readonly IServiceProvider _serviceProvider;

        public JwtForwardingInterceptor(ILogger<JwtForwardingInterceptor> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            _logger.LogInformation($"Interceptor hit — Method: {context.Method.Name}, Request type: {typeof(TRequest).Name}");

            using(IServiceScope scope = _serviceProvider.CreateScope())
            {
                ITokenHelper tokenHelper = scope.ServiceProvider.GetRequiredService<ITokenHelper>();
                
                //Sends the User Auth Token from the original request
                string authHeadersFromToken = tokenHelper.GetUserToken() ?? "";

                if (authHeadersFromToken!="")
                {
                    Metadata headers = new Metadata();

                    headers.Add("Authorization", authHeadersFromToken);

                    CallOptions options = context.Options.WithHeaders(headers);

                    ClientInterceptorContext<TRequest, TResponse> newContext = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);

                    return continuation(request, newContext);
                }
            }

            return continuation(request, context);
        }
    }
}
