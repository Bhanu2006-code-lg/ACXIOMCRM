using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Activity
{
    public int ActivityId { get; set; }

    [Required, StringLength(20)]
    public string ActivityType { get; set; } = "Call"; // Call, Meeting, Email, Task

    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateTime ActivityDate { get; set; }

    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }

    public string? AssignedTo { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Planned"; // Planned, Completed, Cancelled

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }

    [ForeignKey("CustomerId")]
    public Customer? Customer { get; set; }

    [ForeignKey("LeadId")]
    public Lead? Lead { get; set; }

    [ForeignKey("AssignedTo")]
    public AppUser? AssignedUser { get; set; }
}
