using API_Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiGateway.Protos;
using System.Security.Claims;
using System.Threading.Tasks;

[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly IProductGrpcClient _grpcClient;

    public ProductController(IProductGrpcClient grpcClient)
    {
        _grpcClient = grpcClient;
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !int.TryParse(claim.Value, out int userId))
            throw new UnauthorizedAccessException("User ID not found in token.");
        return userId;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateProduct([FromForm] ProductDto dto)
    {
        int userId = Convert.ToInt32(HttpContext.User.FindFirst("userId")?.Value);
        var response = await _grpcClient.CreateProductAsync(userId, dto);
        return response.Status == 1 ? Ok(response.Dto) : BadRequest(response.ErrorMessage);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProductById(int id)
    {
        var product = await _grpcClient.GetProductByIdAsync(id);
        return product != null && product.Id > 0 ? Ok(product) : NotFound();
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllProducts()
    {
        var products = await _grpcClient.GetAllProductsAsync();
        return Ok(products);
    }

    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdateProduct(int id, [FromForm] ProductDto dto)
    {
        int userId = Convert.ToInt32(HttpContext.User.FindFirst("userId")?.Value);
        dto.Id = id;
        var response = await _grpcClient.UpdateProductAsync(userId, dto);
        return response.Status == 1 ? Ok(response.Dto) : BadRequest(response.ErrorMessage);
    }

    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        int userId = Convert.ToInt32(HttpContext.User.FindFirst("userId")?.Value);
        var response = await _grpcClient.DeleteProductAsync(id, userId);
        return response.Status == 1 ? Ok() : BadRequest(response.ErrorMessage);
    }
}
