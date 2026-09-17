namespace OrderServiceGrpc.Models
{
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }

        public static ServiceResult<T> SuccessResult(T data, string? message = null) =>
            new ServiceResult<T> { Success = true, Data = data, Message = message };

        public static ServiceResult<T> Fail(string message) =>
            new ServiceResult<T> { Success = false, Message = message };
    }
}
