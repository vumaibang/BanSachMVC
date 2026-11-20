using System.Net;
using System.Text.RegularExpressions;
using BanSachMVC.Data;
using BanSachMVC.Models;
using BanSachMVC.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BanSachMVC.Tests;

// Chỉ chạy khi BANSACH_TEST_EXISTING_DATABASE=1. Không tạo schema, migrate hay seed.
// Tất cả dữ liệu ghi có tiền tố ngẫu nhiên, và được dọn trong finally theo ID của lần chạy.
public sealed class ExistingDatabaseTheoryAttribute : TheoryAttribute
{
    public ExistingDatabaseTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("BANSACH_TEST_EXISTING_DATABASE") != "1")
            Skip = "Bật BANSACH_TEST_EXISTING_DATABASE=1 để kiểm tra MariaDB có sẵn.";
    }
}

public class ExistingDatabaseWorkflowTests
{
    [ExistingDatabaseTheory]
    [InlineData("BanSachDB")]
    [InlineData("BanSachBlazorDB")]
    [Trait("Category", "ExistingDatabase")]
    public async Task MvcWorkflowsUseExistingDatabaseAndCleanUpOnlyTheirOwnRecords(string databaseName)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(MvcHosting).Assembly.GetName().Name,
            ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot"), EnvironmentName = "Development"
        });
        var connection = new MySqlConnectionStringBuilder(builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Thiếu DefaultConnection.")) { Database = databaseName };
        var version = Version.Parse(builder.Configuration["Database:ServerVersion"] ?? "10.4.32");
        ServerVersion serverVersion = builder.Configuration["Database:ServerType"] == "MySql"
            ? new MySqlServerVersion(version) : new MariaDbServerVersion(version);
        builder.Services.AddBookStore(options => options.UseMySql(connection.ConnectionString, serverVersion));
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        await using var app = builder.Build();
        app.UseBookStore();
        var factory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var marker = "mvc_test_" + Guid.NewGuid().ToString("N")[..12];
        var adminName = marker + "_admin";
        var userName = marker + "_reader";
        const string password = "MvcTest@2026";
        var uploadedPaths = new List<string>();
        var categoryIds = new HashSet<int>();
        var productIds = new HashSet<int>();
        var accountIds = new HashSet<int>();
        var customerIds = new HashSet<int>();
        List<(int Id, int Stock)> originalStock;
        int[] before;
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.True(await db.Database.CanConnectAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            before = await Counts(db);
            originalStock = (await db.Products.AsNoTracking().ToListAsync()).Select(p => (p.ProductId, p.StockQuantity)).ToList();
            Assert.NotEmpty(originalStock);
        }

        try
        {
            await using (var db = await factory.CreateDbContextAsync())
            {
                var admin = new Account { Username = adminName, Role = "Admin" };
                admin.PasswordHash = new PasswordHasher<Account>().HashPassword(admin, password);
                db.Accounts.Add(admin);
                await db.SaveChangesAsync();
                accountIds.Add(admin.AccountId);
            }
            await app.StartAsync();
            using var adminClient = Client(app);
            using var reader = Client(app);
            using var stranger = Client(app);

            // Đọc dữ liệu thật trước khi tạo bản ghi thử; kiểm tra sắp xếp decimal trên MariaDB.
            var home = await reader.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, home.StatusCode);
            Assert.Contains("/User/Products/Details/", await home.Content.ReadAsStringAsync());
            foreach (var sort in new[] { "price-asc", "price-desc", "newest" })
                Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/?Sort=" + sort)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/User/Products/Details/{originalStock[0].Id}")).StatusCode);

            await RedirectPost(adminClient, "/Admin/Login", "/Admin/Login", new() { ["Username"] = adminName, ["Password"] = password });
            foreach (var url in new[] { "/Admin", "/Admin/Products", "/Admin/Categories", "/Admin/Customers", "/Admin/Accounts", "/Admin/Orders" })
                Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync(url)).StatusCode);
            var dashboard = await adminClient.GetStringAsync("/Admin");
            Assert.Contains("href=\"/Admin/Products\"", dashboard);

            await RedirectPost(adminClient, "/Admin/Categories/Create", "/Admin/Categories/Create", new()
            { ["Input.CategoryName"] = marker, ["Input.IsActive"] = "true" });
            int categoryId;
            await using (var db = await factory.CreateDbContextAsync())
            {
                categoryId = await db.Categories.Where(c => c.CategoryName == marker).Select(c => c.CategoryId).SingleAsync();
                categoryIds.Add(categoryId);
            }
            await RedirectPost(adminClient, $"/Admin/Categories/Edit/{categoryId}", $"/Admin/Categories/Edit/{categoryId}", new()
            { ["Input.CategoryName"] = marker + " edited", ["Input.Description"] = "MVC integration test", ["Input.IsActive"] = "true" });

            var productFields = new Dictionary<string, string>
            {
                ["Input.ProductName"] = marker, ["Input.CategoryId"] = categoryId.ToString(), ["Input.Author"] = "Tác giả kiểm thử",
                ["Input.Price"] = "123456.78", ["Input.StockQuantity"] = "10", ["Input.IsActive"] = "true"
            };
            await RedirectPost(adminClient, "/Admin/Products/Create", "/Admin/Products/Create", productFields);
            int productId;
            await using (var db = await factory.CreateDbContextAsync())
            {
                productId = await db.Products.Where(p => p.ProductName == marker).Select(p => p.ProductId).SingleAsync();
                productIds.Add(productId);
            }
            productFields["Input.ProductName"] = marker + " edited";
            await RedirectPost(adminClient, $"/Admin/Products/Edit/{productId}", $"/Admin/Products/Edit/{productId}", productFields);

            // Tải ảnh bìa qua multipart; tên file ngẫu nhiên được ghi lại để dọn đúng file.
            using (var upload = new MultipartFormDataContent())
            {
                foreach (var field in productFields) upload.Add(new StringContent(field.Value), field.Key);
                upload.Add(new StringContent(await Token(adminClient, $"/Admin/Products/Edit/{productId}")), "__RequestVerificationToken");
                var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1cAAAAASUVORK5CYII=");
                upload.Add(new ByteArrayContent(bytes), "ImageFile", "cover.png");
                Assert.Equal(HttpStatusCode.Found, (await adminClient.PostAsync($"/Admin/Products/Edit/{productId}", upload)).StatusCode);
            }
            await using (var db = await factory.CreateDbContextAsync())
            {
                var book = await db.Products.SingleAsync(p => p.ProductId == productId);
                Assert.StartsWith("/images/products/", book.ImageUrl);
                uploadedPaths.Add(Path.Combine(root, "wwwroot", book.ImageUrl!.TrimStart('/')));
                Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync(book.ImageUrl)).StatusCode);
                Assert.Equal(123456.78m, book.Price);
            }

            // Đăng ký qua MVC rồi hợp nhất giỏ khách, cập nhật số lượng và đặt hàng.
            await RedirectPost(reader, "/User/Register", "/User/Register", new()
            { ["Input.Username"] = userName, ["Input.Password"] = password, ["Input.ConfirmPassword"] = password, ["Input.FullName"] = marker });
            int customerId;
            await using (var db = await factory.CreateDbContextAsync())
            {
                var account = await db.Accounts.Include(a => a.Customer).SingleAsync(a => a.Username == userName);
                accountIds.Add(account.AccountId); customerId = account.Customer!.CustomerId; customerIds.Add(customerId);
            }
            await RedirectPost(reader, "/User/Cart/Add", "/", new() { ["productId"] = productId.ToString(), ["quantity"] = "1" });
            await RedirectPost(reader, "/User/Login", "/User/Login", new() { ["Username"] = userName, ["Password"] = password });
            await RedirectPost(reader, "/User/Cart/Update", "/User/Cart", new() { ["productId"] = productId.ToString(), ["quantity"] = "2" });
            await RedirectPost(reader, "/User/Profile", "/User/Profile", new()
            { ["Input.FullName"] = marker, ["Input.Phone"] = "0901234567", ["Input.Address"] = "Địa chỉ kiểm thử MVC" });
            var shipping = new Dictionary<string, string>
            { ["Input.ShippingAddress"] = "Địa chỉ kiểm thử MVC", ["Input.Phone"] = "0901234567", ["Input.PaymentMethod"] = "COD", ["Quantity"] = "1" };
            var checkoutPage = await reader.GetStringAsync("/User/Cart/Checkout");
            Assert.DoesNotContain("field-validation-error", checkoutPage);
            await RedirectPost(reader, "/User/Cart/Checkout", "/User/Cart/Checkout", shipping);
            int cancelledId;
            await using (var db = await factory.CreateDbContextAsync())
            {
                var order = await db.Orders.Include(o => o.OrderDetails).SingleAsync(o => o.CustomerId == customerId);
                cancelledId = order.OrderId;
                Assert.Equal(246913.56m, order.TotalAmount);
                Assert.Equal(2, order.OrderDetails.Single().Quantity);
                Assert.Equal(8, (await db.Products.FindAsync(productId))!.StockQuantity);
                Assert.False(await db.CartItems.AnyAsync(c => c.CustomerId == customerId));
            }
            var details = $"/User/Orders/Details/{cancelledId}";
            var orderHtml = await reader.GetStringAsync(details);
            Assert.Contains($"action=\"/User/Orders/Cancel/{cancelledId}\"", orderHtml);
            Assert.Equal(HttpStatusCode.Found, (await stranger.GetAsync(details)).StatusCode);
            await RedirectPost(reader, $"/User/Orders/Cancel/{cancelledId}", details, new());
            await RedirectPost(reader, $"/User/Orders/Cancel/{cancelledId}", details, new());
            await using (var db = await factory.CreateDbContextAsync())
            {
                Assert.Equal("Cancelled", (await db.Orders.FindAsync(cancelledId))!.Status);
                Assert.Equal(10, (await db.Products.FindAsync(productId))!.StockQuantity);
            }

            // Mua ngay giữ nguyên giỏ, Admin xử lý Pending -> Shipping -> Completed.
            await RedirectPost(reader, "/User/Cart/Add", "/User/Cart", new() { ["productId"] = productId.ToString(), ["quantity"] = "1" });
            shipping["ProductId"] = productId.ToString(); shipping["Input.PaymentMethod"] = "BankTransfer";
            await RedirectPost(reader, "/User/Cart/Checkout", $"/User/Cart/Checkout?ProductId={productId}", shipping);
            int completedId;
            await using (var db = await factory.CreateDbContextAsync())
            {
                var order = await db.Orders.SingleAsync(o => o.CustomerId == customerId && o.OrderId != cancelledId);
                completedId = order.OrderId;
                Assert.Equal("BankTransfer", order.PaymentMethod);
                Assert.True(await db.CartItems.AnyAsync(c => c.CustomerId == customerId && c.Quantity == 1));
            }
            foreach (var status in new[] { "Shipping", "Completed" })
            {
                var adminDetails = $"/Admin/Orders/Details/{completedId}";
                var html = await adminClient.GetStringAsync(adminDetails);
                Assert.Contains($"action=\"/Admin/Orders/UpdateStatus/{completedId}\"", html);
                await RedirectPost(adminClient, $"/Admin/Orders/UpdateStatus/{completedId}", adminDetails, new() { ["newStatus"] = status });
            }
            await using (var db = await factory.CreateDbContextAsync())
            {
                Assert.Equal("Completed", (await db.Orders.FindAsync(completedId))!.Status);
                Assert.Equal(9, (await db.Products.FindAsync(productId))!.StockQuantity);
            }
            await RedirectPost(reader, "/User/Cart/Remove", "/User/Cart", new() { ["productId"] = productId.ToString() });
            await RedirectPost(reader, "/User/Cart/Clear", "/User/Cart", new());

            // Sách đã có đơn sẽ ngừng bán; danh mục đang chứa sách không bị xóa.
            await RedirectPost(adminClient, $"/Admin/Products/Delete/{productId}", $"/Admin/Products/Delete/{productId}", new());
            var blockedDelete = await Post(adminClient, $"/Admin/Categories/Delete/{categoryId}", $"/Admin/Categories/Delete/{categoryId}", new());
            Assert.Equal(HttpStatusCode.OK, blockedDelete.StatusCode);
            await using (var db = await factory.CreateDbContextAsync())
            {
                Assert.False((await db.Products.FindAsync(productId))!.IsActive);
                Assert.NotNull(await db.Categories.FindAsync(categoryId));
            }

            // Đổi mật khẩu, vô hiệu hóa cookie cũ, đăng nhập lại và khóa tài khoản thử.
            await RedirectPost(reader, "/User/Profile/ChangePassword", "/User/Profile/ChangePassword", new()
            { ["Input.CurrentPassword"] = password, ["Input.NewPassword"] = "NewMvc@2026", ["Input.ConfirmPassword"] = "NewMvc@2026" });
            Assert.Equal(HttpStatusCode.Found, (await reader.GetAsync("/User/Profile")).StatusCode);
            await RedirectPost(reader, "/User/Login", "/User/Login", new() { ["Username"] = userName, ["Password"] = "NewMvc@2026" });
            var readerId = accountIds.Single(id => id != accountIds.Min());
            await RedirectPost(adminClient, $"/Admin/Accounts/ToggleStatus/{readerId}", "/Admin/Accounts", new());
            Assert.Equal(HttpStatusCode.Found, (await reader.GetAsync("/User/Orders")).StatusCode);
            await RedirectPost(adminClient, "/Admin/Logout", "/Admin", new());
        }
        finally
        {
            // Lấy lại ID theo tiền tố của riêng lần chạy, kể cả khi test lỗi giữa các bước.
            await using var db = await factory.CreateDbContextAsync();
            accountIds.UnionWith(await db.Accounts.Where(a => a.Username.StartsWith(marker)).Select(a => a.AccountId).ToListAsync());
            customerIds.UnionWith(await db.Customers.Where(c => c.FullName.StartsWith(marker) || (c.AccountId.HasValue && accountIds.Contains(c.AccountId.Value))).Select(c => c.CustomerId).ToListAsync());
            productIds.UnionWith(await db.Products.Where(p => p.ProductName.StartsWith(marker)).Select(p => p.ProductId).ToListAsync());
            categoryIds.UnionWith(await db.Categories.Where(c => c.CategoryName.StartsWith(marker)).Select(c => c.CategoryId).ToListAsync());
            uploadedPaths.AddRange((await db.Products.Where(p => productIds.Contains(p.ProductId) && p.ImageUrl != null).Select(p => p.ImageUrl!).ToListAsync())
                .Select(p => Path.Combine(root, "wwwroot", p.TrimStart('/'))));
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                await db.CartItems.Where(c => customerIds.Contains(c.CustomerId)).ExecuteDeleteAsync();
                await db.OrderDetails.Where(d => customerIds.Contains(d.Order.CustomerId)).ExecuteDeleteAsync();
                await db.Orders.Where(o => customerIds.Contains(o.CustomerId)).ExecuteDeleteAsync();
                await db.Products.Where(p => productIds.Contains(p.ProductId)).ExecuteDeleteAsync();
                await db.Categories.Where(c => categoryIds.Contains(c.CategoryId)).ExecuteDeleteAsync();
                await db.Customers.Where(c => customerIds.Contains(c.CustomerId)).ExecuteDeleteAsync();
                await db.Accounts.Where(a => accountIds.Contains(a.AccountId)).ExecuteDeleteAsync();
                await transaction.CommitAsync();
            }
            var imageRoot = Path.GetFullPath(Path.Combine(root, "wwwroot", "images", "products")) + Path.DirectorySeparatorChar;
            foreach (var file in uploadedPaths.Distinct())
            {
                var fullPath = Path.GetFullPath(file);
                if (fullPath.StartsWith(imageRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath)) File.Delete(fullPath);
            }
            Assert.Equal(before, await Counts(db));
            var afterStock = (await db.Products.AsNoTracking().ToListAsync()).Select(p => (p.ProductId, p.StockQuantity)).ToList();
            Assert.Equal(originalStock.OrderBy(p => p.Id), afterStock.OrderBy(p => p.ProductId));
            await app.StopAsync();
        }
    }

    private static HttpClient Client(WebApplication app) => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() })
    { BaseAddress = new Uri(app.Urls.Single()) };

    private static async Task<int[]> Counts(AppDbContext db) =>
        [await db.Accounts.CountAsync(), await db.Customers.CountAsync(), await db.Categories.CountAsync(), await db.Products.CountAsync(),
            await db.Orders.CountAsync(), await db.OrderDetails.CountAsync(), await db.CartItems.CountAsync()];

    private static async Task<string> Token(HttpClient client, string page)
    {
        var response = await client.GetAsync(page);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return WebUtility.HtmlDecode(token);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string action, string page, Dictionary<string, string> fields)
    {
        var body = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = await Token(client, page) };
        return await client.PostAsync(action, new FormUrlEncodedContent(body));
    }
    private static async Task RedirectPost(HttpClient client, string action, string page, Dictionary<string, string> fields)
    {
        var result = await Post(client, action, page, fields);
        Assert.True(result.StatusCode == HttpStatusCode.Found, $"POST {action}: {result.StatusCode}\n{await result.Content.ReadAsStringAsync()}");
    }
}
