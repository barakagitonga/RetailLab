using System.Windows;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Desktop;

public partial class AdjustmentHistoryWindow : Window
{
    private readonly InventoryProductRow product;
    private readonly Func<RetailLabDbContext> createDbContext;
    private readonly TimeProvider timeProvider;

    public AdjustmentHistoryWindow(
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

        ProductHeadingTextBlock.Text = product.IsArchived
            ? $"{product.Sku} — {product.Description} · Archived"
            : $"{product.Sku} — {product.Description}";
        Title = $"Adjustment history — {product.Sku}";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            await using var dbContext = createDbContext();
            var service = new InventoryService(new EfRetailRepository(dbContext), timeProvider);
            var history = await service.GetHistoryAsync(product.Sku);

            // The repository returns oldest-first; staff read newest-first,
            // so the presentation layer reverses the order deterministically.
            var rows = history
                .OrderByDescending(adjustment => adjustment.CreatedAtUtc)
                .ThenByDescending(adjustment => adjustment.Id)
                .Select(InventoryAdjustmentRow.FromAdjustment)
                .ToList();

            if (rows.Count == 0)
            {
                HistoryGrid.Visibility = Visibility.Collapsed;
                EmptyStateTextBlock.Visibility = Visibility.Visible;
                SetStatus($"No adjustments have been recorded for {product.Sku} yet.");
            }
            else
            {
                HistoryGrid.ItemsSource = rows;
                HistoryGrid.Visibility = Visibility.Visible;
                EmptyStateTextBlock.Visibility = Visibility.Collapsed;
                SetStatus($"Showing {rows.Count} adjustments for {product.Sku}, newest first.");
            }
        }
        catch (BusinessRuleException exception)
        {
            // Business-rule messages are already staff-friendly.
            HistoryGrid.Visibility = Visibility.Collapsed;
            EmptyStateTextBlock.Visibility = Visibility.Collapsed;
            SetStatus(exception.Message, isError: true);
        }
        catch
        {
            // Never surface raw storage or programmer details to staff.
            HistoryGrid.Visibility = Visibility.Collapsed;
            EmptyStateTextBlock.Visibility = Visibility.Collapsed;
            SetStatus(
                "Adjustment history could not be loaded. Check the local database and try again.",
                isError: true);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = isError
            ? System.Windows.Media.Brushes.DarkRed
            : (System.Windows.Media.Brush)FindResource("InkBrush");
    }
}
