using API_Gateway.Grpc;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API_Gateway.Controllers
{
    [ApiController]
    [Route("api/transactions")]
    public class CustomerTransactionController : ControllerBase
    {
        private readonly ICustomerTransactionGrpcClient _grpcClient;

        public CustomerTransactionController(ICustomerTransactionGrpcClient grpcClient)
        {
            _grpcClient = grpcClient;
        }

        private int UserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        [HttpGet]
        [Route("get/{id}")]
        public async Task<IActionResult> GetTransactionById(int id)
        {
            var request = new TransactionRequestSingle { Id = id, UserId = UserId };
            var response = await _grpcClient.GetTransactionByIdAsync(request);
            return Ok(response);
        }

        [HttpGet]
        [Route("all")]
        public async Task<IActionResult> GetAllTransactions([FromQuery] TransactionRequestMultiple request)
        {
            var response = await _grpcClient.GetAllTransactionsAsync(request);
            return Ok(response);
        }

        [HttpPost]
        [Route("create")]
        public async Task<IActionResult> AddTransaction([FromBody] TransactionDto dto)
        {
            dto.UserId = UserId;
            dto.CreatedBy = UserId;
            dto.CreatedDate = Timestamp.FromDateTime(DateTime.UtcNow);

            var response = await _grpcClient.AddTransactionAsync(dto);
            return Ok(response);
        }

        [HttpPut]
        [Route("update")]
        public async Task<IActionResult> UpdateTransaction([FromBody] TransactionDto dto)
        {
            dto.UserId = UserId;
            dto.ModifiedBy = UserId;
            dto.ModifiedDate = Timestamp.FromDateTime(DateTime.UtcNow);

            var response = await _grpcClient.UpdateTransactionAsync(dto);
            return Ok(response);
        }

        [HttpDelete]
        [Route("delete/{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            var request = new TransactionRequestSingle { Id = id, UserId = UserId };
            var response = await _grpcClient.DeleteTransactionAsync(request);
            return Ok(response);
        }

        [HttpGet]
        [Route("integration-test")]
        public async Task<IActionResult> TestCustomerTransactionServiceGrpc()
        {
            var response = await _grpcClient.TestCustomerTransactionServiceAsync(new Empty());
            return Ok(response);
        }
    }
}
