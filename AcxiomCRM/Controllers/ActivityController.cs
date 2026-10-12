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
public class ActivityController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IAuditService _audit;

    public ActivityController(AppDbContext db, UserManager<AppUser> users, IAuditService audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? type, string? status)
    {
        var userId = _users.GetUserId(User);
        var q = _db.Activities.Include(a => a.Customer).Include(a => a.Lead)
            .Include(a => a.AssignedUser).AsQueryable();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            q = q.Where(a => a.AssignedTo == userId);

        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(a => a.ActivityType == type);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.Status == status);

        ViewBag.Type = type; ViewBag.Status = status;
        return View(await q.OrderByDescending(a => a.ActivityDate).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadDropdowns();
        return View(new Activity { ActivityDate = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity model)
    {
        if (!ModelState.IsValid) { await LoadDropdowns(); return View(model); }

        model.CreatedBy = _users.GetUserId(User);
        model.CreatedDate = DateTime.Now;
        _db.Activities.Add(model);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Activity", model.ActivityId.ToString(), null, model.Subject);
        TempData["Success"] = "Activity logged.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var a = await _db.Activities.FindAsync(id);
        if (a == null) return NotFound();
        await LoadDropdowns();
        return View(a);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Activity model)
    {
        if (!ModelState.IsValid) { await LoadDropdowns(); return View(model); }
        var existing = await _db.Activities.FindAsync(id);
        if (existing == null) return NotFound();

        existing.ActivityType = model.ActivityType;
        existing.Subject = model.Subject;
        existing.Description = model.Description;
        existing.ActivityDate = model.ActivityDate;
        existing.CustomerId = model.CustomerId;
        existing.LeadId = model.LeadId;
        existing.AssignedTo = model.AssignedTo;
        existing.Status = model.Status;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Activity", id.ToString(), null, model.Subject);
        TempData["Success"] = "Activity updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await _db.Activities.FindAsync(id);
        if (a == null) return NotFound();
        _db.Activities.Remove(a);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "Activity", id.ToString(), a.Subject, "Deleted");
        TempData["Success"] = "Activity deleted.";
        return RedirectToAction(nameof(Index));
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
        ViewBag.Types = new SelectList(new[] { "Call", "Meeting", "Email", "Task" });
        ViewBag.Statuses = new SelectList(new[] { "Planned", "Completed", "Cancelled" });
    }
}
