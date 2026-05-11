using SmartBooking_Desk.Models.Appointments;
using SmartBooking_Desk.Models.BusinessCustomers;
using SmartBooking_Desk.Models.Resources;
using SmartBooking_Desk.Models.Scheduling;
using SmartBooking_Desk.Models.Services;
using SmartBooking_Desk.Models.Staff;
using SmartBooking_Desk.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SmartBooking_Desk
{
    public partial class CreateAppointmentWindow : Window
    {
        private readonly BookingApiClient _apiClient;
        private readonly long _businessId;
        private readonly DispatcherTimer _customerSearchTimer;
        private bool _isSaving;
        private string _lastCustomerSearchText = string.Empty;
        private bool _isApplyingCustomerSuggestion;
        private long? _selectedBusinessCustomerId;

        public AppointmentListItemDto? CreatedAppointment { get; private set; }

        public CreateAppointmentWindow(BookingApiClient apiClient, long businessId)
        {
            InitializeComponent();

            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _businessId = businessId;

            _customerSearchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _customerSearchTimer.Tick += CustomerSearchTimer_Tick;

            Loaded += CreateAppointmentWindow_Loaded;
        }

        private async void CreateAppointmentWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                SetBusy(true, "Učitavam podatke...");
                await LoadLookupsAsync();

                DatePickerAppointmentDate.SelectedDate = DateTime.Today;

                TextBoxCustomerSearch.Focus();
                SetStatus("Unesite podatke za novi termin.");
            }
            catch (Exception ex)
            {
                SetStatus("Greška pri učitavanju podataka.");
                MessageBox.Show(
                    ex.Message,
                    "Učitavanje nije uspelo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task LoadLookupsAsync(CancellationToken cancellationToken = default)
        {
            var services = await _apiClient.GetServicesAsync(_businessId, cancellationToken);
            var staff = await _apiClient.GetStaffAsync(_businessId, cancellationToken);

            ComboBoxService.ItemsSource = services
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToList();

            ComboBoxStaff.ItemsSource = staff
                .Where(x => x.IsActive && x.IsBookable)
                .OrderBy(x => x.DisplayName)
                .ToList();
            ComboBoxService.SelectedIndex = ComboBoxService.Items.Count > 0 ? 0 : -1;
            ComboBoxStaff.SelectedIndex = ComboBoxStaff.Items.Count > 0 ? 0 : -1;
        }


        private async void ComboBoxAvailableSlots_DropDownOpened(object sender, EventArgs e)
        {
            try
            {
                await LoadAvailableSlotsAsync();
            }
            catch (Exception ex)
            {
                ComboBoxAvailableSlots.ItemsSource = null;
                SetStatus("Slobodni termini nisu učitani.");
                MessageBox.Show(
                    ex.Message,
                    "Učitavanje slobodnih termina nije uspelo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async void AppointmentInputs_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _isSaving)
                return;

            try
            {
                await LoadAvailableSlotsAsync();
            }
            catch
            {
                ComboBoxAvailableSlots.ItemsSource = null;
                ComboBoxAvailableSlots.SelectedItem = null;
                SetStatus("Slobodni termini trenutno nisu dostupni za izabrane podatke.");
            }
        }

        private async Task LoadAvailableSlotsAsync(CancellationToken cancellationToken = default)
        {
            ComboBoxAvailableSlots.ItemsSource = null;
            ComboBoxAvailableSlots.SelectedItem = null;

            if (DatePickerAppointmentDate.SelectedDate is null)
            {
                SetStatus("Prvo izaberite datum.");
                return;
            }

            if (ComboBoxService.SelectedItem is not ServiceItemDto selectedService)
            {
                SetStatus("Prvo izaberite uslugu.");
                return;
            }

            if (ComboBoxStaff.SelectedItem is not StaffItemDto selectedStaff)
            {
                SetStatus("Prvo izaberite radnika.");
                return;
            }



            SetStatus("Učitavam slobodne termine...");

            var selectedDate = DatePickerAppointmentDate.SelectedDate.Value.Date;

            var slots = await _apiClient.GetAvailableSlotsAsync(
                _businessId,
                selectedService.Id,
                selectedStaff.Id,
                null,
                selectedDate,
                cancellationToken);

            System.Diagnostics.Debug.WriteLine("========== DESKTOP RAW AVAILABLE SLOTS ==========");
            System.Diagnostics.Debug.WriteLine($"SelectedDate={selectedDate:O} Kind={selectedDate.Kind}");

            foreach (var slot in slots.Take(30))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"RAW SLOT | Label={slot.StartLabel}-{slot.EndLabel} | " +
                    $"StartAtUtc={slot.StartAtUtc:O} Kind={slot.StartAtUtc.Kind} | " +
                    $"EndAtUtc={slot.EndAtUtc:O} Kind={slot.EndAtUtc.Kind}");
            }

            System.Diagnostics.Debug.WriteLine("=================================================");

            var orderedSlots = slots
                .OrderBy(x => x.StartAtUtc)
                .Select(x => new AvailableSlotOption
                {
                    StartAtUtc = EnsureUtc(x.StartAtUtc),
                    EndAtUtc = EnsureUtc(x.EndAtUtc),
                    DisplayText = $"{x.StartLabel} - {x.EndLabel}"
                })
                .ToList();

            ComboBoxAvailableSlots.DisplayMemberPath = nameof(AvailableSlotOption.DisplayText);
            ComboBoxAvailableSlots.ItemsSource = orderedSlots;
            ComboBoxAvailableSlots.SelectedIndex = orderedSlots.Count > 0 ? 0 : -1;

            System.Diagnostics.Debug.WriteLine("========== DESKTOP NORMALIZED AVAILABLE SLOTS ==========");

            foreach (var slot in orderedSlots.Take(30))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"OPTION | Text={slot.DisplayText} | " +
                    $"StartAtUtc={slot.StartAtUtc:O} Kind={slot.StartAtUtc.Kind} | " +
                    $"EndAtUtc={slot.EndAtUtc:O} Kind={slot.EndAtUtc.Kind}");
            }

            System.Diagnostics.Debug.WriteLine("=======================================================");

            if (orderedSlots.Count == 0)
                SetStatus("Nema slobodnih termina za izabrani datum.");
            else
                SetStatus("Izaberite jedan od ponuđenih slobodnih termina.");
        }

        private void TextBoxCustomerSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingCustomerSuggestion)
                return;

            var text = TextBoxCustomerSearch.Text?.Trim() ?? string.Empty;

            _customerSearchTimer.Stop();

            if (text.Length < 2)
            {
                HideCustomerSuggestions();
                return;
            }

            _lastCustomerSearchText = text;
            _customerSearchTimer.Start();
        }

        private async void CustomerSearchTimer_Tick(object? sender, EventArgs e)
        {
            _customerSearchTimer.Stop();

            var query = _lastCustomerSearchText.Trim();

            if (query.Length < 2)
            {
                HideCustomerSuggestions();
                return;
            }

            await SearchCustomersAsync(query);
        }

        private async Task SearchCustomersAsync(string query)
        {
            try
            {
                if (query.Length < 2)
                {
                    HideCustomerSuggestions();
                    return;
                }

                var customers = await _apiClient.SearchBusinessCustomersAsync(
                    _businessId,
                    query,
                    limit: 10);

                if (customers.Count == 0)
                {
                    HideCustomerSuggestions();
                    return;
                }

                ListBoxCustomerSuggestions.ItemsSource = customers;
                BorderCustomerSuggestions.Visibility = Visibility.Visible;
            }
            catch
            {
                HideCustomerSuggestions();
            }
        }

        private void ListBoxCustomerSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListBoxCustomerSuggestions.SelectedItem is not BusinessCustomerItemDto customer)
                return;

            ApplyCustomerSuggestion(customer);
        }

        private void ApplyCustomerSuggestion(BusinessCustomerItemDto customer)
        {
            _isApplyingCustomerSuggestion = true;

            try
            {
                _selectedBusinessCustomerId = customer.Id;

                TextBoxCustomerSearch.Text = customer.DisplayText;

                TextBoxCustomerName.Text = customer.FullName;
                TextBoxCustomerPhone.Text = customer.Phone ?? string.Empty;
                TextBoxCustomerEmail.Text = customer.Email ?? string.Empty;

                TextBlockSelectedCustomerStatus.Text = "Izabran postojeći klijent iz baze.";
                ButtonRememberCustomer.Content = "Klijent izabran";
                ButtonRememberCustomer.IsEnabled = false;

                HideCustomerSuggestions();
            }
            finally
            {
                _isApplyingCustomerSuggestion = false;
            }
        }

        private void HideCustomerSuggestions()
        {
            ListBoxCustomerSuggestions.ItemsSource = null;
            ListBoxCustomerSuggestions.SelectedItem = null;
            BorderCustomerSuggestions.Visibility = Visibility.Collapsed;
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (_isSaving)
                return;

            try
            {
                var request = BuildRequest();
                SetBusy(true, "Čuvam termin...");

                CreatedAppointment = await _apiClient.CreateOwnerAppointmentAsync(request);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                SetStatus("Termin nije sačuvan.");
                MessageBox.Show(
                    ex.Message,
                    "Čuvanje termina nije uspelo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private CreateOwnerAppointmentRequestDto BuildRequest()
        {
            if (DatePickerAppointmentDate.SelectedDate is null)
                throw new InvalidOperationException("Izaberite datum termina.");

            if (ComboBoxAvailableSlots.SelectedItem is not AvailableSlotOption selectedSlot)
                throw new InvalidOperationException("Izaberite slobodan termin.");

            if (ComboBoxService.SelectedItem is not ServiceItemDto selectedService)
                throw new InvalidOperationException("Izaberite uslugu.");

            if (ComboBoxStaff.SelectedItem is not StaffItemDto selectedStaff)
                throw new InvalidOperationException("Izaberite radnika.");

            var startAtUtc = selectedSlot.StartAtUtc;

            System.Diagnostics.Debug.WriteLine("========== DESKTOP BUILD REQUEST ==========");
            System.Diagnostics.Debug.WriteLine(
                $"SelectedSlot | Text={selectedSlot.DisplayText} | " +
                $"StartAtUtc={selectedSlot.StartAtUtc:O} Kind={selectedSlot.StartAtUtc.Kind} | " +
                $"EndAtUtc={selectedSlot.EndAtUtc:O} Kind={selectedSlot.EndAtUtc.Kind}");
            System.Diagnostics.Debug.WriteLine($"Request StartAtUtc will be: {startAtUtc:O} Kind={startAtUtc.Kind}");
            System.Diagnostics.Debug.WriteLine("===========================================");

            var customerName = NullIfEmpty(TextBoxCustomerName.Text);
            var customerPhone = NullIfEmpty(TextBoxCustomerPhone.Text);
            var notes = NullIfEmpty(TextBoxNotes.Text);

            if (string.IsNullOrWhiteSpace(customerName))
                throw new InvalidOperationException("Unesite ime klijenta.");

            return new CreateOwnerAppointmentRequestDto
            {
                BusinessId = _businessId,
                ServiceId = selectedService.Id,
                PrimaryStaffMemberId = selectedStaff.Id,
                ResourceId = null,
                BusinessCustomerId = _selectedBusinessCustomerId,
                StartAtUtc = startAtUtc,
                CustomerName = customerName,
                CustomerPhone = customerPhone,
                Notes = notes,
                IgnoreAvailabilityRules = false,
                IgnoreWorkingHours = false,
                IgnoreTimeOffBlocks = false,
                IgnoreAppointmentConflicts = false,
                FinalDurationMin = null
            };
        }

        private static string? NullIfEmpty(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),

                // Važno:
                // Server mora da šalje UTC vreme.
                // Ako Kind stigne kao Unspecified, tretiramo ga kao UTC,
                // ali to ne sme biti mesto gde se ispravlja pogrešan server-side slot.
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetBusy(bool isBusy, string? statusMessage = null)
        {
            _isSaving = isBusy;

            ButtonSave.IsEnabled = !isBusy;
            ButtonCancel.IsEnabled = !isBusy;
            ComboBoxService.IsEnabled = !isBusy;
            ComboBoxStaff.IsEnabled = !isBusy;
            DatePickerAppointmentDate.IsEnabled = !isBusy;
            ComboBoxAvailableSlots.IsEnabled = !isBusy;
            TextBoxCustomerSearch.IsEnabled = !isBusy;
            TextBoxCustomerName.IsEnabled = !isBusy;
            TextBoxCustomerPhone.IsEnabled = !isBusy;
            TextBoxCustomerEmail.IsEnabled = !isBusy;
            ButtonRememberCustomer.IsEnabled = !isBusy && !_selectedBusinessCustomerId.HasValue;
            TextBoxNotes.IsEnabled = !isBusy;

            if (!string.IsNullOrWhiteSpace(statusMessage))
                SetStatus(statusMessage);
        }

        private void SetStatus(string message)
        {
            TextBlockStatus.Text = message;
        }

        private sealed class AvailableSlotOption
        {
            public DateTime StartAtUtc { get; set; }
            public DateTime EndAtUtc { get; set; }
            public string DisplayText { get; set; } = "";
        }

        private void CustomerManualFields_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingCustomerSuggestion)
                return;

            _selectedBusinessCustomerId = null;

            if (TextBlockSelectedCustomerStatus is not null)
                TextBlockSelectedCustomerStatus.Text = string.Empty;

            if (ButtonRememberCustomer is not null)
            {
                ButtonRememberCustomer.Content = "Zapamti klijenta";
                ButtonRememberCustomer.IsEnabled = true;
            }
        }

        private void ButtonRememberCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBusinessCustomerId.HasValue)
            {
                MessageBox.Show(
                    "Ovaj klijent je već izabran iz baze.",
                    "Klijent je već poznat",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var fullName = NullIfEmpty(TextBoxCustomerName.Text);
            var phone = NullIfEmpty(TextBoxCustomerPhone.Text);
            var email = NullIfEmpty(TextBoxCustomerEmail.Text);

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show(
                    "Prvo unesite ime i prezime klijenta.",
                    "Nedostaju podaci",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                TextBoxCustomerName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show(
                    "Unesite bar telefon ili email klijenta.",
                    "Nedostaju podaci",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                TextBoxCustomerPhone.Focus();
                return;
            }

            var window = new SaveBusinessCustomerWindow(
                _apiClient,
                _businessId,
                fullName,
                phone,
                email)
            {
                Owner = this
            };

            if (window.ShowDialog() != true || window.SavedCustomer is null)
                return;

            var savedCustomer = window.SavedCustomer;

            _isApplyingCustomerSuggestion = true;

            try
            {
                _selectedBusinessCustomerId = savedCustomer.Id;

                TextBoxCustomerSearch.Text = savedCustomer.DisplayText;
                TextBoxCustomerName.Text = savedCustomer.FullName;
                TextBoxCustomerPhone.Text = savedCustomer.Phone ?? string.Empty;
                TextBoxCustomerEmail.Text = savedCustomer.Email ?? string.Empty;

                TextBlockSelectedCustomerStatus.Text = "Klijent je zapamćen u bazi.";
                ButtonRememberCustomer.Content = "Klijent zapamćen";
                ButtonRememberCustomer.IsEnabled = false;
            }
            finally
            {
                _isApplyingCustomerSuggestion = false;
            }
        }
    }

}