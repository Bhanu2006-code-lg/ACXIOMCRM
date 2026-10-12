using AcxiomCRM.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class AuditLogController : Controller
{
    private readonly AppDbContext _db;

    public AuditLogController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? user, string? action, string? entity, DateTime? from, DateTime? to)
    {
        var q = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(user)) q = q.Where(a => a.UserName != null && a.UserName.Contains(user));
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(entity)) q = q.Where(a => a.EntityName == entity);
        if (from.HasValue) q = q.Where(a => a.CreatedDate >= from.Value);
        if (to.HasValue) q = q.Where(a => a.CreatedDate <= to.Value.AddDays(1));

        ViewBag.User = user; ViewBag.Action = action; ViewBag.Entity = entity;
        ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.Actions = new[] { "Login", "Logout", "FailedLogin", "Lockout", "Register", "Create", "Update", "Delete", "Convert", "RoleChange", "PasswordChange", "Unlock" };
        ViewBag.Entities = new[] { "Auth", "Customer", "Lead", "Opportunity", "FollowUp", "Activity", "User" };

        return View(await q.OrderByDescending(a => a.CreatedDate).Take(500).ToListAsync());
    }
}
