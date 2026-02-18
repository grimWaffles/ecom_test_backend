namespace API_Gateway.Models
{
    public class MicroServiceUrl
    {
        public string Mode { get; set; }
        public ApiUrlModel LocalUrls { get; set; }
        public ApiUrlModel DockerUrls { get; set; }

        private string GetMode() => this.Mode;
        public string GetUserServiceUrl() => this.GetMode().ToLower() == "local" ? this.LocalUrls.UserService : this.DockerUrls.UserService;
        public string GetProductServiceUrl() => this.GetMode().ToLower() == "local" ? this.LocalUrls.ProductService : this.DockerUrls.ProductService;
        public string GetOrderServiceUrl() => this.GetMode().ToLower() == "local" ? this.LocalUrls.OrderService : this.DockerUrls.OrderService;
    }
}
