using BanSachMVC.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BanSachMVC.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Policy = "Admin")]
public abstract class AdminController : StoreController { }
