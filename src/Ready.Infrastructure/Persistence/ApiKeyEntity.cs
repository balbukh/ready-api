using System.ComponentModel.DataAnnotations;

namespace Ready.Infrastructure.Persistence;

public class ApiKeyEntity
{
    public Guid Id { get; set; }
    
    [Required]
    public string Key { get; set; } = string.Empty;
    
    [Required]
    public string CustomerId { get; set; } = string.Empty;
    
    public string Label { get; set; } = string.Empty;
    
    public DateTimeOffset CreatedAt { get; set; }
}
