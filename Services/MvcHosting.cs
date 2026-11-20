using BanSachMVC.Data;
using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BanSachMVC.Services;

public static class MvcHosting
{
    public static IServiceCollection AddBookStore(this IServiceCollection services, Action<DbContextOptionsBuilder> database)
    {
        services.AddControllersWithViews(options =>
        {
            // Mọi action thay đổi dữ liệu đều phải gửi token chống CSRF.
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        });
        // HTML input[type=number] gửi dấu chấm thập phân. Cố định culture của
        // request để máy Windows vi-VN không đọc 123456.78 thành 12345678.
        // Giá hiển thị vẫn dùng vi-VN qua Display.Money.
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var culture = CultureInfo.GetCultureInfo("en-US");
            options.DefaultRequestCulture = new RequestCulture(culture);
            options.SupportedCultures = [culture];
            options.SupportedUICultures = [culture];
            options.RequestCultureProviders.Clear();
        });
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.Cookie.Name = "BanSachMVC.GuestCart";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.IdleTimeout = TimeSpan.FromHours(8);
        });
        services.AddHttpContextAccessor();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "BanSachMVC.Auth";
            options.LoginPath = "/User/Login";
            options.AccessDeniedPath = "/User/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.EventsType = typeof(AccountCookieEvents);
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
            options.AddPolicy("Customer", policy => policy.RequireRole("Customer"));
        });
        services.AddScoped<AccountCookieEvents>();
        services.AddScoped<CurrentUser>();
        services.AddScoped<AccountService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<AdminService>();
        services.AddScoped<OrderService>();
        services.AddScoped<CartService>();
        services.AddDbContextFactory<AppDbContext>(database);
        return services;
    }

    public static void UseBookStore(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/User/Error");
            app.UseHsts();
            app.UseHttpsRedirection();
        }
        app.UseRequestLocalization();
        app.UseStatusCodePagesWithReExecute("/User/NotFound");
        app.UseStaticFiles();
        app.UseRouting();
        app.UseSession();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("admin", "Admin/{controller=Dashboard}/{action=Index}/{id?}", new { area = "Admin" });
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
    }
}
