using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace AcxiomCRM.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(string action, string entityName, string? recordId = null,
        string? oldValue = null, string? newValue = null, string? result = "Success")
    {
        var ctx = _http.HttpContext;
        var userId = ctx?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = ctx?.User?.FindFirstValue(ClaimTypes.Name);
        var ip = ctx?.Connection?.RemoteIpAddress?.ToString();

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            OldValue = oldValue,
            NewValue = newValue,
            Result = result,
            IpAddress = ip,
            CreatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
