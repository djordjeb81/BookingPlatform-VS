using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmartBooking_Desk.Models.Auth;
using SmartBooking_Desk.Services;
using SmartBooking_Desk.Services.Licensing;

namespace SmartBooking_Desk
{
    public partial class LoginWindow : Window
    {
        private readonly BookingApiClient _apiClient = new();
        private readonly LicenseGuardService _licenseGuardService = new();
        private readonly AppSettingsService _appSettingsService = new();

        public AuthResponseDto? AuthResult { get; private set; }
        public bool RegistrationCompletedButLicensePending { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();

            Loaded += LoginWindow_Loaded;
            PreviewKeyDown += LoginWindow_PreviewKeyDown;

            LoadSavedEmail();
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(LoginEmailTextBox.Text))
            {
                LoginPasswordBox.Focus();
                Keyboard.Focus(LoginPasswordBox);
                return;
            }

            LoginEmailTextBox.Focus();
            Keyboard.Focus(LoginEmailTextBox);
        }

        private void LoginWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (Keyboard.FocusedElement is TextBox textBox && textBox.AcceptsReturn)
                return;

            e.Handled = true;

            if (IsFocusInsideLoginTab())
            {
                LoginButton_Click(LoginButton, new RoutedEventArgs());
                return;
            }

            RegisterButton_Click(RegisterButton, new RoutedEventArgs());
        }

        private bool IsFocusInsideLoginTab()
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused is null)
                return true;

            return IsDescendantOf(focused, LoginEmailTextBox)
                   || IsDescendantOf(focused, LoginPasswordBox)
                   || IsDescendantOf(focused, LoginButton);
        }

        private static bool IsDescendantOf(DependencyObject source, DependencyObject target)
        {
            var current = source;

            while (current is not null)
            {
                if (ReferenceEquals(current, target))
                    return true;

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void LoadSavedEmail()
        {
            var settings = _appSettingsService.Load();
            LoginEmailTextBox.Text = settings.LastLoginEmail ?? string.Empty;
        }

        private void SaveLastLoginEmail(string email)
        {
            var settings = _appSettingsService.Load();
            settings.LastLoginEmail = email?.Trim() ?? "";
            _appSettingsService.Save(settings);
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Prijava je u toku...";

                var email = LoginEmailTextBox.Text.Trim();
                var password = LoginPasswordBox.Password;

                if (string.IsNullOrWhiteSpace(email))
                {
                    StatusTextBlock.Text = "Unesite email.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    StatusTextBlock.Text = "Unesite lozinku.";
                    return;
                }

                var auth = await _apiClient.LoginAsync(email, password);

                if (string.IsNullOrWhiteSpace(auth.Token))
                {
                    StatusTextBlock.Text = "Prijava nije uspela.";
                    return;
                }

                SaveLastLoginEmail(email);

                StatusTextBlock.Text = "Prijava je uspela. Proverava se licenca uređaja...";

                var licenseResult = await _licenseGuardService.EnsureLicenseAsync(
                    auth.Email ?? email,
                    auth.Token);

                if (licenseResult.IsAllowed)
                {
                    AuthResult = auth;
                    DialogResult = true;
                    Close();
                    return;
                }

                if (licenseResult.IsPendingApproval)
                {
                    MessageBox.Show(
                        "Prijava je uspela, ali licenca za ovaj uređaj još nije odobrena.\n\n" +
                        "Kada licenca bude odobrena, bićete obavešteni mailom.\n" +
                        "Nakon odobrenja ponovo pokrenite aplikaciju i prijavite se.",
                        "Čeka se odobrenje licence",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = false;
                    Close();
                    return;
                }

                if (licenseResult.IsBlocked)
                {
                    MessageBox.Show(
                        "Prijava je uspela, ali je ovaj uređaj blokiran.\n" +
                        "Kontaktirajte administratora.",
                        "Uređaj je blokiran",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    DialogResult = false;
                    Close();
                    return;
                }

                MessageBox.Show(
                    licenseResult.Message,
                    "Licenca nije spremna",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = ex.Message;
            }
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Kreiranje naloga i biznisa je u toku...";

                var fullName = RegisterFullNameTextBox.Text.Trim();
                var email = RegisterEmailTextBox.Text.Trim();
                var password = RegisterPasswordBox.Password;
                var businessName = BusinessNameTextBox.Text.Trim();
                var slotIntervalText = SlotIntervalTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    StatusTextBlock.Text = "Unesite ime i prezime.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    StatusTextBlock.Text = "Unesite email za prijavu.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    StatusTextBlock.Text = "Unesite lozinku.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(businessName))
                {
                    StatusTextBlock.Text = "Unesite naziv biznisa.";
                    return;
                }

                if (!int.TryParse(slotIntervalText, out var slotIntervalMin) || slotIntervalMin <= 0)
                {
                    StatusTextBlock.Text = "Interval termina mora biti pozitivan broj.";
                    return;
                }

                var latitude = TryParseNullableDouble(LatitudeTextBox.Text);
                var longitude = TryParseNullableDouble(LongitudeTextBox.Text);

                var request = new RegisterOwnerRequestDto
                {
                    FullName = fullName,
                    Email = email,
                    Password = password,
                    BusinessName = businessName,
                    BusinessType = GetSelectedBusinessType(),
                    Description = NullIfEmpty(BusinessDescriptionTextBox.Text),
                    Phone = NullIfEmpty(BusinessPhoneTextBox.Text),
                    BusinessEmail = NullIfEmpty(BusinessEmailTextBox.Text),
                    SlotIntervalMin = slotIntervalMin,
                    Street = NullIfEmpty(StreetTextBox.Text),
                    StreetNumber = NullIfEmpty(StreetNumberTextBox.Text),
                    City = NullIfEmpty(CityTextBox.Text),
                    PostalCode = NullIfEmpty(PostalCodeTextBox.Text),
                    Country = NullIfEmpty(CountryTextBox.Text),
                    Latitude = latitude,
                    Longitude = longitude,
                    GooglePlaceId = NullIfEmpty(GooglePlaceIdTextBox.Text)
                };

                var auth = await _apiClient.RegisterOwnerAsync(request);

                if (string.IsNullOrWhiteSpace(auth.Token))
                {
                    StatusTextBlock.Text = "Kreiranje naloga i biznisa nije uspelo.";
                    return;
                }

                SaveLastLoginEmail(email);

                StatusTextBlock.Text = "Nalog je kreiran. Proverava se licenca uređaja...";

                var licenseResult = await _licenseGuardService.EnsureLicenseAsync(
                    auth.Email ?? email,
                    auth.Token);

                if (licenseResult.IsAllowed)
                {
                    AuthResult = auth;
                    DialogResult = true;
                    Close();
                    return;
                }

                if (licenseResult.IsPendingApproval)
                {
                    RegistrationCompletedButLicensePending = true;

                    MessageBox.Show(
                        "Nalog i biznis su uspešno kreirani.\n\n" +
                        "Ovaj uređaj je prijavljen za licencu i trenutno čeka odobrenje.\n" +
                        "Kada licenca bude odobrena, bićete obavešteni mailom.\n\n" +
                        "Nakon odobrenja ponovo pokrenite aplikaciju i prijavite se.",
                        "Čeka se odobrenje licence",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = false;
                    Close();
                    return;
                }

                if (licenseResult.IsBlocked)
                {
                    MessageBox.Show(
                        "Nalog i biznis su kreirani, ali je ovaj uređaj blokiran.\n" +
                        "Kontaktirajte administratora.",
                        "Uređaj je blokiran",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    DialogResult = false;
                    Close();
                    return;
                }

                MessageBox.Show(
                    licenseResult.Message,
                    "Licenca nije spremna",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = ex.Message;
            }
        }

        private int GetSelectedBusinessType()
        {
            if (BusinessTypeComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is string tagText &&
                int.TryParse(tagText, out var value))
            {
                return value;
            }

            return 0;
        }

        private static string? NullIfEmpty(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private static double? TryParseNullableDouble(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (double.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            return null;
        }
    }
}