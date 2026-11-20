using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BanSachMVC.Controllers;

[Route("User/Products")]
public class ProductsController(CatalogService catalog) : Controller
{
    [HttpGet("Details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var product = await catalog.ProductAsync(id);
        return product == null ? NotFound() : View(new ProductDetailsViewModel { Product = product });
    }
}
