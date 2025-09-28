namespace DocumentNotificationService.Models;

public class PortfolioOwner
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public OwnerType Type { get; set; }
    public string PortfolioId { get; set; } = string.Empty;
    
    // Contact-specific fields
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    // Account/Organization-specific fields
    public string OrganizationName { get; set; } = string.Empty;
    public string ContactPersonEmail { get; set; } = string.Empty;
}

public enum OwnerType
{
    Contact,    // Private person
    Account     // Organization
}