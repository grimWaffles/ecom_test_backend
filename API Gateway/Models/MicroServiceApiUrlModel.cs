namespace API_Gateway.Models
{
    public class MicroServiceUrl
    {
        private string Mode { get; set; }
        private ApiUrlModel LocalUrls { get; set; }
        private ApiUrlModel DockerUrls { get; set; }

        private string GetMode() => this.Mode;
        public string GetUserServiceUrl() => this.GetMode() == "Local" ? this.LocalUrls.UserService : this.DockerUrls.UserService;
        public string GetProductServiceUrl() => this.GetMode() == "Local" ? this.LocalUrls.ProductService : this.DockerUrls.ProductService;
        public string GetOrderServiceUrl() => this.GetMode() == "Local" ? this.LocalUrls.OrderService : this.DockerUrls.OrderService;
    }

    public class ApiUrlModel
    {
        public string UserService { get; set; }
        public string ProductService { get; set; }
        public string OrderService { get; set; }
    }
}
