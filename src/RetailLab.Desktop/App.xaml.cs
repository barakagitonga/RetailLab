using System.IO;
using System.Windows;
using RetailLab.Data;

namespace RetailLab.Desktop;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var configuredDataDirectory = Environment.GetEnvironmentVariable("RETAILLAB_DATA_DIRECTORY");
            var databaseDirectory = string.IsNullOrWhiteSpace(configuredDataDirectory)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RetailLab",
                    "LabPrototype1")
                : Path.GetFullPath(configuredDataDirectory);

            var database = new RetailLabSqliteDatabase(databaseDirectory);

            await using (var dbContext = database.CreateDbContext())
            {
                await DatabaseInitializer.InitializeAsync(dbContext);
            }

            MainWindow = new MainWindow(database.CreateDbContext, TimeProvider.System);
            MainWindow.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"RetailLab could not start because its local database is unavailable.\n\n{exception.Message}",
                "RetailLab startup problem",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }
}
