using AcxiomCRM.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class ReportsController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;

    public ReportsController(AppDbContext db, UserManager<AppUser> users)
    {
        _db = db;
        _users = users;
    }

    public IActionResult Index() => View();

    public async Task<IActionResult> Customers(string? status, DateTime? from, DateTime? to)
    {
        var q = _db.Customers.Include(c => c.AssignedUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => c.Status == status);
        if (from.HasValue) q = q.Where(c => c.CreatedDate >= from.Value);
        if (to.HasValue) q = q.Where(c => c.CreatedDate <= to.Value.AddDays(1));
        ViewBag.Status = status; ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(await q.OrderByDescending(c => c.CreatedDate).ToListAsync());
    }

    public async Task<IActionResult> Leads(string? status, string? source)
    {
        var q = _db.Leads.Include(l => l.AssignedUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(source)) q = q.Where(l => l.Source == source);
        ViewBag.Status = status; ViewBag.Source = source;
        return View(await q.OrderByDescending(l => l.CreatedDate).ToListAsync());
    }

    public async Task<IActionResult> Opportunities(string? stage, string? status)
    {
        var q = _db.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(stage)) q = q.Where(o => o.Stage == stage);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(o => o.Status == status);
        ViewBag.Stage = stage; ViewBag.Status = status;
        return View(await q.OrderByDescending(o => o.CreatedDate).ToListAsync());
    }

    public async Task<IActionResult> Pipeline()
    {
        var opps = await _db.Opportunities.Include(o => o.AssignedUser)
            .Where(o => o.Status == "Open").ToListAsync();

        ViewBag.ByStage = opps.GroupBy(o => o.Stage)
            .Select(g => new { Stage = g.Key, Total = g.Sum(o => o.Amount), Weighted = g.Sum(o => o.WeightedAmount), Count = g.Count() })
            .OrderBy(x => x.Stage).ToList();

        ViewBag.ByOwner = opps.GroupBy(o => o.AssignedUser?.FullName ?? "Unassigned")
            .Select(g => new { Owner = g.Key, Total = g.Sum(o => o.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Total).ToList();

        return View(opps);
    }

    public async Task<IActionResult> FollowUps(string? status)
    {
        var q = _db.FollowUps.Include(f => f.Customer).Include(f => f.Lead).Include(f => f.AssignedUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(f => f.Status == status);
        ViewBag.Status = status;
        ViewBag.OverdueCount = await _db.FollowUps.CountAsync(f => f.Status == "Planned" && f.FollowUpDate < DateTime.Today);
        return View(await q.OrderBy(f => f.FollowUpDate).ToListAsync());
    }

    public async Task<IActionResult> Conversion()
    {
        var leads = await _db.Leads.ToListAsync();
        ViewBag.Total = leads.Count;
        ViewBag.Converted = leads.Count(l => l.IsConverted);
        ViewBag.Lost = leads.Count(l => l.Status == "Lost");
        ViewBag.Open = leads.Count(l => l.Status != "Converted" && l.Status != "Lost");
        ViewBag.ConversionRate = leads.Count > 0 ? (double)leads.Count(l => l.IsConverted) / leads.Count * 100 : 0;
        return View(leads);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UserActivity()
    {
        var logs = await _db.AuditLogs
            .GroupBy(a => new { a.UserId, a.UserName })
            .Select(g => new { g.Key.UserId, g.Key.UserName, Count = g.Count() })
            .OrderByDescending(x => x.Count).ToListAsync();
        return View(logs);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Audit(string? action, string? entity, DateTime? from, DateTime? to)
    {
        var q = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(entity)) q = q.Where(a => a.EntityName == entity);
        if (from.HasValue) q = q.Where(a => a.CreatedDate >= from.Value);
        if (to.HasValue) q = q.Where(a => a.CreatedDate <= to.Value.AddDays(1));
        return View(await q.OrderByDescending(a => a.CreatedDate).Take(500).ToListAsync());
    }
}
