using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Lead
{
    public int LeadId { get; set; }

    [Required, StringLength(10)]
    public string LeadCode { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LeadName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit mobile number.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    public string? CompanyName { get; set; }

    [Required, StringLength(50)]
    public string Source { get; set; } = string.Empty; // Website, Referral, Cold Call, Email, Social Media, Other

    [Required, StringLength(30)]
    public string Status { get; set; } = "New"; // New, Contacted, Qualified, Unqualified, Converted, Lost

    [StringLength(20)]
    public string Priority { get; set; } = "Medium"; // Low, Medium, High

    [Range(0, 10000000, ErrorMessage = "Expected value must be between 0 and 10,000,000.")]
    public decimal ExpectedValue { get; set; }

    public string? AssignedTo { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? ModifiedDate { get; set; }
    public string? CreatedBy { get; set; }

    public int? ConvertedCustomerId { get; set; }
    public bool IsConverted { get; set; }

    [ForeignKey("AssignedTo")]
    public AppUser? AssignedUser { get; set; }

    [ForeignKey("ConvertedCustomerId")]
    public Customer? ConvertedCustomer { get; set; }

    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
