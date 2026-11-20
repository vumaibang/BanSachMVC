# BanSachMVC — Đồ án bán sách bằng ASP.NET Core MVC

Dự án chạy độc lập bằng **ASP.NET Core MVC (.NET 10)**, dựa trên `BanSachRazorPages` và đối chiếu chức năng của `BanSachBlazor (2)`.

- Controller nhận request, kiểm tra ModelState/phân quyền, gọi Service và trả View hoặc chuyển hướng.
- View là file `.cshtml` dùng Razor để render HTML. Không có `PageModel`, `@page`, `OnGet/OnPost` hoặc component Blazor.
- `Models`, `Data` và bốn migration được tái sử dụng từ dự án gốc, chỉ đổi namespace thành `BanSachMVC`; giữ nguyên bảng, cột và quan hệ.
- Ứng dụng chính dùng **BanSachDB có sẵn** trong MariaDB/MySQL. Không tự tạo database, migrate, seed lại tài khoản hay đặt lại mật khẩu khi khởi động.

## Chạy dự án

Yêu cầu .NET SDK 10 và MySQL/MariaDB đang chạy. Trên máy hiện tại dùng XAMPP MariaDB **10.4.32**, cổng **3306**.

Trong terminal tại thư mục `BanSachMVC`:

```powershell
dotnet restore BanSachMVC.slnx
dotnet run --project BanSachMVC.csproj --launch-profile http
```

Hoặc từ thư mục cha `Thư mục mới`:

```powershell
dotnet run --project .\BanSachMVC\BanSachMVC.csproj --launch-profile http
```

- Cửa hàng: <http://localhost:5092/>
- Đăng nhập khách hàng: <http://localhost:5092/User/Login>
- Đăng nhập quản trị: <http://localhost:5092/Admin/Login>
- Khu vực quản trị: <http://localhost:5092/Admin>

Dùng tài khoản và mật khẩu đang có trong database. Có thể đăng ký khách hàng mới tại `/User/Register`. Ứng dụng không thay đổi mật khẩu của các tài khoản gốc.

Visual Studio: mở `BanSachMVC.slnx`, chọn **BanSachMVC** làm startup project và profile **http**. Các dự án test dùng để kiểm thử, không phải ứng dụng chính.

## Database có sẵn

`appsettings.json` mặc định:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=BanSachDB;User=root;Password=;"
  },
  "Database": {
    "ServerType": "MariaDb",
    "ServerVersion": "10.4.32"
  }
}
```

Để sử dụng dữ liệu của bản Blazor, thay `Database=BanSachDB` bằng `Database=BanSachBlazorDB` trong chuỗi kết nối. Hai database đã được kiểm thử với cùng mã MVC; mỗi lần chạy ứng dụng kết nối một database.

Cũng có thể chọn tạm database bằng biến môi trường, không sửa file:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=localhost;Port=3306;Database=BanSachBlazorDB;User=root;Password=;'
dotnet run --project BanSachMVC.csproj --launch-profile http
# Sau khi dừng máy chủ bằng Ctrl+C:
Remove-Item Env:ConnectionStrings__DefaultConnection
```

Nếu dùng MySQL thay MariaDB, chỉnh `Database:ServerType` thành `MySql` và `ServerVersion` đúng phiên bản máy chủ. Nếu không kết nối được, kiểm tra MySQL trong XAMPP, cổng và tài khoản database.

`Data/SeedData.cs` được giữ để tái sử dụng dữ liệu mẫu khi cần trong môi trường thử riêng; **Program.cs không gọi SeedData**. Với hai database hiện có, không cần chạy `dotnet ef database update`.

## Cấu trúc MVC

```text
BanSachMVC/
  Controllers/                 Home, Account, Products, Cart, Orders, Profile
  Views/                       Giao diện khách hàng và partial/layout dùng chung
  Areas/Admin/
    Controllers/               Dashboard, Auth, Products, Categories,
                               Customers, Accounts, Orders
    Views/                     Giao diện và layout quản trị
  ViewModels/                  Dữ liệu biểu mẫu và dữ liệu hiển thị
  Models/                      Entity gốc
  Data/                        AppDbContext, SeedData gốc
  Migrations/                  Bốn migration và snapshot gốc
  Services/                    Nghiệp vụ, giỏ hàng, giao dịch, xác thực, cấu hình MVC
  wwwroot/                     Bootstrap, CSS, JS và thư mục ảnh bìa
  BanSachMVC.Tests/             Kiểm thử nghiệp vụ, HTTP MVC, migration và MariaDB
  docs/KET_QUA_KIEM_THU.md      Kết quả kiểm thử và giới hạn xác minh
  Program.cs
  BanSachMVC.csproj
  BanSachMVC.slnx
```

Luồng đặt hàng: `POST /User/Cart/Checkout` → `CartController.Checkout` → `OrderService.CheckoutAsync` → `AppDbContext` → database → chuyển đến `OrdersController.Details` → `Views/Orders/Details.cshtml`.

