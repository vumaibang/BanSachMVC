using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BanSachMVC.Controllers;

public class HomeController(CatalogService catalog) : Controller
{
    [HttpGet("/", Order = 0)]
    [HttpGet("/User", Order = 1)]
    [HttpGet("/User/Index", Order = 2)]
    public async Task<IActionResult> Index(CatalogViewModel model)
    {
        model.PageNumber = Math.Clamp(model.PageNumber, 1, 100000);
        model.Categories = await catalog.CategoriesAsync();
        if (model.MinPrice > model.MaxPrice)
            ModelState.AddModelError(string.Empty, "Giá từ không được lớn hơn giá đến.");
        if (!ModelState.IsValid) return View(model);
        var result = await catalog.SearchAsync(model.Search, model.CategoryId, model.MinPrice, model.MaxPrice, model.Sort, model.PageNumber);
        model.Total = result.Total;
        model.Products = result.Products;
        if (model.PageNumber > model.TotalPages)
        {
            model.PageNumber = model.TotalPages;
            model.Products = (await catalog.SearchAsync(model.Search, model.CategoryId, model.MinPrice, model.MaxPrice, model.Sort, model.PageNumber)).Products;
        }
        return View(model);
    }

    [HttpGet("/User/Privacy")]
    public IActionResult Privacy() => View();

    [Route("/User/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() { Response.StatusCode = 500; return View(); }

    [Route("/User/AccessDenied")]
    public IActionResult AccessDenied() { Response.StatusCode = 403; return View(); }

    [Route("/User/NotFound")]
    public IActionResult Missing() { if (Response.StatusCode < 400) Response.StatusCode = 404; return View("NotFound"); }
}
