using SmartBooking_Desk.Models.Appointments;
using SmartBooking_Desk.Models.Scheduling;
using SmartBooking_Desk.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SmartBooking_Desk
{
    public partial class ProposeAppointmentTimeWindow : Window
    {
        private readonly BookingApiClient _apiClient;
        private readonly long _businessId;
        private readonly long _appointmentId;
        private readonly long _serviceId;
        private readonly long? _staffMemberId;
        private bool _isSaving;

        public AppointmentChangeActionResponseDto? ResultDto { get; private set; }

        public ProposeAppointmentTimeWindow(
            BookingApiClient apiClient,
            long businessId,
            long appointmentId,
            long serviceId,
            long? staffMemberId,
            DateTime initialDate)
        {
            InitializeComponent();

            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _businessId = businessId;
            _appointmentId = appointmentId;
            _serviceId = serviceId;
            _staffMemberId = staffMemberId;

            DatePickerAppointmentDate.SelectedDate = initialDate.Date;
            Loaded += ProposeAppointmentTimeWindow_Loaded;
        }

        private async void ProposeAppointmentTimeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadAvailableSlotsAsync();
            }
            catch (Exception ex)
            {
                SetStatus("Slobodni termini nisu učitani.");
                MessageBox.Show(
                    ex.Message,
                    "Učitavanje slobodnih termina nije uspelo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async void ComboBoxAvailableSlots_DropDownOpened(object sender, EventArgs e)
        {
            try
            {
                await LoadAvailableSlotsAsync();
            }
            catch (Exception ex)
            {
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

            if (!_staffMemberId.HasValue)
            {
                SetStatus("Termin nema izabranog radnika, pa nije moguće ponuditi novi slot.");
                return;
            }

            SetStatus("Učitavam slobodne termine...");

            var selectedDate = DatePickerAppointmentDate.SelectedDate.Value.Date;

            var slots = await _apiClient.GetAvailableSlotsAsync(
                _businessId,
                _serviceId,
                _staffMemberId.Value,
                null,
                selectedDate,
                cancellationToken);

            var orderedSlots = slots
                .OrderBy(x => x.StartAtUtc)
                .Select(x => new AvailableSlotOption
                {
                    StartAtUtc = EnsureUtc(x.StartAtUtc),
                    EndAtUtc = EnsureUtc(x.EndAtUtc),
                    DisplayText = $"{x.StartLabel} - {x.EndLabel}"
                })
                .ToList();

            ComboBoxAvailableSlots.ItemsSource = orderedSlots;
            ComboBoxAvailableSlots.SelectedIndex = orderedSlots.Count > 0 ? 0 : -1;

            if (orderedSlots.Count == 0)
                SetStatus("Nema slobodnih termina za izabrani datum.");
            else
                SetStatus("Izaberite novi termin i pošaljite predlog.");
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (_isSaving)
                return;

            try
            {
                if (ComboBoxAvailableSlots.SelectedItem is not AvailableSlotOption selectedSlot)
                    throw new InvalidOperationException("Izaberite slobodan termin.");

                var request = new ProposeAppointmentTimeRequestDto
                {
                    AppointmentId = _appointmentId,
                    ProposedStartAtUtc = selectedSlot.StartAtUtc,
                    FinalDurationMin = null,
                    Message = string.IsNullOrWhiteSpace(TextBoxMessage.Text) ? null : TextBoxMessage.Text.Trim()
                };

                SetBusy(true, "Šaljem predlog novog termina...");

                ResultDto = await _apiClient.ProposeAppointmentTimeAsync(request);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                SetStatus("Predlog novog termina nije sačuvan.");
                MessageBox.Show(
                    ex.Message,
                    "Predlog nije uspeo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
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
            DatePickerAppointmentDate.IsEnabled = !isBusy;
            ComboBoxAvailableSlots.IsEnabled = !isBusy;
            TextBoxMessage.IsEnabled = !isBusy;

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
    }
}