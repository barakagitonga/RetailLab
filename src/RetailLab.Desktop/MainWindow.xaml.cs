using System.Windows;
using System.Windows.Controls;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Desktop;

public partial class MainWindow : Window
{
    private const string StaffActor = "staff-desktop-01";

    private readonly Func<RetailLabDbContext> createDbContext;
    private readonly TimeProvider timeProvider;
    private bool isBusy;

    public MainWindow(
        Func<RetailLabDbContext> createDbContext,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(createDbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.createDbContext = createDbContext;
        this.timeProvider = timeProvider;

        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshProductsAsync();
    }

    private void InventoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionPanel();
    }

    private async void NewProductButton_Click(object sender, RoutedEventArgs e)
    {
        var createWindow = new CreateProductWindow(createDbContext, timeProvider)
        {
            Owner = this
        };

        if (createWindow.ShowDialog() == true &&
            createWindow.CreatedSku is string createdSku &&
            createWindow.CreatedStockQuantity is int createdStock)
        {
            await RefreshProductsAsync(
                createdSku,
                $"Created product {createdSku} with starting stock {createdStock}.");
        }
    }

    private async void EditProductButton_Click(object sender, RoutedEventArgs e)
    {
        if (InventoryGrid.SelectedItem is not InventoryProductRow selected)
        {
            SetStatus("Select a product before editing details.", isError: true);
            return;
        }

        if (selected.IsArchived)
        {
            SetStatus($"Product {selected.Sku} is archived. Unarchive it before editing details.", isError: true);
            return;
        }

        var editWindow = new EditProductWindow(selected, createDbContext, timeProvider)
        {
            Owner = this
        };

        if (editWindow.ShowDialog() == true && editWindow.SavedSku is string savedSku)
        {
            await RefreshProductsAsync(savedSku, $"Updated {savedSku}.");
        }
        else if (editWindow.HadConflict)
        {
            await RefreshProductsAsync(
                selected.Sku,
                $"{selected.Sku} changed elsewhere. The list has been refreshed; review the current values before editing again.");
        }
    }

    private async void ArchiveToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (InventoryGrid.SelectedItem is not InventoryProductRow selected)
        {
            SetStatus("Select a product before changing its archive state.", isError: true);
            return;
        }

        if (selected.IsArchived)
        {
            await UnarchiveSelectedAsync(selected);
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Archive {selected.Sku} ({selected.Description})?\n\n" +
            "- It will disappear from the customer catalogue and favourites.\n" +
            "- Existing basket lines stay visible but become unavailable: customers can only remove them.\n" +
            "- Stock, orders, and adjustment history are kept.\n" +
            "- You can unarchive it later; nothing is deleted.",
            $"Archive {selected.Sku}?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        await ArchiveSelectedAsync(selected);
    }

