using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IAuditService _audit;

    public CustomersApiController(AppDbContext db, UserManager<AppUser> users, IAuditService audit)
    {
        _db = db; _users = users; _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var userId = _users.GetUserId(User);
        var q = _db.Customers.Include(c => c.AssignedUser).AsQueryable();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            q = q.Where(c => c.AssignedTo == userId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.CustomerName.Contains(search) || c.Email.Contains(search));
        return Ok(await q.Select(c => new CustomerDto
        {
            CustomerId = c.CustomerId, CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName, Email = c.Email, Phone = c.Phone,
            CompanyName = c.CompanyName, City = c.City, Status = c.Status,
            AssignedUserName = c.AssignedUser != null ? c.AssignedUser.FullName : null,
            CreatedDate = c.CreatedDate
        }).ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _db.Customers.Include(x => x.AssignedUser).FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c == null) return NotFound(new { message = "Customer not found." });
        var userId = _users.GetUserId(User);
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && c.AssignedTo != userId) return Forbid();
        return Ok(new CustomerDto
        {
            CustomerId = c.CustomerId, CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName, Email = c.Email, Phone = c.Phone,
            CompanyName = c.CompanyName, City = c.City, Status = c.Status,
            AssignedUserName = c.AssignedUser?.FullName, CreatedDate = c.CreatedDate
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (await _db.Customers.AnyAsync(c => c.Email == dto.Email))
            return Conflict(new { message = "A customer with this email already exists." });
        if (await _db.Customers.AnyAsync(c => c.Phone == dto.Phone))
            return Conflict(new { message = "A customer with this phone already exists." });

        var customer = new Customer
        {
            CustomerCode = $"CUST{(await _db.Customers.CountAsync() + 1):D4}",
            CustomerName = dto.CustomerName, Email = dto.Email, Phone = dto.Phone,
            CompanyName = dto.CompanyName, Address = dto.Address, City = dto.City,
            State = dto.State, AssignedTo = dto.AssignedTo, Notes = dto.Notes,
            Status = "Active", CreatedBy = _users.GetUserId(User), CreatedDate = DateTime.Now
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Customer", customer.CustomerId.ToString(), null, customer.CustomerName);
        return CreatedAtAction(nameof(GetById), new { id = customer.CustomerId }, new { customer.CustomerId, customer.CustomerCode });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = "Customer not found." });
        var userId = _users.GetUserId(User);
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && customer.AssignedTo != userId) return Forbid();
        if (await _db.Customers.AnyAsync(c => c.Email == dto.Email && c.CustomerId != id))
            return Conflict(new { message = "Email already in use." });
        if (await _db.Customers.AnyAsync(c => c.Phone == dto.Phone && c.CustomerId != id))
            return Conflict(new { message = "Phone already in use." });

        customer.CustomerName = dto.CustomerName; customer.Email = dto.Email;
        customer.Phone = dto.Phone; customer.CompanyName = dto.CompanyName;
        customer.Address = dto.Address; customer.City = dto.City;
        customer.State = dto.State; customer.AssignedTo = dto.AssignedTo;
        customer.Notes = dto.Notes; customer.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Customer", id.ToString(), null, dto.CustomerName);
        return Ok(new { message = "Customer updated." });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = "Customer not found." });
        customer.Status = "Inactive"; customer.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "Customer", id.ToString(), customer.CustomerName, "Deactivated");
        return Ok(new { message = "Customer deactivated." });
    }
}
