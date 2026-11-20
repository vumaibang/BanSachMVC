using System.ComponentModel.DataAnnotations;
using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BanSachMVC.Controllers;

[Route("User/Cart")]
public class CartController(CartService cart, OrderService orders, CatalogService catalog) : StoreController
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Admin")) return RedirectToAction("AccessDenied", "Home");
        return View(new CartViewModel { Items = await cart.GetAsync() });
    }

    [HttpPost("Add")]
    public async Task<IActionResult> Add(int productId, int quantity = 1, string? returnUrl = null)
    {
        await UpdateCartAsync(() => cart.AddAsync(productId, quantity), "Đã thêm sách vào giỏ hàng.");
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    [HttpPost("BuyNow")]
    public IActionResult BuyNow(int productId, int quantity = 1) =>
        RedirectToAction(nameof(Checkout), new { ProductId = productId, Quantity = quantity });

    [HttpPost("Update")]
    public async Task<IActionResult> Update(int productId, int quantity)
    {
        await UpdateCartAsync(async () =>
        {
            if (quantity < 1) throw new ValidationException("Số lượng phải lớn hơn 0.");
            await cart.UpdateAsync(productId, quantity);
        }, "Đã cập nhật giỏ hàng.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Remove")]
    public async Task<IActionResult> Remove(int productId)
    {
        await UpdateCartAsync(() => cart.UpdateAsync(productId, 0), "Đã xóa sách khỏi giỏ.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Clear")]
    public async Task<IActionResult> Clear()
    {
        await UpdateCartAsync(cart.ClearAsync, "Đã xóa toàn bộ giỏ hàng.");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "Customer"), HttpGet("Checkout")]
    public async Task<IActionResult> Checkout(int? productId, int quantity = 1)
    {
        var model = new CheckoutViewModel { ProductId = productId, Quantity = quantity };
        try
        {
            var profile = await orders.ProfileAsync();
            model.Input.ShippingAddress = profile.Address ?? "";
            model.Input.Phone = profile.Phone ?? "";
            await LoadCheckoutAsync(model);
        }
        catch (ValidationException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        return View(model);
    }

    [Authorize(Policy = "Customer"), HttpPost("Checkout")]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        try { await LoadCheckoutAsync(model); }
        catch (ValidationException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        var id = 0;
        if (!await TrySaveAsync(async () => id = await orders.CheckoutAsync(model.Input, model.ProductId, model.Quantity))) return View(model);
        TempData["OrderMessage"] = "Đặt hàng thành công! Nhà sách đã nhận đơn hàng của bạn.";
        return RedirectToAction("Details", "Orders", new { id });
    }

    private async Task LoadCheckoutAsync(CheckoutViewModel model)
    {
        if (model.ProductId.HasValue)
        {
            var product = await catalog.ProductAsync(model.ProductId.Value) ?? throw new ValidationException("Sách không còn kinh doanh.");
            if (model.Quantity <= 0 || model.Quantity > product.StockQuantity)
                throw new ValidationException("Số lượng vượt quá tồn kho hoặc không hợp lệ.");
            model.Items = [new() { ProductId = product.ProductId, ProductName = product.ProductName, Price = product.Price,
                Quantity = model.Quantity, StockQuantity = product.StockQuantity, ImageUrl = product.ImageUrl }];
        }
        else model.Items = await cart.GetAsync();
    }

    private async Task UpdateCartAsync(Func<Task> action, string message)
    {
        if (!ModelState.IsValid) { TempData["ErrorMessage"] = "Mã sách hoặc số lượng không hợp lệ."; return; }
        try { await action(); TempData["CartMessage"] = message; }
        catch (ValidationException ex) { TempData["ErrorMessage"] = ex.Message; }
    }
}
