# Kết quả kiểm thử BanSachMVC

Ngày: **13/11/2026**. Môi trường: Windows, .NET SDK 10.0.401, .NET 10.0.12, MariaDB 10.4.32/XAMPP.

## Kết quả

- Build solution thành công, không có lỗi hoặc cảnh báo.
- **33 test đạt, 0 lỗi, 0 bỏ qua** khi bật `BANSACH_TEST_EXISTING_DATABASE=1`.
- 31 test mặc định: nghiệp vụ, HTTP MVC và tương thích migration; 2 test tích hợp: một cho mỗi database có sẵn.
- Đã khởi động ứng dụng chính bằng profile `http` tại `http://localhost:5092`, kết nối BanSachDB.
- Báo cáo máy đọc được tại `BanSachMVC.Tests/TestResults/BanSachMVC.trx`.

## Những gì đã kiểm tra

| Nhóm | Nội dung |
| --- | --- |
| MVC/HTTP | Render Views, liên kết/form đến action, static CSS/JS, trang 404/405, chuyển hướng sau POST |
| Database thật | Đọc danh mục/sách có sẵn, truy vấn sắp xếp giá trên MariaDB, model không lệch snapshot, đủ bốn migration |
| Danh mục/sách | Tạo, sửa, xóa; kiểm tra giá/tồn kho; giá có phần thập phân; không ghi dữ liệu khi input sai |
| Ảnh bìa | Upload multipart PNG thật, truy cập ảnh tĩnh, từ chối file giả PNG |
| Tài khoản/khách hàng | Đăng ký, CRUD quản trị, hồ sơ, khóa/mở trạng thái; chặn tự khóa Admin; xử lý quan hệ tài khoản–khách hàng |
| Giỏ hàng | Giỏ khách, hợp nhất khi đăng nhập, thay số lượng, xóa từng sách/toàn giỏ; giữ giỏ khi mua ngay |
| Đặt hàng | Giá lấy từ database, tổng tiền, trừ kho, xóa đúng giỏ, rollback khi thiếu hàng, COD/chuyển khoản |
| Đơn hàng | Khách chỉ truy cập đơn của mình; hủy hoàn kho đúng một lần; Admin giao/hoàn thành đơn; không hủy đơn hoàn thành |
| Bảo toàn lịch sử | Sách đã có đơn chuyển sang ngừng bán; danh mục còn sách không bị xóa |
| Xác thực | Cookie, antiforgery, tách quyền Admin/Customer, từ chối ReturnUrl bên ngoài, vô hiệu hóa phiên khi khóa/đổi mật khẩu |

## Dữ liệu thật sau khi dọn bản ghi thử

Số lượng sau kiểm thử khớp với trước kiểm thử:

| Database | Sách | Danh mục | Tài khoản | Đơn hàng |
| --- | ---: | ---: | ---: | ---: |
| BanSachDB | 8 | 4 | 4 | 6 |
| BanSachBlazorDB | 8 | 4 | 2 | 4 |

Test còn so sánh số lượng của đủ bảy bảng và tồn kho theo từng mã sách gốc. Bản ghi có tiền tố `mvc_test_...` cùng ảnh upload thử đã được xóa. Không migrate, seed lại hay thay mật khẩu tài khoản gốc. Các bộ đếm AUTO_INCREMENT có thể đã tăng do việc tạo/xóa bản ghi thử.

## Giới hạn xác minh

Công cụ Computer Use dừng vì không xác định được URL hiện tại của Microsoft Edge. Vì vậy chưa xác nhận bằng mắt bố cục desktop/mobile hoặc tương tác JavaScript trong trình duyệt. Các request, HTML, form action, tài nguyên tĩnh, binding MVC và luồng dữ liệu đã được kiểm tra qua HTTP thật trên Kestrel.

Chưa kiểm thử tải lớn hoặc cạnh tranh đồng thời nhiều người dùng. Thanh toán chuyển khoản chưa kết nối dịch vụ ngân hàng.
