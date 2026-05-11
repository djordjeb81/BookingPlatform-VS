using SmartBooking_Desk.Models.BusinessCustomers;
using SmartBooking_Desk.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SmartBooking_Desk
{
    public partial class BusinessCustomersWindow : Window
    {
        private readonly BookingApiClient _apiClient;
        private readonly long _businessId;
        private readonly DispatcherTimer _searchTimer;

        private List<BusinessCustomerItemDto> _allCustomers = new();
        private bool _isBusy;
        private string _lastSearchText = string.Empty;

        public BusinessCustomersWindow(BookingApiClient apiClient, long businessId)
        {
            InitializeComponent();

            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _businessId = businessId;

            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchTimer.Tick += SearchTimer_Tick;

            Loaded += BusinessCustomersWindow_Loaded;
        }

        private async void BusinessCustomersWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCustomersAsync();
            TextBoxSearch.Focus();
        }

        private async Task LoadCustomersAsync()
        {
            try
            {
                SetBusy(true, "Učitavam klijente...");

                _allCustomers = await _apiClient.GetBusinessCustomersAsync(_businessId);

                DataGridCustomers.ItemsSource = _allCustomers
                    .OrderByDescending(x => x.IsActive)
                    .ThenBy(x => x.FullName)
                    .ToList();

                SetStatus($"Učitano klijenata: {_allCustomers.Count}");
            }
            catch (Exception ex)
            {
                DataGridCustomers.ItemsSource = null;
                SetStatus("Klijenti nisu učitani.");

                MessageBox.Show(
                    ex.Message,
                    "Učitavanje klijenata nije uspelo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void TextBoxSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();

            _lastSearchText = TextBoxSearch.Text?.Trim() ?? string.Empty;

            if (_lastSearchText.Length == 0)
            {
                ApplyLocalFilter();
                return;
            }

            _searchTimer.Start();
        }

        private async void SearchTimer_Tick(object? sender, EventArgs e)
        {
            _searchTimer.Stop();

            if (_lastSearchText.Length < 2)
            {
                ApplyLocalFilter();
                return;
            }

            await SearchCustomersAsync(_lastSearchText);
        }

        private async Task SearchCustomersAsync(string query)
        {
            try
            {
                SetBusy(true, "Pretražujem klijente...");

                var customers = await _apiClient.SearchBusinessCustomersAsync(
                    _businessId,
                    query,
                    limit: 20);

                DataGridCustomers.ItemsSource = customers
                    .OrderBy(x => x.FullName)
                    .ToList();

                SetStatus($"Pronađeno klijenata: {customers.Count}");
            }
            catch (Exception ex)
            {
                SetStatus("Pretraga nije uspela.");

                MessageBox.Show(
                    ex.Message,
                    "Pretraga klijenata nije uspela",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ApplyLocalFilter()
        {
            var query = _lastSearchText;

            var items = _allCustomers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                items = items.Where(x =>
                    Contains(x.FullName, query) ||
                    Contains(x.Phone, query) ||
                    Contains(x.Email, query));
            }

            var result = items
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.FullName)
                .ToList();

            DataGridCustomers.ItemsSource = result;
            SetStatus($"Prikazano klijenata: {result.Count}");
        }

        private static bool Contains(string? value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private async void ButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            TextBoxSearch.Text = string.Empty;
            await LoadCustomersAsync();
        }

        private async void ButtonNew_Click(object sender, RoutedEventArgs e)
        {
            var window = new SaveBusinessCustomerWindow(
                _apiClient,
                _businessId,
                fullName: null,
                phone: null,
                email: null)
            {
                Owner = this
            };

            if (window.ShowDialog() != true)
                return;

            await LoadCustomersAsync();

            if (window.SavedCustomer is not null)
                SelectCustomer(window.SavedCustomer.Id);
        }

        private async void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            await EditSelectedCustomerAsync();
        }

        private async void DataGridCustomers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            await EditSelectedCustomerAsync();
        }

        private async Task EditSelectedCustomerAsync()
        {
            if (DataGridCustomers.SelectedItem is not BusinessCustomerItemDto selectedCustomer)
            {
                MessageBox.Show(
                    "Prvo izaberite klijenta iz liste.",
                    "Nije izabran klijent",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var window = new SaveBusinessCustomerWindow(
                _apiClient,
                _businessId,
                selectedCustomer)
            {
                Owner = this
            };

            if (window.ShowDialog() != true)
                return;

            await LoadCustomersAsync();

            if (window.SavedCustomer is not null)
                SelectCustomer(window.SavedCustomer.Id);
        }

        private void DataGridCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ButtonEdit.IsEnabled = DataGridCustomers.SelectedItem is not null && !_isBusy;
        }

        private void SelectCustomer(long customerId)
        {
            if (DataGridCustomers.ItemsSource is not IEnumerable<BusinessCustomerItemDto> items)
                return;

            var customer = items.FirstOrDefault(x => x.Id == customerId);
            if (customer is null)
                return;

            DataGridCustomers.SelectedItem = customer;
            DataGridCustomers.ScrollIntoView(customer);
        }

        private void SetBusy(bool isBusy, string? statusMessage = null)
        {
            _isBusy = isBusy;

            TextBoxSearch.IsEnabled = !isBusy;
            ButtonRefresh.IsEnabled = !isBusy;
            ButtonNew.IsEnabled = !isBusy;
            ButtonEdit.IsEnabled = !isBusy && DataGridCustomers.SelectedItem is not null;
            DataGridCustomers.IsEnabled = !isBusy;
            ButtonClose.IsEnabled = !isBusy;

            if (!string.IsNullOrWhiteSpace(statusMessage))
                SetStatus(statusMessage);
        }

        private void SetStatus(string message)
        {
            TextBlockStatus.Text = message;
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}