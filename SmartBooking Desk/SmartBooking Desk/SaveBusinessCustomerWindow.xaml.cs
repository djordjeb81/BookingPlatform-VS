using SmartBooking_Desk.Models.BusinessCustomers;
using SmartBooking_Desk.Services;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace SmartBooking_Desk
{
    public partial class SaveBusinessCustomerWindow : Window
    {
        private readonly BookingApiClient _apiClient;
        private readonly long _businessId;
        private readonly BusinessCustomerItemDto? _existingCustomer;
        private bool _isSaving;

        public BusinessCustomerItemDto? SavedCustomer { get; private set; }

        public SaveBusinessCustomerWindow(
            BookingApiClient apiClient,
            long businessId,
            string? fullName,
            string? phone,
            string? email)
            : this(apiClient, businessId, null)
        {
            TextBoxFullName.Text = fullName?.Trim() ?? string.Empty;
            TextBoxPhone.Text = phone?.Trim() ?? string.Empty;
            TextBoxEmail.Text = email?.Trim() ?? string.Empty;
        }

        public SaveBusinessCustomerWindow(
            BookingApiClient apiClient,
            long businessId,
            BusinessCustomerItemDto? existingCustomer = null)
        {
            InitializeComponent();

            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _businessId = businessId;
            _existingCustomer = existingCustomer;

            if (_existingCustomer is not null)
            {
                Title = "Izmeni klijenta";
                ButtonSave.Content = "Sačuvaj izmene";
                TextBlockStatus.Text = "Izmenite podatke klijenta.";

                TextBoxFullName.Text = _existingCustomer.FullName;
                TextBoxPhone.Text = _existingCustomer.Phone ?? string.Empty;
                TextBoxEmail.Text = _existingCustomer.Email ?? string.Empty;
                TextBoxNotes.Text = _existingCustomer.Notes ?? string.Empty;
            }

            Loaded += SaveBusinessCustomerWindow_Loaded;
        }

        private void SaveBusinessCustomerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TextBoxFullName.Focus();
            TextBoxFullName.SelectAll();
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (_isSaving)
                return;

            try
            {
                SetBusy(true, _existingCustomer is null ? "Čuvam klijenta..." : "Čuvam izmene...");

                if (_existingCustomer is null)
                    SavedCustomer = await _apiClient.CreateBusinessCustomerAsync(BuildCreateRequest());
                else
                    SavedCustomer = await _apiClient.UpdateBusinessCustomerAsync(_existingCustomer.Id, BuildUpdateRequest());

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                SetStatus(_existingCustomer is null ? "Klijent nije sačuvan." : "Izmene nisu sačuvane.");
                MessageBox.Show(
                    ex.Message,
                    _existingCustomer is null ? "Čuvanje klijenta nije uspelo" : "Izmena klijenta nije uspela",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private CreateBusinessCustomerRequestDto BuildCreateRequest()
        {
            var fullName = NullIfEmpty(TextBoxFullName.Text);
            var phone = NullIfEmpty(TextBoxPhone.Text);
            var email = NullIfEmpty(TextBoxEmail.Text);
            var notes = NullIfEmpty(TextBoxNotes.Text);

            ValidateInput(fullName, phone, email);

            return new CreateBusinessCustomerRequestDto
            {
                BusinessId = _businessId,
                AppUserId = null,
                FullName = fullName!,
                Phone = phone,
                Email = email,
                Notes = notes
            };
        }

        private UpdateBusinessCustomerRequestDto BuildUpdateRequest()
        {
            var fullName = NullIfEmpty(TextBoxFullName.Text);
            var phone = NullIfEmpty(TextBoxPhone.Text);
            var email = NullIfEmpty(TextBoxEmail.Text);
            var notes = NullIfEmpty(TextBoxNotes.Text);

            ValidateInput(fullName, phone, email);

            return new UpdateBusinessCustomerRequestDto
            {
                AppUserId = _existingCustomer?.AppUserId,
                FullName = fullName!,
                Phone = phone,
                Email = email,
                Notes = notes,
                IsActive = _existingCustomer?.IsActive ?? true
            };
        }

        private static void ValidateInput(string? fullName, string? phone, string? email)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new InvalidOperationException("Unesite ime i prezime klijenta.");

            if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("Unesite bar telefon ili email klijenta.");
        }

        private static string? NullIfEmpty(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private void SetBusy(bool isBusy, string? statusMessage = null)
        {
            _isSaving = isBusy;

            ButtonSave.IsEnabled = !isBusy;
            ButtonCancel.IsEnabled = !isBusy;
            TextBoxFullName.IsEnabled = !isBusy;
            TextBoxPhone.IsEnabled = !isBusy;
            TextBoxEmail.IsEnabled = !isBusy;
            TextBoxNotes.IsEnabled = !isBusy;

            if (!string.IsNullOrWhiteSpace(statusMessage))
                SetStatus(statusMessage);
        }

        private void SetStatus(string message)
        {
            TextBlockStatus.Text = message;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}