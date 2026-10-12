using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;

    public DashboardController(AppDbContext db, UserManager<AppUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _users.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        var isManager = User.IsInRole("Manager");

        var vm = new DashboardViewModel();

        var custQ = _db.Customers.AsQueryable();
        var leadQ = _db.Leads.AsQueryable();
        var oppQ = _db.Opportunities.AsQueryable();
        var fuQ = _db.FollowUps.AsQueryable();

        if (!isAdmin && !isManager)
        {
            custQ = custQ.Where(c => c.AssignedTo == userId);
            leadQ = leadQ.Where(l => l.AssignedTo == userId);
            oppQ = oppQ.Where(o => o.AssignedTo == userId);
            fuQ = fuQ.Where(f => f.AssignedTo == userId);
        }

        vm.TotalCustomers = await custQ.CountAsync();
        vm.TotalLeads = await leadQ.CountAsync();
        vm.OpenLeads = await leadQ.CountAsync(l => l.Status != "Converted" && l.Status != "Lost");
        vm.TotalOpportunities = await oppQ.CountAsync();
        vm.OpenOpportunities = await oppQ.CountAsync(o => o.Status == "Open");
        vm.WonOpportunities = await oppQ.CountAsync(o => o.Status == "Won");
        vm.LostOpportunities = await oppQ.CountAsync(o => o.Status == "Lost");
        vm.TotalPipelineValue = await oppQ.Where(o => o.Status == "Open").SumAsync(o => (decimal?)o.Amount) ?? 0;
        vm.PendingFollowUps = await fuQ.CountAsync(f => f.Status == "Planned" && f.FollowUpDate >= DateTime.Today);
        vm.OverdueFollowUps = await fuQ.CountAsync(f => f.Status == "Planned" && f.FollowUpDate < DateTime.Today);

        // Lead status chart
        var leadStatuses = await leadQ.GroupBy(l => l.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        vm.LeadStatusChart = leadStatuses.ToDictionary(x => x.Key, x => x.Count);

        // Opportunity stage chart
        var stages = await oppQ.GroupBy(o => o.Stage).Select(g => new { g.Key, Total = g.Sum(o => o.Amount) }).ToListAsync();
        vm.OpportunityStageChart = stages.ToDictionary(x => x.Key, x => x.Total);

        // Monthly sales (last 6 months)
        var sixMonthsAgo = DateTime.Today.AddMonths(-5);
        var monthly = await oppQ
            .Where(o => o.Status == "Won" && o.ModifiedDate >= sixMonthsAgo)
            .GroupBy(o => new { o.ModifiedDate!.Value.Year, o.ModifiedDate.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(o => o.Amount) })
            .ToListAsync();
        vm.MonthlySalesChart = monthly.ToDictionary(
            x => $"{x.Year}-{x.Month:D2}",
            x => x.Total);

        return View(vm);
    }
}
