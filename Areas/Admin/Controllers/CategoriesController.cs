using BanSachMVC.Data;
using BanSachMVC.Models;
using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BanSachMVC.Areas.Admin.Controllers;

public class CategoriesController(AppDbContext db, AdminService admin) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(CategoryListViewModel model)
    {
        var query = db.Categories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(model.SearchTerm))
        {
            var keyword = model.SearchTerm.Trim();
            query = query.Where(x => x.CategoryName.Contains(keyword));
        }
        if (model.StatusFilter == "Active") query = query.Where(x => x.IsActive);
        else if (model.StatusFilter == "Inactive") query = query.Where(x => !x.IsActive);
        model.Total = await query.CountAsync();
        model.PageNumber = Math.Clamp(model.PageNumber, 1, model.TotalPages);
        model.Items = await query.OrderByDescending(x => x.CategoryId)
            .Skip((model.PageNumber - 1) * 20).Take(20).ToListAsync();
        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        CategoryEditViewModel model = new();
        return View(model);
    }

    [HttpPost]
    public Task<IActionResult> Create(CategoryEditViewModel model) => SaveAsync(0, model, "Create");

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await admin.FindAsync<Category>(id);
        if (item == null) return NotFound();
        return View(new CategoryEditViewModel
        {
            Id = id, Input = CategoryInput.From(item)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CategoryEditViewModel model)
    {
        if (id <= 0) return NotFound();
        return await SaveAsync(id, model, "Edit");
    }

    private async Task<IActionResult> SaveAsync(int id, CategoryEditViewModel model, string viewName)
    {
        model.Id = id;
        if (id != 0)
        {
            var existing = await admin.FindAsync<Category>(id);
            if (existing == null) return NotFound();
        }
        if (!await TrySaveAsync(async () =>
        {
            await admin.SaveAsync(model.Input.ToEntity(id), id);
        })) return View(viewName, model);
        TempData["SuccessMessage"] = "Đã lưu danh mục.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await admin.FindAsync<Category>(id);
        return item == null ? NotFound() : View(new DeleteViewModel<Category> { Item = item });
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await admin.FindAsync<Category>(id);
        if (item == null) return NotFound();
        if (!await TrySaveAsync(async () => TempData["SuccessMessage"] = await admin.DeleteAsync<Category>(id)))
            return View("Delete", new DeleteViewModel<Category> { Item = item });
        return RedirectToAction(nameof(Index));
    }
}
