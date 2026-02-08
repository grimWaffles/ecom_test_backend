namespace OrderServiceGrpc.Factory
{
    //Factory Method Example

    //The Basic design/blueprint that all payment methods will use
    public interface IPaymentProcessor
    {
        bool Validate(decimal amount);
        decimal CalculateBaseAmount(decimal weight);
        decimal ApplyServiceCharge(decimal amount);
        Task LogAsync(decimal amount, bool success);
        Task<(bool Success, string Message)> ProcessAsync(decimal amount);
    }

    //Define the logic that each payment provider will use
    public class CreditCardPaymentProvider : IPaymentProcessor
    {
        public bool Validate(decimal amount)
            => amount <= 100_000;

        public decimal CalculateBaseAmount(decimal weight)
            => weight * 5;

        public decimal ApplyServiceCharge(decimal amount)
            => amount + (amount * 0.03m);

        public async Task LogAsync(decimal amount, bool success)
        {
            await Task.Delay(50); // simulate DB log
        }

        public async Task<(bool Success, string Message)> ProcessAsync(decimal amount)
        {
            await Task.Delay(100); // simulate gateway call
            return (true, "Paid by Credit Card");
        }
    }

    public class BkashPaymentProvider : IPaymentProcessor
    {
        public bool Validate(decimal amount)
            => amount <= 50_000;

        public decimal CalculateBaseAmount(decimal weight)
            => weight * 15;

        public decimal ApplyServiceCharge(decimal amount)
            => amount + 25;

        public async Task LogAsync(decimal amount, bool success)
        {
            await Task.Delay(50);
        }

        public async Task<(bool Success, string Message)> ProcessAsync(decimal amount)
        {
            await Task.Delay(100);
            return (true, "Paid by Bkash");
        }
    }

    //This the class that each payment service will implement
    public abstract class PaymentService
    {
        protected abstract IPaymentProcessor CreateProcessor();

        public async Task<(bool Success, string Message)> ProcessPaymentAsync(decimal weight)
        {
            var processor = CreateProcessor();

            // Step 1: Calculate
            var baseAmount = processor.CalculateBaseAmount(weight);

            // Step 2: Apply charges
            var finalAmount = processor.ApplyServiceCharge(baseAmount);

            // Step 3: Validate
            if (!processor.Validate(finalAmount))
            {
                await processor.LogAsync(finalAmount, false);
                return (false, "Validation failed");
            }

            // Step 4: Process payment
            var result = await processor.ProcessAsync(finalAmount);

            // Step 5: Log
            await processor.LogAsync(finalAmount, result.Success);

            return result;
        }
    }

    //Creating the services that the clients will use
    public class BkashPaymentService : PaymentService
    {
        protected override IPaymentProcessor CreateProcessor()
            => new BkashPaymentProvider();
    }

    public class CreditCardPaymentService : PaymentService
    {
        protected override IPaymentProcessor CreateProcessor()
            => new CreditCardPaymentProvider();
    }
}
