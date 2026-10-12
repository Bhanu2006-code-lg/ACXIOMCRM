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
public class CustomerController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IAuditService _audit;
    public CustomerController(AppDbContext db, UserManager<AppUser> users, IAuditService audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }
    public async Task<IActionResult> Index(string? search, string? status)
    {
        var userId = _users.GetUserId(User);
        var q = _db.Customers.Include(c => c.AssignedUser).AsQueryable();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            q = q.Where(c => c.AssignedTo == userId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.CustomerName.Contains(search) || c.Email.Contains(search) ||
                              c.Phone.Contains(search) || (c.CompanyName != null && c.CompanyName.Contains(search)));
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(c => c.Status == status);
        ViewBag.Search = search;
        ViewBag.Status = status;
        return View(await q.OrderByDescending(c => c.CreatedDate).ToListAsync());
    }
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadUsersDropdown();
        return View(new Customer());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer model)
    {
        // Business: duplicate email/phone
        if (await _db.Customers.AnyAsync(c => c.Email == model.Email))
            ModelState.AddModelError("Email", "A customer with this email already exists.");
        if (await _db.Customers.AnyAsync(c => c.Phone == model.Phone))
            ModelState.AddModelError("Phone", "A customer with this phone already exists.");
        if (!ModelState.IsValid)
        {
            await LoadUsersDropdown();
            return View(model);
        }
        model.CustomerCode = await GenerateCode("CUST");
        model.CreatedBy = _users.GetUserId(User);
        model.CreatedDate = DateTime.Now;
        _db.Customers.Add(model);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Customer", model.CustomerId.ToString(), null, model.CustomerName);
        TempData["Success"] = "Customer created successfully.";
        return RedirectToAction(nameof(Index));
    }
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await GetAuthorizedCustomer(id);
        if (customer == null) return Forbid();
        await LoadUsersDropdown();
        return View(customer);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer model)
    {
        var existing = await GetAuthorizedCustomer(id);
        if (existing == null) return Forbid();
        if (await _db.Customers.AnyAsync(c => c.Email == model.Email && c.CustomerId != id))
            ModelState.AddModelError("Email", "A customer with this email already exists.");
        if (await _db.Customers.AnyAsync(c => c.Phone == model.Phone && c.CustomerId != id))
            ModelState.AddModelError("Phone", "A customer with this phone already exists.");
        if (!ModelState.IsValid)
        {
            await LoadUsersDropdown();
            return View(model);
        }
        var old = $"{existing.CustomerName}|{existing.Status}";
        existing.CustomerName = model.CustomerName;
        existing.Email = model.Email;
        existing.Phone = model.Phone;
        existing.CompanyName = model.CompanyName;
        existing.Address = model.Address;
        existing.City = model.City;
        existing.State = model.State;
        existing.Status = model.Status;
        existing.AssignedTo = model.AssignedTo;
        existing.Notes = model.Notes;
        existing.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Customer", id.ToString(), old, model.CustomerName);
        TempData["Success"] = "Customer updated successfully.";
        return RedirectToAction(nameof(Index));
    }
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _db.Customers
            .Include(c => c.AssignedUser)
            .Include(c => c.Leads)
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer == null) return NotFound();
        return View(customer);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return NotFound();
        customer.Status = "Inactive";
        customer.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "Customer", id.ToString(), customer.CustomerName, "Deactivated");
        TempData["Success"] = "Customer deactivated.";
        return RedirectToAction(nameof(Index));
    }
    private async Task<Customer?> GetAuthorizedCustomer(int id)
    {
        var userId = _users.GetUserId(User);
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return null;
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && c.AssignedTo != userId) return null;
        return c;
    }
    private async Task LoadUsersDropdown()
    {
        var salesRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "SalesExecutive");
        if (salesRole != null)
        {
            var salesUserIds = await _db.UserRoles
                .Where(ur => ur.RoleId == salesRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync();
            var salesUsers = await _db.Users
                .Where(u => salesUserIds.Contains(u.Id) && u.IsActive)
                .ToListAsync();
            ViewBag.Users = new SelectList(salesUsers, "Id", "FullName");
        }
        else
        {
            ViewBag.Users = new SelectList(new List<AppUser>(), "Id", "FullName");
        }
    }
    private async Task<string> GenerateCode(string prefix)
    {
        var count = await _db.Customers.CountAsync() + 1;
        return $"{prefix}{count:D4}";
    }
}
