using System.Globalization;
using System.Windows;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Desktop;

public partial class EditProductWindow : Window
{
    private readonly InventoryProductRow product;
    private readonly Func<RetailLabDbContext> createDbContext;
    private readonly TimeProvider timeProvider;

    public EditProductWindow(
        InventoryProductRow product,
        Func<RetailLabDbContext> createDbContext,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(createDbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.product = product;
        this.createDbContext = createDbContext;
        this.timeProvider = timeProvider;

        InitializeComponent();

        SkuHeadingTextBlock.Text = $"{product.Sku} — {product.Description}";
        Title = $"Edit product — {product.Sku}";

        DescriptionTextBox.Text = product.Description;
        PriceTextBox.Text = product.Price.ToString("0.00", CultureInfo.CurrentCulture);

        // Typing convenience only, read from the Core-owned limit so the
        // dialog cannot drift from the real rule. Core remains the authority.
        DescriptionTextBox.MaxLength = Product.MaximumDescriptionLength;
    }

    /// <summary>
    /// SKU of the updated product. Set only when saving succeeds.
    /// </summary>
    public string? SavedSku { get; private set; }

    /// <summary>
    /// True when saving found that another interface changed the product
    /// first. MainWindow refreshes and explains instead of retrying.
    /// </summary>
    public bool HadConflict { get; private set; }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        DescriptionTextBox.Focus();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private async void SaveChangesButton_Click(object sender, RoutedEventArgs e)
    {
        // UI prechecks give immediate friendly feedback. They mirror the Core
        // rules but never replace them: the service validates everything again.
        var description = DescriptionTextBox.Text;
        if (string.IsNullOrWhiteSpace(description))
        {
            SetStatus("Enter a description for the product.", isError: true);
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

        SetBusy(true);

        try
        {
            await using var dbContext = createDbContext();
            var service = new ProductService(new EfRetailRepository(dbContext), timeProvider);

            // The version captured when this form opened travels with the
            // save, so values entered against stale data cannot silently win.
            var updated = await service.UpdateDetailsAsync(
                product.Sku,
                description,
                price,
                product.Version);

            SavedSku = updated.Sku;
            DialogResult = true;
        }
        catch (ProductConflictException)
        {
            // Never overwrite the winner and never retry automatically. Close
            // with a conflict signal so MainWindow refreshes to current values.
            HadConflict = true;
            DialogResult = false;
        }
        catch (BusinessRuleException exception)
        {
            // Business-rule messages are already staff-friendly.
            SetStatus(exception.Message, isError: true);
        }
        catch (ArgumentException exception)
        {
            // Core remains the final authority; map anything the prechecks
            // missed back to its field without exposing parameter names.
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
            else
            {
                SetStatus(
                    "The product details were not valid. Check each field and try again.",
                    isError: true);
                DescriptionTextBox.Focus();
            }
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

    private void SetBusy(bool busy)
    {
        DescriptionTextBox.IsEnabled = !busy;
        PriceTextBox.IsEnabled = !busy;
        SaveChangesButton.IsEnabled = !busy;
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
