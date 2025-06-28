// Compass.Data/Entities/Invoice.cs
using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class Invoice
{
    public Guid InvoiceId { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid SubscriptionId { get; set; }

    [Required]
    [StringLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    [StringLength(3)]
    public string Currency { get; set; } = "USD";

    public decimal TaxAmount { get; set; } = 0;
    public decimal TotalAmount { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = "Draft"; // Draft, Sent, Paid, Overdue, Cancelled

    public DateTime? BillingPeriodStart { get; set; }
    public DateTime? BillingPeriodEnd { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }

    [StringLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;

    [StringLength(255)]
    public string PaymentReference { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Customer Customer { get; set; } = null!;
    public Subscription Subscription { get; set; } = null!;
}