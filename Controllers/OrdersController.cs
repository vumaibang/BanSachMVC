using System.ComponentModel.DataAnnotations;
using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BanSachMVC.Controllers;

[Authorize(Policy = "Customer"), Route("User/Orders")]
public class OrdersController(OrderService orders) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(OrdersViewModel model)
    {
        OrderListing.Fill(model, await orders.ListAsync());
        return View(model);
    }

    [HttpGet("Details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var order = await orders.FindAsync(id);
        return order == null ? NotFound() : View(new OrderViewModel { Order = order });
    }

    [HttpPost("Cancel/{id:int}")]
    public async Task<IActionResult> Cancel(int id)
    {
        try { await orders.ChangeStatusAsync(id, "Cancelled"); TempData["SuccessMessage"] = "Đã hủy đơn hàng và hoàn lại tồn kho."; }
        catch (ValidationException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
