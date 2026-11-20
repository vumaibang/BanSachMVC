using BanSachMVC.Models;
using BanSachMVC.ViewModels;

namespace BanSachMVC.Services;

public static class OrderListing
{
    public static void Fill(OrdersViewModel model, List<Order> orders)
    {
        var query = orders.Where(o => (string.IsNullOrEmpty(model.Status) || o.Status == model.Status)
            && (string.IsNullOrWhiteSpace(model.SearchTerm)
                || $"{o.OrderId} {o.Customer.FullName} {o.Phone}".Contains(model.SearchTerm.Trim(), StringComparison.OrdinalIgnoreCase)));
        model.Total = query.Count();
        model.PageNumber = Math.Clamp(model.PageNumber, 1, model.TotalPages);
        model.Items = query.Skip((model.PageNumber - 1) * 20).Take(20).ToList();
    }
}
