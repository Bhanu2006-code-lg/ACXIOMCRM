using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin")]
public class UserManagementController : Controller
{
    private readonly UserManager<AppUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly IAuditService _audit;

    public UserManagementController(UserManager<AppUser> users, RoleManager<IdentityRole> roles, IAuditService audit)
    {
        _users = users;
        _roles = roles;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var allUsers = string.IsNullOrWhiteSpace(search)
            ? _users.Users.ToList()
            : _users.Users.Where(u => u.FullName.Contains(search) || u.Email!.Contains(search)).ToList();

        var list = new List<UserListViewModel>();
        foreach (var u in allUsers)
        {
            var roles = await _users.GetRolesAsync(u);
            list.Add(new UserListViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? "",
                Role = roles.FirstOrDefault() ?? "None",
                IsActive = u.IsActive,
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                CreatedDate = u.CreatedDate
            });
        }
        ViewBag.Search = search;
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        var roles = await _users.GetRolesAsync(user);
        return View(new UserEditViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            Role = roles.FirstOrDefault() ?? "",
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _users.FindByIdAsync(model.Id);
        if (user == null) return NotFound();

        var oldRole = (await _users.GetRolesAsync(user)).FirstOrDefault() ?? "";
        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.IsActive = model.IsActive;

        await _users.UpdateAsync(user);

        if (oldRole != model.Role)
        {
            if (!string.IsNullOrEmpty(oldRole)) await _users.RemoveFromRoleAsync(user, oldRole);
            await _users.AddToRoleAsync(user, model.Role);
            await _audit.LogAsync("RoleChange", "User", user.Id, oldRole, model.Role);
        }

        await _audit.LogAsync("Update", "User", user.Id, null, model.FullName);
        TempData["Success"] = "User updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        await _audit.LogAsync("Unlock", "User", id, null, "Account unlocked");
        TempData["Success"] = "Account unlocked.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult ResetPassword(string id) => View(new ResetPasswordViewModel { UserId = id });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _users.FindByIdAsync(model.UserId);
        if (user == null) return NotFound();

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, model.NewPassword);
        if (result.Succeeded)
        {
            await _audit.LogAsync("PasswordChange", "User", user.Id, null, "Password reset by admin");
            TempData["Success"] = "Password reset successfully.";
            return RedirectToAction(nameof(Index));
        }
        foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
        return View(model);
    }
}
