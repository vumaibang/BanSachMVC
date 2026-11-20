using System.ComponentModel.DataAnnotations;
using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BanSachMVC.Controllers;

[Route("User")]
public class AccountController(AccountService accounts) : StoreController
{
    [HttpGet("Login")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return SignedInHome();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("Login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            var account = await accounts.LoginAsync(model.Username, model.Password);
            if (account?.Role != "Customer") throw new ValidationException("Tên đăng nhập hoặc mật khẩu không đúng cho khu vực này.");
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CurrentUser.Principal(account));
            if (Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl!);
            return RedirectToAction("Index", "Home");
        }
        catch (ValidationException ex) { ModelState.AddModelError(string.Empty, ex.Message); return View(model); }
    }

    [HttpGet("Register")]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? SignedInHome() : View(new RegistrationViewModel());

    [HttpPost("Register")]
    public async Task<IActionResult> Register(RegistrationViewModel model)
    {
        if (!await TrySaveAsync(() => accounts.RegisterAsync(model.Input))) return View(model);
        TempData["RegisterSuccess"] = "Đăng ký thành công. Hãy đăng nhập để mua sách.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize, HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    private IActionResult SignedInHome() => User.IsInRole("Admin")
        ? RedirectToAction("Index", "Dashboard", new { area = "Admin" })
        : RedirectToAction("Index", "Home");
}
