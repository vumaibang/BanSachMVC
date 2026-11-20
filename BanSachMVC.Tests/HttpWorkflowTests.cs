using System.Net;
using System.Text.RegularExpressions;
using BanSachMVC.Data;
using BanSachMVC.Models;
using BanSachMVC.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BanSachMVC.Tests;

// Đi qua HTTP thật: MVC model binding, chống CSRF, cookie, phân quyền và chuyển trang.
// Mỗi test có một SQLite tạm; không kết nối BanSachDB.
public sealed class HttpWorkflowTests : IAsyncLifetime
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"Mvc-tests-{Guid.NewGuid():N}.db");
    private WebApplication app = null!;
    private HttpClient client = null!;
    private int bookId, customerId;
    private IDbContextFactory<AppDbContext> Factory => app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

    public async Task InitializeAsync()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ApplicationName = typeof(MvcHosting).Assembly.GetName().Name,
            ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot"), EnvironmentName = "Development"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddBookStore(options => options.UseSqlite($"Data Source={database}"));
        app = builder.Build();
        app.UseBookStore();
        await using (var db = await Factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            Account Make(string name, string role) {
                var a = new Account { Username = name, Role = role };
                a.PasswordHash = new PasswordHasher<Account>().HashPassword(a, "Test@123");
                if (role == "Customer") a.Customer = new Customer { FullName = "Khách thử", Address = "123 Đường Sách", Phone = "0901234567" };
                return a;
            }
            var customer = Make("reader", "Customer");
            var book = new Product { ProductName = "Sách kiểm thử", Price = 100000, StockQuantity = 10, Category = new Category { CategoryName = "Văn học" } };
            db.AddRange(customer, Make("admin", "Admin"), book);
            await db.SaveChangesAsync();
            customerId = customer.Customer!.CustomerId; bookId = book.ProductId;
        }
        await app.StartAsync();
        client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Urls.Single()) };
    }
    public async Task DisposeAsync()
    {
        client?.Dispose();
        await app.StopAsync(); await app.DisposeAsync();
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(database + suffix)) File.Delete(database + suffix);
    }
    private async Task<HttpResponseMessage> Post(string path, Dictionary<string,string>? fields = null, string? tokenPage = null)
    {
        var response = await client.GetAsync(tokenPage ?? path.Split('?')[0]);
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, html.Split("HEADERS")[0]);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.True(token.Length > 0, html);
        fields ??= [];
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }
    private Task<HttpResponseMessage> Login(bool admin = false, string? returnUrl = null) => Post(admin ? "/Admin/Login" : "/User/Login", new() {
        ["Username"] = admin ? "admin" : "reader", ["Password"] = "Test@123", ["ReturnUrl"] = returnUrl ?? ""
    });

    [Fact]
    public async Task AnonymousAndCustomerCannotEnterAdminAndPostRequiresAntiforgery()
    {
        var anonymous = await client.GetAsync("/Admin/Products");
        Assert.Equal(HttpStatusCode.Found, anonymous.StatusCode);
        Assert.StartsWith("/Admin/Login?", anonymous.Headers.Location!.OriginalString);
        var csrf = await client.PostAsync("/User/Cart/Add", new FormUrlEncodedContent(new Dictionary<string,string> { ["productId"] = bookId.ToString() }));
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await Login()).StatusCode);
        var forbidden = await client.GetAsync("/Admin/Products");
        Assert.Equal(HttpStatusCode.Found, forbidden.StatusCode);
        Assert.Contains("/User/AccessDenied", forbidden.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task GuestCartLoginCheckoutAndCancelWorkThroughMvcForms()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        var added = await Post("/User/Cart/Add", new() { ["productId"] = bookId.ToString(), ["quantity"] = "2" }, "/");
        Assert.Equal(HttpStatusCode.Found, added.StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await Login()).StatusCode);
        var ordered = await Post("/User/Cart/Checkout", new() { ["Input.ShippingAddress"] = "456 Đường Mới", ["Input.Phone"] = "0901234567", ["Input.PaymentMethod"] = "COD", ["Quantity"] = "1" });
        Assert.Equal(HttpStatusCode.Found, ordered.StatusCode);
        int orderId;
        await using (var db = await Factory.CreateDbContextAsync()) {
            var order = await db.Orders.SingleAsync(); orderId = order.OrderId;
            Assert.Equal(200000, order.TotalAmount); Assert.Equal(customerId, order.CustomerId);
            Assert.Equal(8, (await db.Products.SingleAsync()).StockQuantity); Assert.Empty(await db.CartItems.ToListAsync());
        }
        var url = $"/User/Orders/Details/{orderId}";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await Post($"/User/Orders/Cancel/{orderId}", tokenPage: url)).StatusCode);
        await using var check = await Factory.CreateDbContextAsync();
        Assert.Equal("Cancelled", (await check.Orders.SingleAsync()).Status);
        Assert.Equal(10, (await check.Products.SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task AdminCanCreateEditAndDeleteUsingInputModels()
    {
        Assert.Equal(HttpStatusCode.Found, (await Login(true)).StatusCode);
        foreach (var page in new[] { "/Admin", "/Admin/Products", "/Admin/Customers", "/Admin/Categories", "/Admin/Accounts", "/Admin/Orders" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(page)).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await Post("/Admin/Categories/Create", new() { ["Input.CategoryName"] = "Danh mục mới", ["Input.IsActive"] = "true" })).StatusCode);
        int id;
        await using (var db = await Factory.CreateDbContextAsync()) id = (await db.Categories.SingleAsync(c => c.CategoryName == "Danh mục mới")).CategoryId;
        Assert.Equal(HttpStatusCode.Found, (await Post($"/Admin/Categories/Edit/{id}", new() { ["Input.CategoryName"] = "Đã sửa", ["Input.IsActive"] = "true" })).StatusCode);
        var product = new Dictionary<string,string> { ["Input.ProductName"] = "Sách mới", ["Input.Price"] = "150000", ["Input.StockQuantity"] = "5", ["Input.CategoryId"] = id.ToString(), ["Input.IsActive"] = "true" };
        Assert.Equal(HttpStatusCode.Found, (await Post("/Admin/Products/Create", product)).StatusCode);
        int productId;
        await using (var db = await Factory.CreateDbContextAsync()) productId = (await db.Products.SingleAsync(p => p.ProductName == "Sách mới")).ProductId;
        product["Input.ProductName"] = "Sách đã sửa";
        Assert.Equal(HttpStatusCode.Found, (await Post($"/Admin/Products/Edit/{productId}", product)).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await Post($"/Admin/Products/Delete/{productId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await Post($"/Admin/Categories/Delete/{id}")).StatusCode);
        await using var check = await Factory.CreateDbContextAsync();
        Assert.Single(await check.Products.ToListAsync()); Assert.Single(await check.Categories.ToListAsync());
    }

    [Fact]
    public async Task ValidationReturnsFormWithoutWritingAndLockedCookieIsRejected()
    {
        await Login();
        var result = await Post("/User/Cart/Checkout", new() { ["ProductId"] = bookId.ToString(), ["Quantity"] = "1", ["Input.ShippingAddress"] = "", ["Input.Phone"] = "0901234567", ["Input.PaymentMethod"] = "COD" }, $"/User/Cart/Checkout?ProductId={bookId}");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        await using (var db = await Factory.CreateDbContextAsync()) {
            Assert.Empty(await db.Orders.ToListAsync());
            (await db.Accounts.SingleAsync(a => a.Username == "reader")).IsActive = false;
            await db.SaveChangesAsync();
        }
        var locked = await client.GetAsync("/User/Orders");
        Assert.Equal(HttpStatusCode.Found, locked.StatusCode);
        Assert.Contains("/User/Login", locked.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    public async Task LoginDoesNotRedirectToExternalSite(string returnUrl)
    {
        var response = await Login(returnUrl: returnUrl);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task RegistrationProfileAndPasswordFormsWork()
    {
        var registration = await Post("/User/Register", new() { ["Input.Username"] = "newreader", ["Input.Password"] = "Test@123", ["Input.ConfirmPassword"] = "Test@123", ["Input.FullName"] = "Bạn đọc mới" });
        Assert.Equal(HttpStatusCode.Found, registration.StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await Post("/User/Login", new() { ["Username"] = "newreader", ["Password"] = "Test@123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await Post("/User/Profile", new() { ["Input.FullName"] = "Bạn đọc cập nhật", ["Input.Email"] = "reader@example.test" })).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await Post("/User/Profile/ChangePassword", new() { ["Input.CurrentPassword"] = "Test@123", ["Input.NewPassword"] = "New@123", ["Input.ConfirmPassword"] = "New@123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await client.GetAsync("/User/Profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await Post("/User/Login", new() { ["Username"] = "newreader", ["Password"] = "New@123" })).StatusCode);
        await using var db = await Factory.CreateDbContextAsync();
        Assert.Equal("Bạn đọc cập nhật", (await db.Customers.SingleAsync(c => c.Email == "reader@example.test")).FullName);
    }

    [Fact]
    public async Task AdminCanManageCustomersAndAccountsButCannotLockSelf()
    {
        await Login(true);
        Assert.Equal(HttpStatusCode.Found, (await Post("/Admin/Customers/Create", new()
        { ["Input.FullName"] = "Khách tại quầy", ["Input.Email"] = "walkin@example.test", ["Input.IsActive"] = "true" })).StatusCode);
        int id;
        await using (var db = await Factory.CreateDbContextAsync())
            id = await db.Customers.Where(c => c.Email == "walkin@example.test").Select(c => c.CustomerId).SingleAsync();
        Assert.Equal(HttpStatusCode.Found, (await Post($"/Admin/Customers/Edit/{id}", new()
        { ["Input.FullName"] = "Khách đã sửa", ["Input.IsActive"] = "true" })).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await Post($"/Admin/Customers/ToggleStatus/{id}", tokenPage: "/Admin/Customers")).StatusCode);
        await using (var db = await Factory.CreateDbContextAsync()) Assert.False((await db.Customers.FindAsync(id))!.IsActive);
        Assert.Equal(HttpStatusCode.Found, (await Post($"/Admin/Customers/Delete/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Found, (await Post("/Admin/Accounts/Create", new()
        { ["Input.Username"] = "created_reader", ["Input.Password"] = "Test@123", ["Input.Role"] = "Customer", ["Input.IsActive"] = "true" })).StatusCode);
        int accountId, linkedCustomerId, adminId;
        await using (var db = await Factory.CreateDbContextAsync())
        {
            var account = await db.Accounts.Include(a => a.Customer).SingleAsync(a => a.Username == "created_reader");
            accountId = account.AccountId; linkedCustomerId = account.Customer!.CustomerId;
            adminId = await db.Accounts.Where(a => a.Username == "admin").Select(a => a.AccountId).SingleAsync();
        }
        Assert.Equal(HttpStatusCode.Found, (await Post($"/Admin/Accounts/Edit/{accountId}", new()
        { ["Input.Username"] = "edited_reader", ["Input.Role"] = "Customer", ["Input.IsActive"] = "true" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post($"/Admin/Accounts/Delete/{accountId}")).StatusCode);
        await Post($"/Admin/Accounts/ToggleStatus/{adminId}", tokenPage: "/Admin/Accounts");
        await using (var db = await Factory.CreateDbContextAsync())
        {
            Assert.True((await db.Accounts.FindAsync(adminId))!.IsActive);
            Assert.Equal("edited_reader", (await db.Accounts.FindAsync(accountId))!.Username);
        }
        await Post($"/Admin/Customers/Delete/{linkedCustomerId}");
        await Post($"/Admin/Accounts/Delete/{accountId}");
        await using var check = await Factory.CreateDbContextAsync();
        Assert.Null(await check.Customers.FindAsync(linkedCustomerId));
        Assert.Null(await check.Accounts.FindAsync(accountId));
    }

    [Theory]
    [InlineData("vi-VN")]
    [InlineData("fr-FR")]
    public async Task HtmlNumberPriceKeepsDecimalAndInvalidInputDoesNotWrite(string language)
    {
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        await Login(true);
        int categoryId;
        await using (var db = await Factory.CreateDbContextAsync()) categoryId = await db.Categories.Select(c => c.CategoryId).SingleAsync();
        var fields = new Dictionary<string, string>
        {
            ["Input.ProductName"] = "Giá lẻ", ["Input.CategoryId"] = categoryId.ToString(), ["Input.Price"] = "123456.78",
            ["Input.StockQuantity"] = "5", ["Input.IsActive"] = "true", ["ImageUrl"] = "/fake.jpg"
        };
        Assert.Equal(HttpStatusCode.Found, (await Post("/Admin/Products/Create", fields)).StatusCode);
        foreach (var invalid in new[] { "-1", "0", "not-a-number" })
        {
            fields["Input.Price"] = invalid;
            Assert.Equal(HttpStatusCode.OK, (await Post("/Admin/Products/Create", fields)).StatusCode);
        }
        await using var check = await Factory.CreateDbContextAsync();
        var book = await check.Products.SingleAsync(p => p.ProductName == "Giá lẻ");
        Assert.Equal(123456.78m, book.Price);
        Assert.Null(book.ImageUrl);
        Assert.Equal(2, await check.Products.CountAsync());
    }

    [Fact]
    public async Task InvalidImageUploadReturnsValidationWithoutChangingProduct()
    {
        await Login(true);
        var path = $"/Admin/Products/Edit/{bookId}";
        var html = await client.GetStringAsync(path);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        int categoryId;
        await using (var db = await Factory.CreateDbContextAsync()) categoryId = await db.Categories.Select(c => c.CategoryId).SingleAsync();
        using var form = new MultipartFormDataContent
        {
            { new StringContent(WebUtility.HtmlDecode(token)), "__RequestVerificationToken" },
            { new StringContent("Không được lưu"), "Input.ProductName" },
            { new StringContent(categoryId.ToString()), "Input.CategoryId" },
            { new StringContent("999"), "Input.Price" },
            { new StringContent("5"), "Input.StockQuantity" },
            { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>")), "ImageFile", "fake.png" }
        };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(path, form)).StatusCode);
        await using var check = await Factory.CreateDbContextAsync();
        var book = await check.Products.FindAsync(bookId);
        Assert.Equal("Sách kiểm thử", book!.ProductName);
        Assert.Null(book.ImageUrl);
    }

    [Fact]
    public async Task GeneratedLinksFormsAssetsAndStatusPagesWork()
    {
        var html = await client.GetStringAsync($"/User/Products/Details/{bookId}");
        Assert.Contains("action=\"/User/Cart/Add\"", html);
        Assert.Contains("formaction=\"/User/Cart/BuyNow\"", html);
        foreach (var asset in new[] { "/css/mvc.css", "/css/site.css", "/js/site.js", "/lib/bootstrap/dist/css/bootstrap.min.css", "/lib/jquery/dist/jquery.min.js", "/lib/jquery-validation/dist/jquery.validate.min.js" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(asset)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/missing-mvc-page")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/User/Products/Details/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/User/Cart/Add")).StatusCode);
        await Login(true);
        var admin = await client.GetStringAsync("/Admin");
        foreach (var controller in new[] { "Products", "Categories", "Customers", "Accounts", "Orders" })
            Assert.Contains($"href=\"/Admin/{controller}\"", admin);
        Assert.DoesNotContain("asp-page", admin);
    }
}
