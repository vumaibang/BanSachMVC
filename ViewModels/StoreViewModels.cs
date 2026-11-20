using System.ComponentModel.DataAnnotations;
using BanSachMVC.Models;
using BanSachMVC.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BanSachMVC.ViewModels;

// ViewModels chỉ chứa dữ liệu của biểu mẫu/giao diện; nghiệp vụ nằm trong Services.
// Dữ liệu hiển thị được lấy từ database, không bind lại từ request.
public class CatalogViewModel
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    [Range(0, double.MaxValue)] public decimal? MinPrice { get; set; }
    [Range(0, double.MaxValue)] public decimal? MaxPrice { get; set; }
    public string? Sort { get; set; } = "newest";
    public int PageNumber { get; set; } = 1;
    [BindNever, ValidateNever] public List<Category> Categories { get; set; } = [];
    [BindNever, ValidateNever] public List<Product> Products { get; set; } = [];
    [BindNever] public int Total { get; set; }
    public int TotalPages => Math.Max(1, (Total + 7) / 8);
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Nhập tên đăng nhập."), StringLength(50)]
    public string Username { get; set; } = "";
    [Required(ErrorMessage = "Nhập mật khẩu."), StringLength(100), DataType(DataType.Password)]
    public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public class RegistrationViewModel { public RegistrationInput Input { get; set; } = new(); }
public class ProfileViewModel { public CustomerInput Input { get; set; } = new(); }
public class ChangePasswordViewModel { public PasswordInput Input { get; set; } = new(); }
public class ProductDetailsViewModel
{
    [BindNever, ValidateNever] public Product Product { get; set; } = null!;
}
public class CartViewModel
{
    [BindNever, ValidateNever] public List<CartItem> Items { get; set; } = [];
}
public class CheckoutViewModel : CartViewModel
{
    public CheckoutInput Input { get; set; } = new();
    public int? ProductId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; } = 1;
}
public class ListViewModel<T>
{
    public string? SearchTerm { get; set; }
    public string? StatusFilter { get; set; }
    public int PageNumber { get; set; } = 1;
    [BindNever] public int Total { get; set; }
    [BindNever, ValidateNever] public List<T> Items { get; set; } = [];
    public int TotalPages => Math.Max(1, (Total + 19) / 20);
}
public class OrdersViewModel : ListViewModel<Order> { public string? Status { get; set; } }
public class OrderViewModel
{
    [BindNever, ValidateNever] public Order Order { get; set; } = null!;
}
public class DashboardViewModel
{
    [BindNever, ValidateNever] public Dashboard Dashboard { get; set; } = null!;
}
public class ProductListViewModel : ListViewModel<Product>
{
    public int? CategoryId { get; set; }
    [BindNever, ValidateNever] public List<Category> Categories { get; set; } = [];
}
public class CategoryListViewModel : ListViewModel<Category> { }
public class CustomerListViewModel : ListViewModel<Customer> { }
public class AccountListViewModel : ListViewModel<Account> { public string? RoleFilter { get; set; } }
public class ProductEditViewModel
{
    [BindNever] public int Id { get; set; }
    public ProductInput Input { get; set; } = new();
    public IFormFile? ImageFile { get; set; }
    [BindNever] public string? ImageUrl { get; set; }
    [BindNever, ValidateNever] public List<Category> Categories { get; set; } = [];
}
public class CategoryEditViewModel
{
    [BindNever] public int Id { get; set; }
    public CategoryInput Input { get; set; } = new();
}
public class CustomerEditViewModel
{
    [BindNever] public int Id { get; set; }
    public CustomerInput Input { get; set; } = new();
}
public class AccountEditViewModel
{
    [BindNever] public int Id { get; set; }
    public AccountInput Input { get; set; } = new();
}
public class DeleteViewModel<T>
{
    [BindNever, ValidateNever] public T Item { get; set; } = default!;
}
