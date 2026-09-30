using System.Globalization;
using System.Windows;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Desktop;

public partial class CreateProductWindow : Window
{
    private readonly Func<RetailLabDbContext> createDbContext;
    private readonly TimeProvider timeProvider;

    public CreateProductWindow(
        Func<RetailLabDbContext> createDbContext,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(createDbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.createDbContext = createDbContext;
        this.timeProvider = timeProvider;

        InitializeComponent();

        // Typing convenience only, read from the Core-owned limits so the
        // dialog cannot drift from the real rules. Core remains the authority.
        SkuTextBox.MaxLength = Product.MaximumSkuLength;
        DescriptionTextBox.MaxLength = Product.MaximumDescriptionLength;
    }

    /// <summary>
    /// Normalized SKU of the created product. Set only when creation succeeds.
    /// </summary>
    public string? CreatedSku { get; private set; }

    /// <summary>
    /// Starting stock of the created product. Set only when creation succeeds.
    /// </summary>
    public int? CreatedStockQuantity { get; private set; }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SkuTextBox.Focus();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private async void CreateProductButton_Click(object sender, RoutedEventArgs e)
    {
        // UI prechecks give immediate friendly feedback. They mirror the Core
        // rules but never replace them: the service validates everything again.
        var sku = SkuTextBox.Text;
        if (string.IsNullOrWhiteSpace(sku))
        {
            SetStatus("Enter a SKU for the new product.", isError: true);
            SkuTextBox.Focus();
            return;
        }

        if (sku.Trim().Length > Product.MaximumSkuLength)
        {
            SetStatus($"SKU cannot exceed {Product.MaximumSkuLength} characters.", isError: true);
            SkuTextBox.Focus();
            return;
        }

        var description = DescriptionTextBox.Text;
        if (string.IsNullOrWhiteSpace(description))
        {
            SetStatus("Enter a description for the new product.", isError: true);
            DescriptionTextBox.Focus();
            return;
        }

        if (description.Trim().Length > Product.MaximumDescriptionLength)
        {
            SetStatus(
                $"Description cannot exceed {Product.MaximumDescriptionLength} characters.",
                isError: true);
            DescriptionTextBox.Focus();
            return;
        }

        if (!decimal.TryParse(
                PriceTextBox.Text.Trim(),
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out var price) || price < 0)
        {
            SetStatus("Price must be a valid nonnegative amount such as 9.99.", isError: true);
            PriceTextBox.Focus();
            return;
        }

        if (!int.TryParse(InitialStockTextBox.Text.Trim(), out var initialStock) || initialStock < 0)
        {
            SetStatus(
                "Initial stock must be a valid nonnegative whole number such as 0 or 12.",
                isError: true);
            InitialStockTextBox.Focus();
            return;
        }

        SetBusy(true);

        try
        {
            await using var dbContext = createDbContext();
            var service = new ProductService(new EfRetailRepository(dbContext), timeProvider);

            // Values pass through untouched: Core trims, validates, preserves
            // the entered SKU casing, and rejects duplicates case-insensitively.
            var product = await service.CreateAsync(sku, description, price, initialStock);

            CreatedSku = product.Sku;
            CreatedStockQuantity = product.StockQuantity;
            DialogResult = true;
        }
        catch (BusinessRuleException exception)
        {
            // The duplicate-SKU message is already staff-friendly. Keep the
            // window open so staff can correct the SKU and retry.
            SetStatus(exception.Message, isError: true);
            SkuTextBox.Focus();
        }
        catch (ProductConflictException)
        {
            SetStatus(
                "The product could not be saved because inventory changed elsewhere. Try again.",
                isError: true);
        }
        catch (ArgumentException exception)
        {
            // Core remains the final authority; map anything the prechecks
            // missed back to its field without exposing parameter names.
            ShowArgumentError(exception);
        }
        catch
        {
            // Never surface raw storage or programmer details to staff.
            SetStatus(
                "The product could not be saved. Check the local database and try again.",
                isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowArgumentError(ArgumentException exception)
    {
        if (string.Equals(exception.ParamName, "description", StringComparison.Ordinal))
        {
            SetStatus(
                $"Enter a description of at most {Product.MaximumDescriptionLength} characters.",
                isError: true);
            DescriptionTextBox.Focus();
        }
        else if (string.Equals(exception.ParamName, "price", StringComparison.Ordinal))
        {
            SetStatus("Price must be a valid nonnegative amount such as 9.99.", isError: true);
            PriceTextBox.Focus();
        }
        else if (string.Equals(exception.ParamName, "stockQuantity", StringComparison.Ordinal))
        {
            SetStatus(
                "Initial stock must be a valid nonnegative whole number such as 0 or 12.",
                isError: true);
            InitialStockTextBox.Focus();
        }
        else if (string.Equals(exception.ParamName, "sku", StringComparison.Ordinal))
        {
            SetStatus($"Enter a SKU of at most {Product.MaximumSkuLength} characters.", isError: true);
            SkuTextBox.Focus();
        }
        else
        {
            SetStatus(
                "The product details were not valid. Check each field and try again.",
                isError: true);
            SkuTextBox.Focus();
        }
    }

    private void SetBusy(bool busy)
    {
        SkuTextBox.IsEnabled = !busy;
        DescriptionTextBox.IsEnabled = !busy;
        PriceTextBox.IsEnabled = !busy;
        InitialStockTextBox.IsEnabled = !busy;
        CreateProductButton.IsEnabled = !busy;
        CancelButton.IsEnabled = !busy;
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = isError
            ? System.Windows.Media.Brushes.DarkRed
            : (System.Windows.Media.Brush)FindResource("InkBrush");
    }
}
