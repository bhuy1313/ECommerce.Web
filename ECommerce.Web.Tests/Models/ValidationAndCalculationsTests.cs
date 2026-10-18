using System.ComponentModel.DataAnnotations;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;

namespace ECommerce.Web.Tests.Models;

public class ValidationAndCalculationsTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void Order_StoresShippingInfo()
    {
        var order = new Order
        {
            UserId = "u1",
            RecipientName = "Nguyen Van A",
            RecipientPhone = "0901234567",
            ShippingAddress = "12 Le Loi, Q1, TP.HCM",
            Note = "Giao sau 18h",
            TotalAmount = 100m
        };
        Assert.Equal("Nguyen Van A", order.RecipientName);
        Assert.Equal("0901234567", order.RecipientPhone);
        Assert.Equal("12 Le Loi, Q1, TP.HCM", order.ShippingAddress);
        Assert.Equal("Giao sau 18h", order.Note);
    }

    [Fact]
    public void CheckoutViewModel_MissingRequiredFields_IsInvalid()
    {
        var vm = new CheckoutViewModel { RecipientName = "", RecipientPhone = "", ShippingAddress = "" };
        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(vm, new ValidationContext(vm), results, validateAllProperties: true);
        Assert.False(ok);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void OrderTotal_CalculatesSumOfAllItemsAccurately()
    {
        // Arrange
        var items = new List<OrderItem>
        {
            new OrderItem { ProductId = 1, Quantity = 2, UnitPrice = 15.50m }, // 31.00
            new OrderItem { ProductId = 2, Quantity = 1, UnitPrice = 49.99m }, // 49.99
            new OrderItem { ProductId = 3, Quantity = 4, UnitPrice = 5.25m }   // 21.00
        };

        // Act
        var total = items.Sum(i => i.Quantity * i.UnitPrice);

        // Assert
        Assert.Equal(101.99m, total);
    }

    [Fact]
    public void ProductFormViewModel_WhenValid_PassesValidation()
    {
        // Arrange
        var model = new ProductFormViewModel
        {
            Name = "Mechanical Keyboard",
            Description = "RGB Backlit",
            Price = 89.99m,
            StockQuantity = 15,
            CategoryId = 1,
            IsActive = true
        };

        // Act
        var errors = ValidateModel(model);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ProductFormViewModel_WhenNameEmpty_FailsValidation()
    {
        // Arrange
        var model = new ProductFormViewModel
        {
            Name = "",
            Price = 10m,
            StockQuantity = 5,
            CategoryId = 1
        };

        // Act
        var errors = ValidateModel(model);

        // Assert
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ProductFormViewModel.Name)));
    }

    [Fact]
    public void ProductFormViewModel_WhenPriceZeroOrNegative_FailsValidation()
    {
        // Arrange
        var model = new ProductFormViewModel
        {
            Name = "Valid Product",
            Price = 0m,
            StockQuantity = 5,
            CategoryId = 1
        };

        // Act
        var errors = ValidateModel(model);

        // Assert
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ProductFormViewModel.Price)));
    }

    [Fact]
    public void ProductFormViewModel_WhenStockQuantityNegative_FailsValidation()
    {
        // Arrange
        var model = new ProductFormViewModel
        {
            Name = "Valid Product",
            Price = 100m,
            StockQuantity = -1,
            CategoryId = 1
        };

        // Act
        var errors = ValidateModel(model);

        // Assert
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ProductFormViewModel.StockQuantity)));
    }

    [Fact]
    public void CategoryFormViewModel_WhenNameEmpty_FailsValidation()
    {
        // Arrange
        var model = new CategoryFormViewModel
        {
            Name = ""
        };

        // Act
        var errors = ValidateModel(model);

        // Assert
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CategoryFormViewModel.Name)));
    }

    [Fact]
    public void RegisterViewModel_WhenPasswordsDoNotMatch_FailsValidation()
    {
        // Arrange
        var model = new RegisterViewModel
        {
            Email = "test@example.com",
            Password = "Password123!",
            ConfirmPassword = "DifferentPassword456!"
        };

        // Act
        var errors = ValidateModel(model);

        // Assert
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(RegisterViewModel.ConfirmPassword)));
    }

    [Fact]
    public void RegisterViewModel_WhenEmailInvalid_FailsValidation()
    {
        // Arrange
        var model = new RegisterViewModel
        {
            Email = "not-a-valid-email",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        // Act
        var errors = ValidateModel(model);

        // Assert
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(RegisterViewModel.Email)));
    }
}
