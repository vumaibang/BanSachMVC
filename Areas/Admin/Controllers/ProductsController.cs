using BanSachMVC.Data;
using BanSachMVC.Models;
using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BanSachMVC.Areas.Admin.Controllers;

public class ProductsController(AppDbContext db, AdminService admin) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(ProductListViewModel model)
    {
        model.Categories = await db.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToListAsync();
        var query = db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(model.SearchTerm))
        {
            var keyword = model.SearchTerm.Trim();
            query = query.Where(x => x.ProductName.Contains(keyword) || (x.Author != null && x.Author.Contains(keyword)));
        }
        if (model.CategoryId.HasValue) query = query.Where(x => x.CategoryId == model.CategoryId);
        if (model.StatusFilter == "Active") query = query.Where(x => x.IsActive);
        else if (model.StatusFilter == "Inactive") query = query.Where(x => !x.IsActive);
        model.Total = await query.CountAsync();
        model.PageNumber = Math.Clamp(model.PageNumber, 1, model.TotalPages);
        model.Items = await query.OrderByDescending(x => x.ProductId)
            .Skip((model.PageNumber - 1) * 20).Take(20).ToListAsync();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ProductEditViewModel model = new() { Categories = await admin.ListAsync<Category>() };
        return View(model);
    }

    [HttpPost]
    public Task<IActionResult> Create(ProductEditViewModel model) => SaveAsync(0, model, "Create");

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await admin.FindAsync<Product>(id);
        if (item == null) return NotFound();
        return View(new ProductEditViewModel
        {
            Id = id, Input = ProductInput.From(item),
            Categories = await admin.ListAsync<Category>(), ImageUrl = item.ImageUrl
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ProductEditViewModel model)
    {
        if (id <= 0) return NotFound();
        return await SaveAsync(id, model, "Edit");
    }

    private async Task<IActionResult> SaveAsync(int id, ProductEditViewModel model, string viewName)
    {
        model.Id = id;
        model.Categories = await admin.ListAsync<Category>();
        if (id != 0)
        {
            var existing = await admin.FindAsync<Product>(id);
            if (existing == null) return NotFound();
            model.ImageUrl = existing.ImageUrl;
        }
        if (!await TrySaveAsync(async () =>
        {
            if (model.ImageFile is { Length: > 0 }) model.ImageUrl = await admin.UploadCoverAsync(model.ImageFile);
            await admin.SaveAsync(model.Input.ToEntity(id, model.ImageUrl), id);
        })) return View(viewName, model);
        TempData["SuccessMessage"] = "Đã lưu sách.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await admin.FindAsync<Product>(id);
        return item == null ? NotFound() : View(new DeleteViewModel<Product> { Item = item });
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await admin.FindAsync<Product>(id);
        if (item == null) return NotFound();
        if (!await TrySaveAsync(async () => TempData["SuccessMessage"] = await admin.DeleteAsync<Product>(id)))
            return View("Delete", new DeleteViewModel<Product> { Item = item });
        return RedirectToAction(nameof(Index));
    }
}
