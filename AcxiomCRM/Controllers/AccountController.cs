using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<AppUser> _signIn;
    private readonly UserManager<AppUser> _users;
    private readonly IAuditService _audit;

    public AccountController(SignInManager<AppUser> signIn, UserManager<AppUser> users, IAuditService audit)
    {
        _signIn = signIn;
        _users = users;
        _audit = audit;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.FindByEmailAsync(model.Email);
        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError("", "Invalid credentials or account is inactive.");
            await _audit.LogAsync("FailedLogin", "Auth", null, null, model.Email, "Failure");
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _audit.LogAsync("Login", "Auth", user.Id, null, null, "Success");
            return LocalRedirect(returnUrl ?? "/Dashboard");
        }
        if (result.IsLockedOut)
        {
            await _audit.LogAsync("Lockout", "Auth", user.Id, null, null, "Failure");
            ModelState.AddModelError("", "Account locked due to multiple failed attempts. Try again in 15 minutes.");
            return View(model);
        }

        ModelState.AddModelError("", "Invalid email or password.");
        await _audit.LogAsync("FailedLogin", "Auth", user?.Id, null, model.Email, "Failure");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await _users.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _users.AddToRoleAsync(user, model.Role);
            await _audit.LogAsync("Register", "Auth", user.Id, null, $"Role:{model.Role}", "Success");
            await _signIn.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var e in result.Errors)
            ModelState.AddModelError("", e.Description);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = _users.GetUserId(User);
        await _audit.LogAsync("Logout", "Auth", userId, null, null, "Success");
        await _signIn.SignOutAsync();
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied() => View();
}
