using BanSachMVC.Data;
using BanSachMVC.Models;
using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BanSachMVC.Areas.Admin.Controllers;

public class AccountsController(AppDbContext db, AdminService admin) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(AccountListViewModel model)
    {
        var query = db.Accounts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(model.SearchTerm))
        {
            var keyword = model.SearchTerm.Trim();
            query = query.Where(x => x.Username.Contains(keyword));
        }
        if (model.RoleFilter is "Admin" or "Customer") query = query.Where(x => x.Role == model.RoleFilter);
        if (model.StatusFilter == "Active") query = query.Where(x => x.IsActive);
        else if (model.StatusFilter == "Inactive") query = query.Where(x => !x.IsActive);
        model.Total = await query.CountAsync();
        model.PageNumber = Math.Clamp(model.PageNumber, 1, model.TotalPages);
        model.Items = await query.OrderByDescending(x => x.AccountId)
            .Skip((model.PageNumber - 1) * 20).Take(20).ToListAsync();
        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        AccountEditViewModel model = new();
        return View(model);
    }

    [HttpPost]
    public Task<IActionResult> Create(AccountEditViewModel model) => SaveAsync(0, model, "Create");

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await admin.FindAsync<Account>(id);
        if (item == null) return NotFound();
        return View(new AccountEditViewModel
        {
            Id = id, Input = new() { AccountId = id, Username = item.Username, Role = item.Role, IsActive = item.IsActive }
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, AccountEditViewModel model)
    {
        if (id <= 0) return NotFound();
        return await SaveAsync(id, model, "Edit");
    }

    private async Task<IActionResult> SaveAsync(int id, AccountEditViewModel model, string viewName)
    {
        model.Id = id;
        if (id != 0)
        {
            var existing = await admin.FindAsync<Account>(id);
            if (existing == null) return NotFound();
        }
        if (!await TrySaveAsync(async () =>
        {
            model.Input.AccountId = id;
            await admin.SaveAccountAsync(model.Input);
        })) return View(viewName, model);
        TempData["SuccessMessage"] = "Đã lưu tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await admin.FindAsync<Account>(id);
        return item == null ? NotFound() : View(new DeleteViewModel<Account> { Item = item });
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await admin.FindAsync<Account>(id);
        if (item == null) return NotFound();
        if (!await TrySaveAsync(async () => TempData["SuccessMessage"] = await admin.DeleteAsync<Account>(id)))
            return View("Delete", new DeleteViewModel<Account> { Item = item });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var item = await admin.FindAsync<Account>(id);
        if (item == null) return NotFound();
        if (await TrySaveAsync(async () =>
        {
            await admin.SaveAccountAsync(new() { AccountId = id, Username = item.Username, Role = item.Role, IsActive = !item.IsActive });
        })) TempData["SuccessMessage"] = "Đã cập nhật trạng thái tài khoản.";
        else TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return RedirectToAction(nameof(Index));
    }
}
