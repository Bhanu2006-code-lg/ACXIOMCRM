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
public class OpportunityController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IAuditService _audit;

    public OpportunityController(AppDbContext db, UserManager<AppUser> users, IAuditService audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search, string? stage, string? status)
    {
        var userId = _users.GetUserId(User);
        var q = _db.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            q = q.Where(o => o.AssignedTo == userId);

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(o => o.OpportunityName.Contains(search) ||
                              (o.Customer != null && o.Customer.CustomerName.Contains(search)));
        if (!string.IsNullOrWhiteSpace(stage))
            q = q.Where(o => o.Stage == stage);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(o => o.Status == status);

        ViewBag.Search = search; ViewBag.Stage = stage; ViewBag.Status = status;
        return View(await q.OrderByDescending(o => o.CreatedDate).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadDropdowns();
        return View(new Opportunity { ExpectedCloseDate = DateTime.Today.AddDays(30) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity model)
    {
        // Business validation
        if (model.Amount <= 0)
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        if (model.Probability < 0 || model.Probability > 100)
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        if (model.ExpectedCloseDate.Date < DateTime.Today)
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");

        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return View(model);
        }

        model.CreatedBy = _users.GetUserId(User);
        model.CreatedDate = DateTime.Now;
        model.Status = "Open";

        _db.Opportunities.Add(model);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Opportunity", model.OpportunityId.ToString(), null, model.OpportunityName);
        TempData["Success"] = "Opportunity created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var opp = await GetAuthorizedOpp(id);
        if (opp == null) return Forbid();
        await LoadDropdowns();
        return View(opp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity model)
    {
        var existing = await GetAuthorizedOpp(id);
        if (existing == null) return Forbid();

        if (model.Amount <= 0)
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        if (model.Probability < 0 || model.Probability > 100)
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        if (model.Status == "Open" && model.ExpectedCloseDate.Date < DateTime.Today)
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");

        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return View(model);
        }

        var old = $"{existing.OpportunityName}|{existing.Stage}|{existing.Amount}";
        existing.OpportunityName = model.OpportunityName;
        existing.CustomerId = model.CustomerId;
        existing.LeadId = model.LeadId;
        existing.Stage = model.Stage;
        existing.Amount = model.Amount;
        existing.Probability = model.Probability;
        existing.ExpectedCloseDate = model.ExpectedCloseDate;
        existing.Status = model.Status;
        existing.AssignedTo = model.AssignedTo;
        existing.Notes = model.Notes;
        existing.ModifiedDate = DateTime.Now;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Opportunity", id.ToString(), old, $"{model.OpportunityName}|{model.Stage}");
        TempData["Success"] = "Opportunity updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var opp = await _db.Opportunities.Include(o => o.Customer).Include(o => o.Lead)
            .Include(o => o.AssignedUser).FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (opp == null) return NotFound();
        return View(opp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var opp = await _db.Opportunities.FindAsync(id);
        if (opp == null) return NotFound();
        opp.Status = "Lost";
        opp.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "Opportunity", id.ToString(), opp.OpportunityName, "Marked Lost");
        TempData["Success"] = "Opportunity marked as Lost.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<Opportunity?> GetAuthorizedOpp(int id)
    {
        var userId = _users.GetUserId(User);
        var o = await _db.Opportunities.FindAsync(id);
        if (o == null) return null;
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && o.AssignedTo != userId) return null;
        return o;
    }

    private async Task LoadDropdowns()
    {
        var salesUsers = await _users.GetUsersInRoleAsync("SalesExecutive");
        ViewBag.Users = new SelectList(salesUsers, "Id", "FullName");
        ViewBag.Customers = new SelectList(await _db.Customers.Where(c => c.Status == "Active").ToListAsync(), "CustomerId", "CustomerName");
        ViewBag.Leads = new SelectList(await _db.Leads.Where(l => l.Status == "Qualified").ToListAsync(), "LeadId", "LeadName");
        ViewBag.Stages = new SelectList(new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" });
        ViewBag.Statuses = new SelectList(new[] { "Open", "Won", "Lost" });
    }
}
