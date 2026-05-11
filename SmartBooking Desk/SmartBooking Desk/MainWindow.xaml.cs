using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartBooking_Desk.Models.Appointments;
using SmartBooking_Desk.Models.Auth;
using SmartBooking_Desk.Services;
using Microsoft.VisualBasic;
using SmartBooking_Desk.Services.Licensing;

namespace SmartBooking_Desk
{
    public partial class MainWindow : Window
    {
        private readonly BookingApiClient _apiClient = new();
        private readonly LicenseGuardService _licenseGuardService = new();
        private readonly AuthResponseDto _auth;

        private readonly List<AppointmentRowItem> _allAppointments = new();
        private readonly List<InboxRowItem> _allInboxItems = new();
        private readonly List<AppointmentRowItem> _visibleAppointments = new();
        private readonly List<InboxRowItem> _visibleInboxItems = new();

        private long? _selectedBusinessId;
        private DateTime _selectedDate = DateTime.Today;
        private DateTime _sidebarStartDate = DateTime.Today;

        public MainWindow(AuthResponseDto auth)
        {
            _auth = auth;
            InitializeComponent();
            Loaded += MainWindow_Loaded;

            ClearDetails();
            UpdateActionButtonsState();

            if (!string.IsNullOrWhiteSpace(_auth.Token))
                _apiClient.SetBearerToken(_auth.Token);

            var activeMembership =
                _auth.Memberships?.FirstOrDefault(x => x.IsActive)
                ?? _auth.Memberships?.FirstOrDefault();

            _selectedBusinessId = activeMembership?.BusinessId;
            _sidebarStartDate = _selectedDate;
            SelectedDatePicker.SelectedDate = _selectedDate;
            UpdateDashboardCards();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                HeaderStatusTextBlock.Text = "Učitavanje rasporeda...";

                await EnsureLicensedAsync();
                await LoadAppointmentsInternalAsync();
                await LoadInboxInternalAsync();

                ApplySelectedDateFilter();
                BuildDaysSidebar();

                HeaderStatusTextBlock.Text = "Raspored je učitan.";
            }
            catch (Exception ex)
            {
                HeaderStatusTextBlock.Text = ex.Message;

                MessageBox.Show(
                    ex.Message,
                    "Greška pri otvaranju početnog pregleda",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async void LoadAppointmentsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await EnsureLicensedAsync();
                await LoadAppointmentsInternalAsync();
                ApplySelectedDateFilter();
                BuildDaysSidebar();
                HeaderStatusTextBlock.Text = "Termini su učitani.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Greška pri učitavanju termina",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void LoadInboxButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await EnsureLicensedAsync();
                await LoadInboxInternalAsync();
                ApplySelectedDateFilter();
                BuildDaysSidebar();
                HeaderStatusTextBlock.Text = "Obaveštenja su učitana.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Greška pri učitavanju obaveštenja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_auth.Token))
                {
                    MessageBox.Show(
                        "Korisnik nije prijavljen.",
                        "Nije moguće otvoriti podešavanja",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (_selectedBusinessId is null)
                {
                    MessageBox.Show(
                        "Korisnik nema dodeljen biznis.",
                        "Nije moguće otvoriti podešavanja",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var window = new SettingsWindow(_auth.Token, _selectedBusinessId.Value)
                {
                    Owner = this
                };

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Greška pri otvaranju podešavanja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OpenCreateAppointmentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_auth.Token))
                {
                    MessageBox.Show(
                        "Korisnik nije prijavljen.",
                        "Nije moguće otvoriti unos termina",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (_selectedBusinessId is null)
                {
                    MessageBox.Show(
                        "Korisnik nema dodeljen biznis.",
                        "Nije moguće otvoriti unos termina",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await EnsureLicensedAsync();

                var window = new CreateAppointmentWindow(_apiClient, _selectedBusinessId.Value)
                {
                    Owner = this
                };

                var result = window.ShowDialog();
                if (result == true)
                {
                    await RefreshAfterAppointmentCreatedAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Greška pri otvaranju prozora za novi termin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void OpenCustomersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_auth.Token))
                {
                    MessageBox.Show(
                        "Korisnik nije prijavljen.",
                        "Nije moguće otvoriti klijente",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (_selectedBusinessId is null)
                {
                    MessageBox.Show(
                        "Korisnik nema dodeljen biznis.",
                        "Nije moguće otvoriti klijente",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await EnsureLicensedAsync();

                var window = new BusinessCustomersWindow(_apiClient, _selectedBusinessId.Value)
                {
                    Owner = this
                };

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Greška pri otvaranju prozora za klijente",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SelectedDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            _selectedDate = (SelectedDatePicker.SelectedDate ?? DateTime.Today).Date;
            ApplySelectedDateFilter();
            BuildDaysSidebar();
        }

        private async System.Threading.Tasks.Task LoadAppointmentsInternalAsync()
        {
            var appointments = await _apiClient.GetAppointmentsAsync(_selectedBusinessId);

            _allAppointments.Clear();
            _allAppointments.AddRange(
                appointments
                    .Select(MapAppointmentRow)
                    .OrderBy(x => x.StartAtValue));
        }

        private async System.Threading.Tasks.Task LoadInboxInternalAsync()
        {
            var inboxItems = await _apiClient.GetInboxAsync(_selectedBusinessId);

            _allInboxItems.Clear();
            _allInboxItems.AddRange(
                inboxItems
                    .Select(MapInboxRow)
                    .OrderBy(x => x.StartAtValue));
        }

        private async System.Threading.Tasks.Task RefreshAfterAppointmentCreatedAsync()
        {
            HeaderStatusTextBlock.Text = "Osvežavam raspored...";

            await LoadAppointmentsInternalAsync();
            await LoadInboxInternalAsync();

            ApplySelectedDateFilter();
            BuildDaysSidebar();

            HeaderStatusTextBlock.Text = "Termin je uspešno sačuvan.";
        }

        private void ApplySelectedDateFilter()
        {
            _visibleAppointments.Clear();
            _visibleAppointments.AddRange(
                _allAppointments
                    .Where(x => x.StartAtValue.Date == _selectedDate.Date)
                    .OrderBy(x => x.StartAtValue));

            _visibleInboxItems.Clear();
            _visibleInboxItems.AddRange(
                _allInboxItems
                    .Where(x => x.StartAtValue.Date == _selectedDate.Date)
                    .OrderBy(x => x.StartAtValue));

            AppointmentsGrid.ItemsSource = null;
            AppointmentsGrid.ItemsSource = _visibleAppointments;

            InboxGrid.ItemsSource = null;
            InboxGrid.ItemsSource = _visibleInboxItems;

            if (_visibleAppointments.Count > 0)
            {
                AppointmentsGrid.SelectedItem = _visibleAppointments.First();
            }
            else
            {
                AppointmentsGrid.SelectedItem = null;
                InboxGrid.SelectedItem = null;
                ClearDetails();
                WorkflowInfoTextBlock.Text = "Za izabrani dan nema zakazanih termina.";
            }

            UpdateDashboardCards();
            UpdateActionButtonsState();
        }

        private void BuildDaysSidebar()
        {
            DaysSidebarPanel.Children.Clear();

            var startDate = _sidebarStartDate.Date;

            for (var i = 0; i < 8; i++)
            {
                var date = startDate.AddDays(i);
                var appointmentsForDay = _allAppointments
                    .Where(x => x.StartAtValue.Date == date.Date)
                    .OrderBy(x => x.StartAtValue)
                    .ToList();

                var followUpsForDay = _allInboxItems
                    .Count(x => x.StartAtValue.Date == date.Date && x.RequiresOwnerFollowUp);

                var isSelectedDay = date.Date == _selectedDate.Date;
                var isToday = date.Date == DateTime.Today;

                var cardBorder = new Border
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(10),
                    BorderThickness = isSelectedDay
                        ? new Thickness(2)
                        : isToday ? new Thickness(2) : new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    BorderBrush = isSelectedDay
                        ? new SolidColorBrush(Color.FromRgb(52, 96, 160))
                        : isToday
                            ? new SolidColorBrush(Color.FromRgb(76, 140, 90))
                            : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    Background = isSelectedDay
                        ? new SolidColorBrush(Color.FromRgb(229, 239, 252))
                        : isToday
                            ? new SolidColorBrush(Color.FromRgb(236, 248, 238))
                            : new SolidColorBrush(Color.FromRgb(250, 250, 250))
                };

                var cardButton = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Tag = date.Date
                };
                cardButton.Click += DayCardButton_Click;

                var stack = new StackPanel();

                stack.Children.Add(new TextBlock
                {
                    Text = date.ToString("ddd, dd.MM.yyyy", new CultureInfo("sr-Latn-RS")),
                    FontWeight = isSelectedDay ? FontWeights.Bold : FontWeights.SemiBold,
                    FontSize = isSelectedDay ? 15 : 14
                });

                if (isSelectedDay)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Margin = new Thickness(0, 3, 0, 2),
                        Text = "Izabrani dan",
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(52, 96, 160))
                    });
                }

                if (isToday)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Margin = new Thickness(0, isSelectedDay ? 0 : 3, 0, 4),
                        Text = "Danas",
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(76, 140, 90))
                    });
                }

                stack.Children.Add(new TextBlock
                {
                    Margin = new Thickness(0, 4, 0, 6),
                    Text = $"Termina: {appointmentsForDay.Count}    Reakcija: {followUpsForDay}",
                    Foreground = isSelectedDay
                        ? new SolidColorBrush(Color.FromRgb(45, 70, 110))
                        : isToday
                            ? new SolidColorBrush(Color.FromRgb(55, 95, 65))
                            : new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                    FontWeight = isSelectedDay || isToday ? FontWeights.SemiBold : FontWeights.Normal
                });

                if (appointmentsForDay.Count == 0)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = "Nema zakazanih",
                        Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120))
                    });
                }
                else
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = "Zakazali:",
                        Margin = new Thickness(0, 0, 0, 4),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90))
                    });
                    foreach (var item in appointmentsForDay.Take(4))
                    {
                        var clientName = string.IsNullOrWhiteSpace(item.CustomerName) ? "Nepoznat klijent" : item.CustomerName;

                        stack.Children.Add(new TextBlock
                        {
                            Text = $"{item.StartAtValue:HH:mm}  {clientName}",
                            Margin = new Thickness(0, 0, 0, 3),
                            FontWeight = isSelectedDay ? FontWeights.SemiBold : FontWeights.Normal,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        });
                    }

                    if (appointmentsForDay.Count > 4)
                    {
                        stack.Children.Add(new TextBlock
                        {
                            Margin = new Thickness(0, 2, 0, 0),
                            Text = $"+ još {appointmentsForDay.Count - 4}",
                            Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90))
                        });
                    }
                }

                cardButton.Content = stack;
                cardBorder.Child = cardButton;
                DaysSidebarPanel.Children.Add(cardBorder);
            }
        }

        private void DaysSidebarScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (e.Delta < 0)
            {
                _sidebarStartDate = _sidebarStartDate.AddDays(1);
            }
            else if (e.Delta > 0)
            {
                _sidebarStartDate = _sidebarStartDate.AddDays(-1);
            }

            BuildDaysSidebar();
            e.Handled = true;
        }

        private void DayCardButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not DateTime date)
                return;

            _selectedDate = date.Date;
            SelectedDatePicker.SelectedDate = _selectedDate;

            ApplySelectedDateFilter();
            BuildDaysSidebar();
        }

        private async System.Threading.Tasks.Task EnsureLicensedAsync()
        {
            if (string.IsNullOrWhiteSpace(_auth.Token))
                throw new InvalidOperationException("Korisnik nije prijavljen.");

            if (_selectedBusinessId is null)
                throw new InvalidOperationException("Korisnik nema dodeljen biznis.");

            var licenseResult = await _licenseGuardService.EnsureLicenseAsync(
                _auth.Email ?? "",
                _auth.Token);

            HeaderStatusTextBlock.Text = licenseResult.Message;

            if (!licenseResult.IsAllowed)
                throw new InvalidOperationException(licenseResult.Message);
        }

        private void AppointmentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AppointmentsGrid.SelectedItem is not AppointmentRowItem item)
                return;

            InboxGrid.SelectedItem = null;

            ShowDetails(
                item.Status,
                item.CustomerName,
                item.CustomerPhone,
                item.ServiceName,
                item.StaffDisplayName,
                item.ResourceName,
                $"{item.StartAt} - {item.EndAt}",
                item.CreatedAt,
                item.UpdatedAt,
                item.Note,
                item.OwnerWorkflowLabel,
                item.RequiresOwnerFollowUp,
                item.FollowUpHint);

            UpdateActionButtonsState();
        }

        private void InboxGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InboxGrid.SelectedItem is not InboxRowItem item)
                return;

            AppointmentsGrid.SelectedItem = null;

            ShowDetails(
                item.Status,
                item.CustomerName,
                item.CustomerPhone,
                item.ServiceName,
                item.StaffDisplayName,
                item.ResourceName,
                $"{item.StartAt} - {item.EndAt}",
                item.CreatedAt,
                item.UpdatedAt,
                item.Note,
                item.OwnerWorkflowLabel,
                item.RequiresOwnerFollowUp,
                item.FollowUpHint);

            UpdateActionButtonsState();
        }

        private async void AcceptActionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var inboxItem = GetSelectedInboxRow();
                if (inboxItem is not null)
                {
                    if (string.Equals(inboxItem.ChangeRequestType, "RescheduleRequest", StringComparison.OrdinalIgnoreCase) &&
                        inboxItem.ChangeRequestId.HasValue)
                    {
                        await _apiClient.AcceptRescheduleRequestAsync(new AcceptRescheduleRequestDto
                        {
                            AppointmentId = inboxItem.AppointmentId,
                            ChangeRequestId = inboxItem.ChangeRequestId.Value
                        });

                        await RefreshAfterWorkflowActionAsync("Zahtev za promenu termina je prihvaćen.");
                        return;
                    }

                    if (string.Equals(inboxItem.AppointmentStatus, "PendingApproval", StringComparison.OrdinalIgnoreCase))
                    {
                        await _apiClient.ApproveAppointmentAsync(new ApproveAppointmentRequestDto
                        {
                            AppointmentId = inboxItem.AppointmentId
                        });

                        await RefreshAfterWorkflowActionAsync("Termin je prihvaćen.");
                        return;
                    }
                }

                var appointmentItem = GetSelectedAppointmentRow();
                if (appointmentItem is not null)
                {
                    await _apiClient.ApproveAppointmentAsync(new ApproveAppointmentRequestDto
                    {
                        AppointmentId = appointmentItem.Id
                    });

                    await RefreshAfterWorkflowActionAsync("Termin je prihvaćen.");
                    return;
                }

                MessageBox.Show("Prvo izaberite termin ili stavku iz obaveštenja.", "Nema izbora");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RejectActionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reason = PromptForReason("Odbijanje", "Unesite razlog odbijanja:");

                var inboxItem = GetSelectedInboxRow();
                if (inboxItem is not null)
                {
                    if (string.Equals(inboxItem.ChangeRequestType, "RescheduleRequest", StringComparison.OrdinalIgnoreCase) &&
                        inboxItem.ChangeRequestId.HasValue)
                    {
                        await _apiClient.RejectRescheduleRequestAsync(new RejectRescheduleRequestDto
                        {
                            AppointmentId = inboxItem.AppointmentId,
                            ChangeRequestId = inboxItem.ChangeRequestId.Value,
                            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason
                        });

                        await RefreshAfterWorkflowActionAsync("Zahtev za promenu termina je odbijen.");
                        return;
                    }

                    if (string.Equals(inboxItem.AppointmentStatus, "PendingApproval", StringComparison.OrdinalIgnoreCase))
                    {
                        await _apiClient.RejectAppointmentAsync(new RejectAppointmentRequestDto
                        {
                            AppointmentId = inboxItem.AppointmentId,
                            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason
                        });

                        await RefreshAfterWorkflowActionAsync("Termin je odbijen.");
                        return;
                    }
                }

                var appointmentItem = GetSelectedAppointmentRow();
                if (appointmentItem is not null)
                {
                    await _apiClient.RejectAppointmentAsync(new RejectAppointmentRequestDto
                    {
                        AppointmentId = appointmentItem.Id,
                        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason
                    });

                    await RefreshAfterWorkflowActionAsync("Termin je odbijen.");
                    return;
                }

                MessageBox.Show("Prvo izaberite termin ili stavku iz obaveštenja.", "Nema izbora");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ProposeNewTimeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var appointmentItem = GetSelectedAppointmentRow();
                if (appointmentItem is null)
                {
                    MessageBox.Show("Za ovu radnju prvo izaberite potvrđen termin iz leve liste.", "Nema termina");
                    return;
                }

                var window = new ProposeAppointmentTimeWindow(
                    _apiClient,
                    _selectedBusinessId ?? 0,
                    appointmentItem.Id,
                    appointmentItem.ServiceId,
                    appointmentItem.PrimaryStaffMemberId,
                    appointmentItem.StartAtValue)
                {
                    Owner = this
                };

                var result = window.ShowDialog();
                if (result == true)
                {
                    await RefreshAfterWorkflowActionAsync("Predlog novog termina je sačuvan.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Greška pri predlogu novog termina",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void ProposeDelayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var appointmentItem = GetSelectedAppointmentRow();
                if (appointmentItem is null)
                {
                    MessageBox.Show("Za ovu radnju prvo izaberite potvrđen termin iz leve liste.", "Nema termina");
                    return;
                }

                var window = new ProposeDelayWindow(_apiClient, appointmentItem.Id)
                {
                    Owner = this
                };

                var result = window.ShowDialog();
                if (result == true)
                {
                    await RefreshAfterWorkflowActionAsync("Predlog odlaganja je sačuvan.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Greška pri predlogu odlaganja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void MarkCompletedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var appointmentItem = GetSelectedAppointmentRow();
                if (appointmentItem is null)
                {
                    MessageBox.Show("Za ovu radnju prvo izaberite termin iz leve liste.", "Nema termina");
                    return;
                }

                await _apiClient.CompleteAppointmentAsync(new UpdateConfirmedAppointmentStatusRequestDto
                {
                    AppointmentId = appointmentItem.Id
                });

                await RefreshAfterWorkflowActionAsync("Termin je označen kao završen.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MarkNoShowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var appointmentItem = GetSelectedAppointmentRow();
                if (appointmentItem is null)
                {
                    MessageBox.Show("Za ovu radnju prvo izaberite termin iz leve liste.", "Nema termina");
                    return;
                }

                await _apiClient.MarkNoShowAsync(new UpdateConfirmedAppointmentStatusRequestDto
                {
                    AppointmentId = appointmentItem.Id
                });

                await RefreshAfterWorkflowActionAsync("Termin je označen kao nedolazak klijenta.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CancelAppointmentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var appointmentItem = GetSelectedAppointmentRow();
                if (appointmentItem is null)
                {
                    MessageBox.Show("Za ovu radnju prvo izaberite termin iz leve liste.", "Nema termina");
                    return;
                }

                var note = PromptForReason("Otkazivanje termina", "Unesite razlog ili napomenu za otkazivanje:");

                await _apiClient.CancelAppointmentAsync(new UpdateConfirmedAppointmentStatusRequestDto
                {
                    AppointmentId = appointmentItem.Id,
                    Note = string.IsNullOrWhiteSpace(note) ? null : note
                });

                await RefreshAfterWorkflowActionAsync("Termin je otkazan.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowDetails(
            string status,
            string customerName,
            string customerPhone,
            string serviceName,
            string staffName,
            string resourceName,
            string timeRange,
            string createdAt,
            string updatedAt,
            string note,
            string ownerWorkflowLabel,
            bool requiresOwnerFollowUp,
            string followUpHint)
        {
            StatusValueTextBlock.Text = string.IsNullOrWhiteSpace(status) ? "-" : status;
            CustomerValueTextBlock.Text = string.IsNullOrWhiteSpace(customerName) ? "-" : customerName;
            PhoneValueTextBlock.Text = string.IsNullOrWhiteSpace(customerPhone) ? "-" : customerPhone;
            ServiceValueTextBlock.Text = string.IsNullOrWhiteSpace(serviceName) ? "-" : serviceName;
            StaffValueTextBlock.Text = string.IsNullOrWhiteSpace(staffName) ? "-" : staffName;
            ResourceValueTextBlock.Text = string.IsNullOrWhiteSpace(resourceName) ? "-" : resourceName;
            TimeValueTextBlock.Text = string.IsNullOrWhiteSpace(timeRange) ? "-" : timeRange;
            CreatedAtValueTextBlock.Text = string.IsNullOrWhiteSpace(createdAt) ? "-" : createdAt;
            UpdatedAtValueTextBlock.Text = string.IsNullOrWhiteSpace(updatedAt) ? "-" : updatedAt;
            NoteValueTextBlock.Text = string.IsNullOrWhiteSpace(note) ? "-" : note;

            WorkflowInfoTextBlock.Text =
                $"Sledeći korak: {(string.IsNullOrWhiteSpace(ownerWorkflowLabel) ? "-" : ownerWorkflowLabel)}\n" +
                $"Potrebna reakcija: {(requiresOwnerFollowUp ? "Da" : "Ne")}\n" +
                $"Napomena: {(string.IsNullOrWhiteSpace(followUpHint) ? "-" : followUpHint)}";
        }

        private void ClearDetails()
        {
            StatusValueTextBlock.Text = "-";
            CustomerValueTextBlock.Text = "-";
            PhoneValueTextBlock.Text = "-";
            ServiceValueTextBlock.Text = "-";
            StaffValueTextBlock.Text = "-";
            ResourceValueTextBlock.Text = "-";
            TimeValueTextBlock.Text = "-";
            CreatedAtValueTextBlock.Text = "-";
            UpdatedAtValueTextBlock.Text = "-";
            NoteValueTextBlock.Text = "-";
            WorkflowInfoTextBlock.Text = "Nije izabrana nijedna stavka.";

            UpdateActionButtonsState();
        }

        private void SetActionButtonsEnabled(
    bool accept,
    bool reject,
    bool proposeNewTime,
    bool proposeDelay,
    bool markCompleted,
    bool markNoShow,
    bool cancel)
        {
            AcceptActionButton.IsEnabled = accept;
            RejectActionButton.IsEnabled = reject;
            ProposeNewTimeButton.IsEnabled = proposeNewTime;
            ProposeDelayButton.IsEnabled = proposeDelay;
            MarkCompletedButton.IsEnabled = markCompleted;
            MarkNoShowButton.IsEnabled = markNoShow;
            CancelAppointmentButton.IsEnabled = cancel;
        }

        private void UpdateActionButtonsState()
        {
            var selectedAppointment = GetSelectedAppointmentRow();
            var selectedInbox = GetSelectedInboxRow();

            if (selectedAppointment is not null)
            {
                var isConfirmed = string.Equals(
                    selectedAppointment.Status,
                    "Potvrđen",
                    StringComparison.OrdinalIgnoreCase);

                SetActionButtonsEnabled(
                    accept: false,
                    reject: false,
                    proposeNewTime: isConfirmed,
                    proposeDelay: isConfirmed,
                    markCompleted: isConfirmed,
                    markNoShow: isConfirmed,
                    cancel: isConfirmed);

                return;
            }

            if (selectedInbox is not null)
            {
                var canAcceptReject =
                    (string.Equals(selectedInbox.ChangeRequestType, "RescheduleRequest", StringComparison.OrdinalIgnoreCase)
                        && selectedInbox.ChangeRequestId.HasValue)
                    ||
                    string.Equals(selectedInbox.AppointmentStatus, "PendingApproval", StringComparison.OrdinalIgnoreCase);

                SetActionButtonsEnabled(
                    accept: canAcceptReject,
                    reject: canAcceptReject,
                    proposeNewTime: false,
                    proposeDelay: false,
                    markCompleted: false,
                    markNoShow: false,
                    cancel: false);

                return;
            }

            SetActionButtonsEnabled(
                accept: false,
                reject: false,
                proposeNewTime: false,
                proposeDelay: false,
                markCompleted: false,
                markNoShow: false,
                cancel: false);
        }

        private AppointmentRowItem? GetSelectedAppointmentRow()
        {
            return AppointmentsGrid.SelectedItem as AppointmentRowItem;
        }

        private InboxRowItem? GetSelectedInboxRow()
        {
            return InboxGrid.SelectedItem as InboxRowItem;
        }

        private async System.Threading.Tasks.Task RefreshAfterWorkflowActionAsync(string successMessage)
        {
            await LoadAppointmentsInternalAsync();
            await LoadInboxInternalAsync();
            ApplySelectedDateFilter();
            BuildDaysSidebar();
            HeaderStatusTextBlock.Text = successMessage;
        }

        private static string PromptForReason(string title, string message)
        {
            return Microsoft.VisualBasic.Interaction.InputBox(message, title, "");
        }

        private void UpdateDashboardCards()
        {
            TodayTextBlock.Text = _selectedDate.ToString("dddd, dd.MM.yyyy", new CultureInfo("sr-Latn-RS"));

            var nextAppointment = _visibleAppointments
                .OrderBy(x => x.StartAtValue)
                .FirstOrDefault();

            NextAppointmentTextBlock.Text = nextAppointment is null
                ? "Nema termina"
                : nextAppointment.TimeSlot;

            TodayCountTextBlock.Text = _visibleAppointments.Count.ToString(CultureInfo.InvariantCulture);
            FollowUpCountTextBlock.Text = _visibleInboxItems.Count(x => x.RequiresOwnerFollowUp).ToString(CultureInfo.InvariantCulture);

            if (_visibleAppointments.Count == 0 && _visibleInboxItems.Count == 0)
            {
                WorkflowInfoTextBlock.Text = "Za izabrani dan nema zakazanih termina ni obaveštenja.";
            }
        }

        private static AppointmentRowItem MapAppointmentRow(AppointmentListItemDto dto)
        {
            var localStart = dto.StartAtUtc.ToLocalTime();
            var localEnd = dto.EndAtUtc.ToLocalTime();

            return new AppointmentRowItem
            {
                Id = dto.Id,
                ServiceId = dto.ServiceId,
                PrimaryStaffMemberId = dto.PrimaryStaffMemberId,
                Status = PrevediStatus(dto.Status),
                CustomerName = dto.CustomerName ?? "-",
                CustomerPhone = dto.CustomerPhone ?? "-",
                ServiceName = dto.ServiceName ?? $"Usluga #{dto.ServiceId}",
                StaffDisplayName = dto.StaffDisplayName ?? "-",
                ResourceName = dto.ResourceName ?? "-",
                StartAt = FormatDateTime(dto.StartAtUtc),
                EndAt = FormatDateTime(dto.EndAtUtc),
                CreatedAt = dto.CreatedAtUtc == default ? "-" : FormatDateTime(dto.CreatedAtUtc),
                UpdatedAt = dto.UpdatedAtUtc == default ? "-" : FormatDateTime(dto.UpdatedAtUtc),
                TimeSlot = $"{localStart:HH:mm} - {localEnd:HH:mm}",
                StartAtValue = localStart,
                Note = dto.Notes ?? "",
                OwnerWorkflowLabel = OdrediKorakZaTermin(dto.Status),
                RequiresOwnerFollowUp = ZahtevaReakciju(dto.Status),
                FollowUpHint = OdrediNapomenuZaTermin(dto.Status)
            };
        }

        private static InboxRowItem MapInboxRow(AppointmentInboxItemDto dto)
        {
            var localStart = dto.StartAtUtc.ToLocalTime();
            var localEnd = dto.EndAtUtc.ToLocalTime();

            var status = !string.IsNullOrWhiteSpace(dto.OwnerWorkflowLabel)
                ? dto.OwnerWorkflowLabel!
                : PrevediStatus(dto.AppointmentStatus);

            return new InboxRowItem
            {
                AppointmentId = dto.AppointmentId,
                ChangeRequestId = dto.ChangeRequestId,
                ChangeRequestType = dto.ChangeRequestType,
                AppointmentStatus = dto.AppointmentStatus,
                Status = status,
                CustomerName = dto.CustomerName ?? "-",
                CustomerPhone = dto.CustomerPhone ?? "-",
                ServiceName = dto.ServiceName ?? $"Usluga #{dto.ServiceId}",
                StaffDisplayName = dto.StaffDisplayName ?? "-",
                ResourceName = dto.ResourceName ?? "-",
                StartAt = FormatDateTime(dto.StartAtUtc),
                EndAt = FormatDateTime(dto.EndAtUtc),
                CreatedAt = "-",
                UpdatedAt = dto.LastOwnerActionAtUtc.HasValue ? FormatDateTime(dto.LastOwnerActionAtUtc.Value) : "-",
                TimeSlot = $"{localStart:HH:mm} - {localEnd:HH:mm}",
                StartAtValue = localStart,
                Note = dto.Message ?? "",
                OwnerWorkflowLabel = dto.OwnerWorkflowLabel ?? "Pregledaj stavku",
                RequiresOwnerFollowUp = dto.RequiresOwnerFollowUp,
                FollowUpHint = dto.FollowUpHint ?? ""
            };
        }

        private static string FormatDateTime(DateTime value)
        {
            return value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        private static string PrevediStatus(string? status)
        {
            return status switch
            {
                "PendingApproval" => "Čeka odobrenje",
                "Confirmed" => "Potvrđen",
                "Rejected" => "Odbijen",
                "Cancelled" => "Otkazan",
                "Completed" => "Završen",
                "NoShow" => "Klijent se nije pojavio",
                "RescheduleProposed" => "Predložen novi termin",
                "DelayProposed" => "Predloženo odlaganje",
                null or "" => "-",
                _ => status
            };
        }

        private static bool ZahtevaReakciju(string? status)
        {
            return status is "PendingApproval" or "RescheduleProposed" or "DelayProposed";
        }

        private static string OdrediKorakZaTermin(string? status)
        {
            return status switch
            {
                "PendingApproval" => "Pregledaj zahtev i odluči",
                "RescheduleProposed" => "Pregledaj predlog novog termina",
                "DelayProposed" => "Pregledaj predlog odlaganja",
                "Confirmed" => "Termin je potvrđen",
                "Completed" => "Termin je završen",
                "Cancelled" => "Termin je otkazan",
                "Rejected" => "Zahtev je odbijen",
                "NoShow" => "Klijent se nije pojavio",
                _ => "Pregledaj detalje termina"
            };
        }

        private static string OdrediNapomenuZaTermin(string? status)
        {
            return status switch
            {
                "PendingApproval" => "Potrebno je da prihvatiš ili odbiješ zahtev.",
                "RescheduleProposed" => "Klijent ili osoblje su predložili novi termin.",
                "DelayProposed" => "Predloženo je pomeranje termina za kasnije.",
                "Confirmed" => "Nema dodatnih obaveznih radnji.",
                "Completed" => "Termin je zatvoren kao završen.",
                "Cancelled" => "Termin je zatvoren kao otkazan.",
                "Rejected" => "Zahtev je odbijen i nema daljih radnji.",
                "NoShow" => "Evidentirano je da klijent nije došao.",
                _ => ""
            };
        }



        private sealed class AppointmentRowItem
        {
            public long Id { get; set; }
            public string TimeSlot { get; set; } = "";
            public DateTime StartAtValue { get; set; }
            public string Status { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public string CustomerPhone { get; set; } = "";
            public string ServiceName { get; set; } = "";
            public string StaffDisplayName { get; set; } = "";
            public string ResourceName { get; set; } = "";
            public string StartAt { get; set; } = "";
            public string EndAt { get; set; } = "";
            public string CreatedAt { get; set; } = "";
            public string UpdatedAt { get; set; } = "";
            public string Note { get; set; } = "";
            public string OwnerWorkflowLabel { get; set; } = "";
            public bool RequiresOwnerFollowUp { get; set; }
            public string FollowUpHint { get; set; } = "";

            public long ServiceId { get; set; }
            public long? PrimaryStaffMemberId { get; set; }
        }

        private sealed class InboxRowItem
        {
            public long AppointmentId { get; set; }
            public long? ChangeRequestId { get; set; }
            public string? ChangeRequestType { get; set; }
            public string? AppointmentStatus { get; set; }
            public string TimeSlot { get; set; } = "";
            public DateTime StartAtValue { get; set; }
            public string Status { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public string CustomerPhone { get; set; } = "";
            public string ServiceName { get; set; } = "";
            public string StaffDisplayName { get; set; } = "";
            public string ResourceName { get; set; } = "";
            public string StartAt { get; set; } = "";
            public string EndAt { get; set; } = "";
            public string CreatedAt { get; set; } = "";
            public string UpdatedAt { get; set; } = "";
            public string Note { get; set; } = "";
            public string OwnerWorkflowLabel { get; set; } = "";
            public bool RequiresOwnerFollowUp { get; set; }
            public string FollowUpHint { get; set; } = "";
        }
    }
}