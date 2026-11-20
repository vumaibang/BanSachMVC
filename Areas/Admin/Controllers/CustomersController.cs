using BanSachMVC.Data;
using BanSachMVC.Models;
using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BanSachMVC.Areas.Admin.Controllers;

public class CustomersController(AppDbContext db, AdminService admin) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(CustomerListViewModel model)
    {
        var query = db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(model.SearchTerm))
        {
            var keyword = model.SearchTerm.Trim();
            query = query.Where(x => x.FullName.Contains(keyword) || (x.Phone != null && x.Phone.Contains(keyword)) || (x.Email != null && x.Email.Contains(keyword)));
        }
        if (model.StatusFilter == "Active") query = query.Where(x => x.IsActive);
        else if (model.StatusFilter == "Inactive") query = query.Where(x => !x.IsActive);
        model.Total = await query.CountAsync();
        model.PageNumber = Math.Clamp(model.PageNumber, 1, model.TotalPages);
        model.Items = await query.OrderByDescending(x => x.CustomerId)
            .Skip((model.PageNumber - 1) * 20).Take(20).ToListAsync();
        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        CustomerEditViewModel model = new();
        return View(model);
    }

    [HttpPost]
    public Task<IActionResult> Create(CustomerEditViewModel model) => SaveAsync(0, model, "Create");

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await admin.FindAsync<Customer>(id);
        if (item == null) return NotFound();
        return View(new CustomerEditViewModel
        {
            Id = id, Input = CustomerInput.From(item)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CustomerEditViewModel model)
    {
        if (id <= 0) return NotFound();
        return await SaveAsync(id, model, "Edit");
    }

    private async Task<IActionResult> SaveAsync(int id, CustomerEditViewModel model, string viewName)
    {
        model.Id = id;
        if (id != 0)
        {
            var existing = await admin.FindAsync<Customer>(id);
            if (existing == null) return NotFound();
        }
        if (!await TrySaveAsync(async () =>
        {
            await admin.SaveAsync(model.Input.ToEntity(id), id);
        })) return View(viewName, model);
        TempData["SuccessMessage"] = "Đã lưu khách hàng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await admin.FindAsync<Customer>(id);
        return item == null ? NotFound() : View(new DeleteViewModel<Customer> { Item = item });
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await admin.FindAsync<Customer>(id);
        if (item == null) return NotFound();
        if (!await TrySaveAsync(async () => TempData["SuccessMessage"] = await admin.DeleteAsync<Customer>(id)))
            return View("Delete", new DeleteViewModel<Customer> { Item = item });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var item = await admin.FindAsync<Customer>(id);
        if (item == null) return NotFound();
        if (await TrySaveAsync(async () =>
        {
            item.IsActive = !item.IsActive;
            await admin.SaveAsync(item, id);
        })) TempData["SuccessMessage"] = "Đã cập nhật trạng thái khách hàng.";
        else TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return RedirectToAction(nameof(Index));
    }
}
