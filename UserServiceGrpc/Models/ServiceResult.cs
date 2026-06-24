namespace UserServiceGrpc.Models
{
    // ─────────────────────────────────────────────────────────────────────────────
    // ServiceResult — lightweight result carrier returned by mutating service methods.
    // Decouples business outcomes from gRPC proto types so the service layer has
    // no dependency on generated protobuf classes.
    // ─────────────────────────────────────────────────────────────────────────────

    public sealed class ServiceResult
    {
        /// <summary>1 = success, 0 = business-rule failure, -1 = error.</summary>
        public int Status { get; init; }
        public string Message { get; init; } = string.Empty;

        // ── Factory helpers ───────────────────────────────────────────────────────

        public static ServiceResult Success(string message) => new() { Status = 1, Message = message };
        public static ServiceResult Failure(string message) => new() { Status = 0, Message = message };
        public static ServiceResult Error(string message = "Error") => new() { Status = -1, Message = message };

        /// <summary>Combines multiple failure reasons with " | " separator.</summary>
        public static ServiceResult Failures(IEnumerable<string> reasons) =>
            Failure(string.Join(" | ", reasons));
    }
}
