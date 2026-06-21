using API_Gateway.AuthHandlers.PolicyProviders;
using API_Gateway.Services;
using ApiGateway.Protos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/product-categories")]
[Authorize]
public class ProductCategoryController : ControllerBase
{
    private readonly IProductCategoryGrpcClient _grpcClient;

    public ProductCategoryController(IProductCategoryGrpcClient grpcClient)
    {
        _grpcClient = grpcClient;
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier); // or "userId"
        if (claim == null || !int.TryParse(claim.Value, out int userId))
            throw new UnauthorizedAccessException("Invalid or missing user ID claim.");
        return userId;
    }

    [HttpPost("create")]
    [RequiresPermission("productCategory.create")]
    public async Task<IActionResult> CreateCategory([FromForm] ProductCategoryDto dto)
    {
        int userId = Convert.ToInt32(HttpContext.User.FindFirst("userId")?.Value);

        var response = await _grpcClient.CreateCategoryAsync(userId, dto);
        
        return response.Status == 1 ? Ok(response.Dto) : StatusCode(StatusCodes.Status500InternalServerError, new { message = response.ErrorMessage });
    }

    [HttpGet("{id}")]
    [RequiresPermission("productCategory.view")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        var dto = await _grpcClient.GetCategoryByIdAsync(id);
        return dto != null && dto.Id > 0 ? Ok(dto) : NotFound();
    }

    [HttpGet("all")]
    [RequiresPermission("productCategory.view")]
    public async Task<IActionResult> GetAllCategories()
    {
        var categories = await _grpcClient.GetAllCategoriesAsync();
        return Ok(categories);
    }

    [HttpPut("update/{id}")]
    [RequiresPermission("productCategory.update")]
    public async Task<IActionResult> UpdateCategory(int id, [FromForm] ProductCategoryDto dto)
    {
        int userId = Convert.ToInt32(HttpContext.User.FindFirst("userId")?.Value);

        if (dto.Id != id)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new {message = "Invalid information provided."});
        }
        var response = await _grpcClient.UpdateCategoryAsync(userId, dto);
        return response.Status == 1 ? Ok(response.Dto) : BadRequest(response.ErrorMessage);
    }

    [HttpDelete("delete/{id}")]
    [RequiresPermission("productCategory.delete")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        int userId = Convert.ToInt32(HttpContext.User.FindFirst("userId")?.Value);

        var response = await _grpcClient.DeleteCategoryAsync(id, userId);
        return response.Status == 1 ? Ok() : BadRequest(response.ErrorMessage);
    }
}
