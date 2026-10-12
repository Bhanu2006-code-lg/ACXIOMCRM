using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class FollowUpController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IAuditService _audit;

    public FollowUpController(AppDbContext db, UserManager<AppUser> users, IAuditService audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? status, DateTime? date)
    {
        var userId = _users.GetUserId(User);
        var q = _db.FollowUps.Include(f => f.Customer).Include(f => f.Lead)
            .Include(f => f.AssignedUser).AsQueryable();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            q = q.Where(f => f.AssignedTo == userId);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(f => f.Status == status);
        if (date.HasValue)
            q = q.Where(f => f.FollowUpDate.Date == date.Value.Date);

        ViewBag.Status = status;
        ViewBag.OverdueCount = await q.CountAsync(f => f.Status == "Planned" && f.FollowUpDate < DateTime.Today);
        return View(await q.OrderBy(f => f.FollowUpDate).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadDropdowns();
        return View(new FollowUp { FollowUpDate = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp model)
    {
        // Business: follow-up date cannot be earlier than today for new/planned
        if (model.Status == "Planned" && model.FollowUpDate.Date < DateTime.Today)
            ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");

        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return View(model);
        }

        model.CreatedBy = _users.GetUserId(User);
        model.CreatedDate = DateTime.Now;
        _db.FollowUps.Add(model);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "FollowUp", model.FollowUpId.ToString(), null, model.Subject);
        TempData["Success"] = "Follow-up scheduled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var fu = await GetAuthorizedFollowUp(id);
        if (fu == null) return Forbid();
        await LoadDropdowns();
        return View(fu);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUp model)
    {
        var existing = await GetAuthorizedFollowUp(id);
        if (existing == null) return Forbid();

        if (model.Status == "Planned" && model.FollowUpDate.Date < DateTime.Today)
            ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");

        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return View(model);
        }

        var old = $"{existing.Subject}|{existing.Status}";
        existing.FollowUpDate = model.FollowUpDate;
        existing.Subject = model.Subject;
        existing.FollowUpType = model.FollowUpType;
        existing.Status = model.Status;
        existing.Remarks = model.Remarks;
        existing.AssignedTo = model.AssignedTo;
        existing.CustomerId = model.CustomerId;
        existing.LeadId = model.LeadId;
        existing.OpportunityId = model.OpportunityId;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "FollowUp", id.ToString(), old, $"{model.Subject}|{model.Status}");
        TempData["Success"] = "Follow-up updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var fu = await GetAuthorizedFollowUp(id);
        if (fu == null) return Forbid();
        fu.Status = "Completed";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "FollowUp", id.ToString(), "Planned", "Completed");
        TempData["Success"] = "Follow-up marked as completed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var fu = await _db.FollowUps.FindAsync(id);
        if (fu == null) return NotFound();
        fu.Status = "Cancelled";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "FollowUp", id.ToString(), fu.Subject, "Cancelled");
        TempData["Success"] = "Follow-up cancelled.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<FollowUp?> GetAuthorizedFollowUp(int id)
    {
        var userId = _users.GetUserId(User);
        var f = await _db.FollowUps.FindAsync(id);
        if (f == null) return null;
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && f.AssignedTo != userId) return null;
        return f;
    }

    private async Task LoadDropdowns()
    {
        var salesRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "SalesExecutive");
        var salesUsers = salesRole == null ? new List<AppUser>() :
            await _db.Users.Where(u => _db.UserRoles
                .Where(ur => ur.RoleId == salesRole.Id)
                .Select(ur => ur.UserId).Contains(u.Id) && u.IsActive).ToListAsync();
        ViewBag.Users = new SelectList(salesUsers, "Id", "FullName");
        ViewBag.Customers = new SelectList(await _db.Customers.Where(c => c.Status == "Active").ToListAsync(), "CustomerId", "CustomerName");
        ViewBag.Leads = new SelectList(await _db.Leads.Where(l => l.Status != "Converted" && l.Status != "Lost").ToListAsync(), "LeadId", "LeadName");
        ViewBag.Opportunities = new SelectList(await _db.Opportunities.Where(o => o.Status == "Open").ToListAsync(), "OpportunityId", "OpportunityName");
        ViewBag.Types = new SelectList(new[] { "Call", "Email", "Meeting", "Visit" });
        ViewBag.Statuses = new SelectList(new[] { "Planned", "Completed", "Missed", "Cancelled" });
    }
}
