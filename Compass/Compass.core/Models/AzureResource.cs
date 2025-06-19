namespace Compass.Core.Models
{
    public class AzureResource
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string SubscriptionId { get; set; } = string.Empty;
        public Dictionary<string, string> Tags { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
        public DateTime CreatedDate { get; set; }
    }

    public class ResourceInventory
    {
        public Guid CustomerId { get; set; }
        public List<AzureResource> Resources { get; set; } = new();
        public DateTime CollectedDate { get; set; }
        public List<string> SubscriptionIds { get; set; } = new();
    }
}