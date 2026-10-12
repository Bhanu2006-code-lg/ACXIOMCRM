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
[Route("api/leads")]
[Authorize]
public class LeadsApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IAuditService _audit;

    public LeadsApiController(AppDbContext db, UserManager<AppUser> users, IAuditService audit)
    {
        _db = db; _users = users; _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var userId = _users.GetUserId(User);
        var q = _db.Leads.Include(l => l.AssignedUser).AsQueryable();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            q = q.Where(l => l.AssignedTo == userId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.Status == status);
        return Ok(await q.Select(l => new LeadDto
        {
            LeadId = l.LeadId, LeadCode = l.LeadCode, LeadName = l.LeadName,
            Email = l.Email, Phone = l.Phone, CompanyName = l.CompanyName,
            Source = l.Source, Status = l.Status, Priority = l.Priority,
            ExpectedValue = l.ExpectedValue,
            AssignedUserName = l.AssignedUser != null ? l.AssignedUser.FullName : null,
            CreatedDate = l.CreatedDate
        }).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LeadCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var validStatuses = new[] { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" };
        if (!validStatuses.Contains(dto.Status))
            return BadRequest(new { message = "Invalid lead status." });

        var lead = new Lead
        {
            LeadCode = $"LEAD{(await _db.Leads.CountAsync() + 1):D4}",
            LeadName = dto.LeadName, Email = dto.Email, Phone = dto.Phone,
            CompanyName = dto.CompanyName, Source = dto.Source, Status = dto.Status,
            Priority = dto.Priority, ExpectedValue = dto.ExpectedValue,
            AssignedTo = dto.AssignedTo, Notes = dto.Notes,
            CreatedBy = _users.GetUserId(User), CreatedDate = DateTime.Now
        };
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Lead", lead.LeadId.ToString(), null, lead.LeadName);
        return CreatedAtAction(nameof(GetAll), new { id = lead.LeadId }, new { lead.LeadId, lead.LeadCode });
    }
}

[ApiController]
[Route("api/opportunities")]
[Authorize]
public class OpportunitiesApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IAuditService _audit;

    public OpportunitiesApiController(AppDbContext db, UserManager<AppUser> users, IAuditService audit)
    {
        _db = db; _users = users; _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? stage)
    {
        var userId = _users.GetUserId(User);
        var q = _db.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            q = q.Where(o => o.AssignedTo == userId);
        if (!string.IsNullOrWhiteSpace(stage)) q = q.Where(o => o.Stage == stage);
        return Ok(await q.Select(o => new OpportunityDto
        {
            OpportunityId = o.OpportunityId, OpportunityName = o.OpportunityName,
            CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
            Stage = o.Stage, Amount = o.Amount, Probability = o.Probability,
            WeightedAmount = o.Amount * o.Probability / 100,
            ExpectedCloseDate = o.ExpectedCloseDate, Status = o.Status,
            AssignedUserName = o.AssignedUser != null ? o.AssignedUser.FullName : null
        }).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OpportunityCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (dto.Amount <= 0) return BadRequest(new { message = "Opportunity Amount must be greater than 0." });
        if (dto.Probability < 0 || dto.Probability > 100) return BadRequest(new { message = "Probability must be between 0 and 100." });
        if (dto.ExpectedCloseDate.Date < DateTime.Today) return BadRequest(new { message = "Expected Close Date cannot be in the past." });

        var opp = new Opportunity
        {
            OpportunityName = dto.OpportunityName, CustomerId = dto.CustomerId, LeadId = dto.LeadId,
            Stage = dto.Stage, Amount = dto.Amount, Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate, Status = "Open",
            AssignedTo = dto.AssignedTo, Notes = dto.Notes,
            CreatedBy = _users.GetUserId(User), CreatedDate = DateTime.Now
        };
        _db.Opportunities.Add(opp);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Opportunity", opp.OpportunityId.ToString(), null, opp.OpportunityName);
        return CreatedAtAction(nameof(GetAll), new { id = opp.OpportunityId }, new { opp.OpportunityId });
    }
}

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin,Manager")]
public class ReportsApiController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReportsApiController(AppDbContext db) => _db = db;

    [HttpGet("pipeline")]
    public async Task<IActionResult> Pipeline()
    {
        var data = await _db.Opportunities
            .Where(o => o.Status == "Open")
            .GroupBy(o => o.Stage)
            .Select(g => new { Stage = g.Key, Total = g.Sum(o => o.Amount), Count = g.Count() })
            .ToListAsync();
        return Ok(data);
    }
}
