namespace Compass.Api.Services;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string NoReplyAddress { get; set; } = string.Empty;
    public string SupportAddress { get; set; } = string.Empty;
    public string NotificationsAddress { get; set; } = string.Empty;
}