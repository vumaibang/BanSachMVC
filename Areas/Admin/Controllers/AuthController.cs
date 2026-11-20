using System.ComponentModel.DataAnnotations;
using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BanSachMVC.Areas.Admin.Controllers;

[Route("Admin")]
public class AuthController(AccountService accounts) : AdminController
{
    [AllowAnonymous, HttpGet("Login")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return User.IsInRole("Admin") ? RedirectToAction("Index", "Dashboard") : RedirectToAction("Index", "Home", new { area = "" });
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost("Login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            var account = await accounts.LoginAsync(model.Username, model.Password);
            if (account?.Role != "Admin") throw new ValidationException("Tên đăng nhập hoặc mật khẩu không đúng cho khu vực này.");
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CurrentUser.Principal(account));
            if (Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl!);
            return RedirectToAction("Index", "Dashboard");
        }
        catch (ValidationException ex) { ModelState.AddModelError(string.Empty, ex.Message); return View(model); }
    }

    [HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home", new { area = "" });
    }
}
