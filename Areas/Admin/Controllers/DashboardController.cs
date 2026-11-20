using BanSachMVC.Services;
using BanSachMVC.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BanSachMVC.Areas.Admin.Controllers;

public class DashboardController(AdminService admin) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(new DashboardViewModel { Dashboard = await admin.DashboardAsync() });
}
