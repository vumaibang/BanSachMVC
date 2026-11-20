using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BanSachMVC.Controllers;

[Authorize(Policy = "Customer"), Route("User/Profile")]
public class ProfileController(OrderService orders) : StoreController
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => View(new ProfileViewModel { Input = CustomerInput.From(await orders.ProfileAsync()) });

    [HttpPost("")]
    public async Task<IActionResult> Index(ProfileViewModel model)
    {
        if (!await TrySaveAsync(() => orders.SaveProfileAsync(model.Input.ToEntity(0)))) return View(model);
        TempData["SuccessMessage"] = "Đã cập nhật thông tin cá nhân.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("ChangePassword")]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost("ChangePassword")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (model.Input.CurrentPassword == model.Input.NewPassword)
            ModelState.AddModelError(string.Empty, "Mật khẩu mới phải khác mật khẩu hiện tại.");
        if (!await TrySaveAsync(() => orders.ChangePasswordAsync(model.Input))) return View(model);
        await HttpContext.SignOutAsync();
        TempData["SuccessMessage"] = "Đã đổi mật khẩu. Vui lòng đăng nhập lại.";
        return RedirectToAction("Login", "Account");
    }
}