`AddControllersWithViews` và `MapControllerRoute` cấu hình MVC. Khu vực quản trị dùng `Area("Admin")` và policy `Admin`; trang riêng của khách hàng dùng policy `Customer`. View dùng `asp-controller`, `asp-action`, `asp-area` để tạo đường dẫn.

## Chức năng

| Khách hàng | Quản trị |
| --- | --- |
| Tìm sách theo tên/tác giả, danh mục và khoảng giá | Thống kê số sách, danh mục, khách hàng, đơn hàng |
| Sắp xếp theo giá/ngày tạo, phân trang, xem chi tiết | Doanh thu từ đơn đã hoàn thành, đơn gần đây |
| Đăng ký, đăng nhập, đăng xuất | Đăng nhập/đăng xuất quản trị |
| Thêm/sửa/xóa giỏ hàng, hợp nhất giỏ khi đăng nhập | CRUD sách, tải ảnh bìa PNG/JPEG/WebP tối đa 5 MB |
| Mua ngay, đặt hàng COD hoặc ghi nhận chuyển khoản | CRUD danh mục, khách hàng và tài khoản |
| Xem đơn của mình, hủy đơn đang chờ | Tìm/lọc/phân trang, khóa/mở tài khoản và khách hàng |
| Cập nhật hồ sơ, đổi mật khẩu | Xem chi tiết và cập nhật trạng thái đơn |

## Quy tắc nghiệp vụ

- Giỏ khách chưa đăng nhập lưu trong session; giỏ khách đã đăng nhập lưu tại bảng `CartItems`. Khi hợp nhất một sách có trong cả hai giỏ, giữ số lượng lớn hơn và giới hạn theo tồn kho.
- Giá/tồn kho lấy lại từ database lúc đặt hàng; transaction và UPDATE có điều kiện giúp tránh đặt vượt tồn kho. Thiếu hàng rollback toàn bộ đơn. Mua ngay giữ nguyên giỏ hiện tại.
- Admin chuyển `Pending → Shipping/Cancelled`, `Shipping → Completed/Cancelled`. Khách chỉ hủy đơn `Pending` của mình. Hủy đơn hoàn kho đúng một lần; đơn hoàn thành không thể hủy.
- Cookie đăng nhập được kiểm tra lại với tài khoản trong database. Khóa tài khoản, thay quyền hoặc đổi mật khẩu làm phiên cũ không còn hợp lệ. Cookie MVC có tên riêng, không dùng chung cookie của hai ứng dụng gốc.
- Mọi POST có kiểm tra antiforgery. ReturnUrl sau đăng nhập chỉ nhận địa chỉ nội bộ. ViewModel tách dữ liệu nhập khỏi entity; ảnh/giá/tài khoản chủ sở hữu không lấy tùy ý từ request.
- Sách đã có đơn được ngừng bán khi xóa, để giữ lịch sử. Không xóa danh mục còn sách hoặc khách hàng đã có đơn. Không tự khóa/xóa/hạ quyền Admin đang đăng nhập hoặc Admin cuối cùng.
- Form số dùng dấu chấm thập phân theo chuẩn HTML; giá hiển thị bằng định dạng Việt Nam và ký hiệu ₫.
- Chuyển khoản chỉ ghi nhận phương thức thanh toán. Chưa tích hợp cổng thanh toán, xác nhận ngân hàng tự động hay gửi email.

## Kiểm thử

Kiểm thử mặc định không ghi vào database đang dùng:

```powershell
dotnet test BanSachMVC.Tests/BanSachMVC.Tests.csproj
```

31 test nghiệp vụ/HTTP/migration dùng SQLite tạm hoặc sinh SQL không kết nối máy chủ. Hai test database thật được bỏ qua mặc định.

Chạy thêm các luồng trên **cả BanSachDB và BanSachBlazorDB**:

```powershell
$env:BANSACH_TEST_EXISTING_DATABASE = '1'
try {
    dotnet test BanSachMVC.Tests/BanSachMVC.Tests.csproj --logger 'trx;LogFileName=BanSachMVC.trx'
} finally {
    Remove-Item Env:BANSACH_TEST_EXISTING_DATABASE
}
```

Test thật dùng cấu hình host/tài khoản trong `appsettings.json`, tạo bản ghi có tiền tố ngẫu nhiên `mvc_test_...`, thực hiện luồng qua HTTP, rồi dọn đúng dữ liệu của lần chạy trong `finally`. Không tạo database hoặc thay schema; có kiểm tra số bản ghi và tồn kho gốc trước/sau. Giá trị AUTO_INCREMENT có thể tăng dù bản ghi thử đã xóa. Chạy khi không có người khác đồng thời sửa dữ liệu để phép so sánh trước/sau chính xác.

Kết quả ngày 13/11/2026: **33/33 test đạt** khi bật kiểm thử database thật. Xem [biên bản kiểm thử](docs/KET_QUA_KIEM_THU.md).
