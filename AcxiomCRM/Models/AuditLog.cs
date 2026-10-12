using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class AuditLog
{
    public int AuditLogId { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }

    [Required, StringLength(50)]
    public string Action { get; set; } = string.Empty; // Login, Logout, Create, Update, Delete, FailedLogin, Lockout, RoleChange, PasswordChange

    [Required, StringLength(50)]
    public string EntityName { get; set; } = string.Empty;

    public string? RecordId { get; set; }

    [StringLength(2000)]
    public string? OldValue { get; set; }

    [StringLength(2000)]
    public string? NewValue { get; set; }

    public string? Result { get; set; } // Success, Failure

    [StringLength(45)]
    public string? IpAddress { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
