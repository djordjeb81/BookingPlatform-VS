using SmartBooking_Desk.Models.Appointments;
using SmartBooking_Desk.Services;
using System;
using System.Linq;
using System.Windows;

namespace SmartBooking_Desk
{
    public partial class ProposeDelayWindow : Window
    {
        private readonly BookingApiClient _apiClient;
        private readonly long _appointmentId;
        private bool _isSaving;

        public AppointmentChangeActionResponseDto? ResultDto { get; private set; }

        public ProposeDelayWindow(
            BookingApiClient apiClient,
            long appointmentId)
        {
            InitializeComponent();

            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _appointmentId = appointmentId;

            Loaded += ProposeDelayWindow_Loaded;
        }

        private void ProposeDelayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBoxDelayMinutes.ItemsSource = new[] { 5, 10, 15, 20, 30, 45, 60 };
            ComboBoxDelayMinutes.SelectedItem = 15;
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (_isSaving)
                return;

            try
            {
                if (ComboBoxDelayMinutes.SelectedItem is not int delayMinutes)
                    throw new InvalidOperationException("Izaberite broj minuta odlaganja.");

                var request = new ProposeDelayRequestDto
                {
                    AppointmentId = _appointmentId,
                    DelayMinutes = delayMinutes,
                    Message = string.IsNullOrWhiteSpace(TextBoxMessage.Text) ? null : TextBoxMessage.Text.Trim()
                };

                SetBusy(true, "Šaljem predlog odlaganja...");

                ResultDto = await _apiClient.ProposeDelayAsync(request);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                SetStatus("Predlog odlaganja nije sačuvan.");
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

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetBusy(bool isBusy, string? statusMessage = null)
        {
            _isSaving = isBusy;

            ButtonSave.IsEnabled = !isBusy;
            ButtonCancel.IsEnabled = !isBusy;
            ComboBoxDelayMinutes.IsEnabled = !isBusy;
            TextBoxMessage.IsEnabled = !isBusy;

            if (!string.IsNullOrWhiteSpace(statusMessage))
                SetStatus(statusMessage);
        }

        private void SetStatus(string message)
        {
            TextBlockStatus.Text = message;
        }
    }
}