    private async Task ArchiveSelectedAsync(InventoryProductRow selected)
    {
        SetBusy(true);

        try
        {
            await using var dbContext = createDbContext();
            var service = new ProductService(new EfRetailRepository(dbContext), timeProvider);
            await service.ArchiveAsync(selected.Sku, selected.Version);
            await RefreshProductsAsync(
                selected.Sku,
                $"Archived {selected.Sku}. It is hidden from customers but can be unarchived later.");
        }
        catch (ProductConflictException)
        {
            await RefreshProductsAsync(
                selected.Sku,
                $"{selected.Sku} changed elsewhere. The list has been refreshed; review the current product and try again.");
        }
        catch (BusinessRuleException exception)
        {
            SetStatus(exception.Message, isError: true);
        }
        catch (Exception)
        {
            SetStatus("The product could not be archived. Check the local database and try again.", isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task UnarchiveSelectedAsync(InventoryProductRow selected)
    {
        SetBusy(true);

        try
        {
            await using var dbContext = createDbContext();
            var service = new ProductService(new EfRetailRepository(dbContext), timeProvider);
            await service.UnarchiveAsync(selected.Sku, selected.Version);
            await RefreshProductsAsync(
                selected.Sku,
                $"Unarchived {selected.Sku}. It is visible to customers again.");
        }
        catch (ProductConflictException)
        {
            await RefreshProductsAsync(
                selected.Sku,
                $"{selected.Sku} changed elsewhere. The list has been refreshed; review the current product and try again.");
        }
        catch (BusinessRuleException exception)
        {
            SetStatus(exception.Message, isError: true);
        }
        catch (Exception)
        {
            SetStatus("The product could not be unarchived. Check the local database and try again.", isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void RefreshInventoryButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedSku = (InventoryGrid.SelectedItem as InventoryProductRow)?.Sku;
        await RefreshProductsAsync(selectedSku);
    }

    private void ViewHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (InventoryGrid.SelectedItem is not InventoryProductRow selected)
        {
            SetStatus("Select a product before viewing adjustment history.", isError: true);
            return;
        }

        var historyWindow = new AdjustmentHistoryWindow(selected, createDbContext, timeProvider)
        {
            Owner = this
        };
        historyWindow.ShowDialog();
    }

    private async void ApplyAdjustmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (InventoryGrid.SelectedItem is not InventoryProductRow selected)
        {
            SetStatus("Select a product before applying an adjustment.", isError: true);
            return;
        }

        if (!int.TryParse(QuantityChangeTextBox.Text.Trim(), out var quantityChange))
        {
            SetStatus("Quantity change must be a whole number such as 5 or -3.", isError: true);
            QuantityChangeTextBox.Focus();
            return;
        }

        if (quantityChange == 0)
        {
            SetStatus(
                "Quantity change cannot be zero. Enter a positive number to add stock or a negative number to remove it.",
                isError: true);
            QuantityChangeTextBox.Focus();
            return;
        }

        var reason = ReasonTextBox.Text;

        if (string.IsNullOrWhiteSpace(reason))
        {
            SetStatus("Enter a reason for the adjustment.", isError: true);
            ReasonTextBox.Focus();
            return;
        }

        if (reason.Trim().Length > InventoryAdjustment.MaximumReasonLength)
        {
            SetStatus(
                $"Reason cannot exceed {InventoryAdjustment.MaximumReasonLength} characters.",
                isError: true);
            ReasonTextBox.Focus();
            return;
        }

        if (quantityChange < 0)
        {
            long removal = -(long)quantityChange;
            if (removal > selected.StockQuantity)
            {
                SetStatus(
                    $"Cannot remove {removal} from {selected.Sku}: current stock is {selected.StockQuantity}, so the maximum you can remove is {selected.StockQuantity}.",
                    isError: true);
                QuantityChangeTextBox.Focus();
                return;
            }
        }

        SetBusy(true);

        try
        {
            await using var dbContext = createDbContext();
            var service = new InventoryService(new EfRetailRepository(dbContext), timeProvider);
            var adjustment = await service.AdjustAsync(
                selected.Sku,
                quantityChange,
                reason,
                StaffActor);

            QuantityChangeTextBox.Clear();
            ReasonTextBox.Clear();

            await RefreshProductsAsync(
                selected.Sku,
                $"Recorded {adjustment.QuantityChange:+0;-0} for {selected.Sku}. " +
                $"New stock: {adjustment.ResultingQuantity}.");
        }
        catch (BusinessRuleException exception)
        {
            SetStatus(exception.Message, isError: true);
        }
        catch (ProductConflictException exception)
        {
            SetStatus($"Inventory changed elsewhere. Refresh and try again. {exception.Message}", isError: true);
        }
        catch (ArgumentException exception) when (string.Equals(
            exception.ParamName,
            "reason",
            StringComparison.Ordinal))
        {
            if (reason.Trim().Length > InventoryAdjustment.MaximumReasonLength)
            {
                SetStatus(
                    $"Reason cannot exceed {InventoryAdjustment.MaximumReasonLength} characters.",
                    isError: true);
            }
            else
            {
                SetStatus("Enter a reason for the adjustment.", isError: true);
            }

            ReasonTextBox.Focus();
        }
        catch (ArgumentException)
        {
            SetStatus(
                "The adjustment details were not valid. Enter a whole-number quantity change and a reason, then try again.",
                isError: true);
            QuantityChangeTextBox.Focus();
        }
        catch (Exception exception)
        {
            SetStatus($"The adjustment could not be saved. {exception.Message}", isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshProductsAsync(
        string? preferredSku = null,
        string? successMessage = null)
    {
        SetBusy(true);

        try
        {
            await using var dbContext = createDbContext();
            var repository = new EfRetailRepository(dbContext);
            var products = await repository.GetProductsAsync(includeArchived: true);
            var rows = products.Select(InventoryProductRow.FromProduct).ToList();

            InventoryGrid.ItemsSource = rows;
            InventoryGrid.SelectedItem = rows.FirstOrDefault(
                row => string.Equals(row.Sku, preferredSku, StringComparison.OrdinalIgnoreCase));

            if (InventoryGrid.SelectedItem is null && rows.Count > 0)
            {
                InventoryGrid.SelectedIndex = 0;
            }

            SetStatus(successMessage ?? $"Loaded {rows.Count} products from local storage.");
        }
        catch (Exception exception)
        {
            SetStatus($"Inventory could not be loaded. {exception.Message}", isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdateSelectionPanel()
    {
        if (InventoryGrid.SelectedItem is not InventoryProductRow selected)
        {
            SelectedSkuTextBlock.Text = "Select a product";
            SelectedDescriptionTextBlock.Text = string.Empty;
            SelectedStockTextBlock.Text = string.Empty;
            ApplyAdjustmentButton.IsEnabled = false;
            ViewHistoryButton.IsEnabled = false;
            EditProductButton.IsEnabled = false;
            ArchiveToggleButton.IsEnabled = false;
            ArchiveToggleButton.Content = "Archive selected";
            return;
        }

        SelectedSkuTextBlock.Text = selected.Sku;
        SelectedDescriptionTextBlock.Text = selected.Description;
        SelectedStockTextBlock.Text = selected.IsArchived
            ? $"Stock: {selected.StockQuantity} · Archived"
            : $"Current stock: {selected.StockQuantity}";

        ApplyAdjustmentButton.IsEnabled = !isBusy && !selected.IsArchived;
        ViewHistoryButton.IsEnabled = !isBusy;
        EditProductButton.IsEnabled = !isBusy && !selected.IsArchived;
        ArchiveToggleButton.IsEnabled = !isBusy;
        ArchiveToggleButton.Content = selected.IsArchived ? "Unarchive selected" : "Archive selected";
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        InventoryGrid.IsEnabled = !busy;
        NewProductButton.IsEnabled = !busy;
        RefreshInventoryButton.IsEnabled = !busy;
        EditProductButton.IsEnabled = !busy;
        ArchiveToggleButton.IsEnabled = !busy;
        QuantityChangeTextBox.IsEnabled = !busy;
        ReasonTextBox.IsEnabled = !busy;
        UpdateSelectionPanel();
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = isError
            ? System.Windows.Media.Brushes.DarkRed
            : (System.Windows.Media.Brush)FindResource("InkBrush");
    }
}
