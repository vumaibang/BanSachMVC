using System.ComponentModel.DataAnnotations;
using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BanSachMVC.Areas.Admin.Controllers;

public class OrdersController(OrderService orders) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(OrdersViewModel model)
    {
        OrderListing.Fill(model, await orders.ListAsync(true));
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await orders.FindAsync(id, true);
        return order == null ? NotFound() : View(new OrderViewModel { Order = order });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string newStatus)
    {
        try { await orders.ChangeStatusAsync(id, newStatus, true); TempData["SuccessMessage"] = "Đã cập nhật trạng thái đơn hàng."; }
        catch (ValidationException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
