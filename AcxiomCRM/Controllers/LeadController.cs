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
public class LeadController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IAuditService _audit;

    public LeadController(AppDbContext db, UserManager<AppUser> users, IAuditService audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search, string? status)
    {
        var userId = _users.GetUserId(User);
        var q = _db.Leads.Include(l => l.AssignedUser).AsQueryable();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            q = q.Where(l => l.AssignedTo == userId);

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(l => l.LeadName.Contains(search) || l.Email.Contains(search) ||
                              (l.CompanyName != null && l.CompanyName.Contains(search)));

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(l => l.Status == status);

        ViewBag.Search = search;
        ViewBag.Status = status;
        return View(await q.OrderByDescending(l => l.CreatedDate).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadDropdowns();
        return View(new Lead());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead model)
    {
        var validStatuses = new[] { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" };
        if (!validStatuses.Contains(model.Status))
            ModelState.AddModelError("Status", "Invalid lead status.");

        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return View(model);
        }

        model.LeadCode = $"LEAD{(await _db.Leads.CountAsync() + 1):D4}";
        model.CreatedBy = _users.GetUserId(User);
        model.CreatedDate = DateTime.Now;

        _db.Leads.Add(model);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Lead", model.LeadId.ToString(), null, model.LeadName);
        TempData["Success"] = "Lead created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var lead = await GetAuthorizedLead(id);
        if (lead == null) return Forbid();
        await LoadDropdowns();
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead model)
    {
        var existing = await GetAuthorizedLead(id);
        if (existing == null) return Forbid();

        // Prevent invalid status transitions
        var validTransitions = new Dictionary<string, string[]>
        {
            ["New"] = new[] { "New", "Contacted", "Lost" },
            ["Contacted"] = new[] { "Contacted", "Qualified", "Unqualified", "Lost" },
            ["Qualified"] = new[] { "Qualified", "Converted", "Lost" },
            ["Unqualified"] = new[] { "Unqualified", "Lost" },
            ["Converted"] = new[] { "Converted" },
            ["Lost"] = new[] { "Lost" }
        };

        if (validTransitions.TryGetValue(existing.Status, out var allowed) && !allowed.Contains(model.Status))
            ModelState.AddModelError("Status", $"Cannot transition from '{existing.Status}' to '{model.Status}'.");

        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return View(model);
        }

        var old = $"{existing.LeadName}|{existing.Status}";
        existing.LeadName = model.LeadName;
        existing.Email = model.Email;
        existing.Phone = model.Phone;
        existing.CompanyName = model.CompanyName;
        existing.Source = model.Source;
        existing.Status = model.Status;
        existing.Priority = model.Priority;
        existing.ExpectedValue = model.ExpectedValue;
        existing.AssignedTo = model.AssignedTo;
        existing.Notes = model.Notes;
        existing.ModifiedDate = DateTime.Now;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Lead", id.ToString(), old, $"{model.LeadName}|{model.Status}");
        TempData["Success"] = "Lead updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var lead = await _db.Leads.Include(l => l.AssignedUser).Include(l => l.ConvertedCustomer)
            .Include(l => l.FollowUps).Include(l => l.Activities).FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead == null) return NotFound();
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int id)
    {
        var lead = await GetAuthorizedLead(id);
        if (lead == null) return Forbid();
        if (lead.Status != "Qualified")
        {
            TempData["Error"] = "Only Qualified leads can be converted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var customer = new Customer
        {
            CustomerCode = $"CUST{(await _db.Customers.CountAsync() + 1):D4}",
            CustomerName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Status = "Active",
            AssignedTo = lead.AssignedTo,
            CreatedBy = _users.GetUserId(User),
            CreatedDate = DateTime.Now
        };

        // Check for duplicate before converting
        if (await _db.Customers.AnyAsync(c => c.Email == lead.Email))
        {
            TempData["Error"] = "A customer with this email already exists.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        lead.Status = "Converted";
        lead.IsConverted = true;
        lead.ConvertedCustomerId = customer.CustomerId;
        lead.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Convert", "Lead", id.ToString(), "Qualified", $"Customer:{customer.CustomerId}");
        TempData["Success"] = "Lead converted to customer successfully.";
        return RedirectToAction("Details", "Customer", new { id = customer.CustomerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _db.Leads.FindAsync(id);
        if (lead == null) return NotFound();
        lead.Status = "Lost";
        lead.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "Lead", id.ToString(), lead.LeadName, "Marked Lost");
        TempData["Success"] = "Lead marked as Lost.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<Lead?> GetAuthorizedLead(int id)
    {
        var userId = _users.GetUserId(User);
        var l = await _db.Leads.FindAsync(id);
        if (l == null) return null;
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && l.AssignedTo != userId) return null;
        return l;
    }

    private async Task LoadDropdowns()
    {
        var salesRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "SalesExecutive");
        var salesUsers = salesRole == null ? new List<AppUser>() :
            await _db.Users.Where(u => _db.UserRoles
                .Where(ur => ur.RoleId == salesRole.Id)
                .Select(ur => ur.UserId).Contains(u.Id) && u.IsActive).ToListAsync();
        ViewBag.Users = new SelectList(salesUsers, "Id", "FullName");
        ViewBag.Sources = new SelectList(new[] { "Website", "Referral", "Cold Call", "Email", "Social Media", "Other" });
        ViewBag.Statuses = new SelectList(new[] { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" });
        ViewBag.Priorities = new SelectList(new[] { "Low", "Medium", "High" });
    }
}
