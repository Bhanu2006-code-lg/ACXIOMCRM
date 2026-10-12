using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class FollowUp
{
    public int FollowUpId { get; set; }

    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }

    [Required]
    public DateTime FollowUpDate { get; set; }

    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string FollowUpType { get; set; } = "Call"; // Call, Email, Meeting, Visit

    [Required, StringLength(20)]
    public string Status { get; set; } = "Planned"; // Planned, Completed, Missed, Cancelled

    [StringLength(500)]
    public string? Remarks { get; set; }

    public string? AssignedTo { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }

    [ForeignKey("CustomerId")]
    public Customer? Customer { get; set; }

    [ForeignKey("LeadId")]
    public Lead? Lead { get; set; }

    [ForeignKey("OpportunityId")]
    public Opportunity? Opportunity { get; set; }

    [ForeignKey("AssignedTo")]
    public AppUser? AssignedUser { get; set; }
}
