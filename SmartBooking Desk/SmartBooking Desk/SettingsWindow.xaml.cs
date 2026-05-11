using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SmartBooking_Desk.Models.Businesses;
using SmartBooking_Desk.Models.Resources;
using SmartBooking_Desk.Models.Scheduling;
using SmartBooking_Desk.Models.Services;
using SmartBooking_Desk.Models.Staff;
using SmartBooking_Desk.Services;

namespace SmartBooking_Desk
{
    public partial class SettingsWindow : Window
    {
        private readonly BookingApiClient _apiClient = new();
        private readonly long _businessId;


        private double _timelineStaffColumnWidth = 180;
        private double _timelineResourceColumnWidth = 180;
        private double _timelineMinuteWidth = 26;

        private readonly List<ServiceItemDto> _services = new();
        private readonly List<StaffItemDto> _staff = new();
        private readonly List<ResourceItemDto> _resources = new();
        private readonly List<ServiceResourceUsageDto> _serviceResourceUsages = new();
        private readonly List<WorkingHourEditorRow> _workingHours = new();
        private readonly List<TimelineEditorRow> _timelineRows = new();
        private readonly List<StaffServiceEditorRow> _staffServiceRows = new();
        private readonly Dictionary<long, HashSet<long>> _staffToServiceIds = new();
        private readonly List<StaffResourceEditorRow> _staffResourceRows = new();
        private readonly Dictionary<long, HashSet<long>> _staffToResourceIds = new();
        private readonly List<FixedScheduleEditorRow> _fixedScheduleRows = new();
        private readonly List<ShiftScheduleEditorRow> _shiftScheduleRows = new();
        private readonly List<SplitScheduleEditorRow> _splitScheduleRows = new();
        private readonly List<AbsenceCalendarCell> _absenceCalendarCells = new();
        private readonly HashSet<DateTime> _selectedAbsenceDates = new();
        private DateTime _absenceMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private readonly List<TimeOffBlockDto> _staffAbsenceBlocks = new();
        private readonly List<ShiftOverrideCalendarCell> _shiftOverrideCalendarCells = new();
        private readonly Dictionary<DateTime, int> _baseShiftByDate = new();
        private readonly Dictionary<DateTime, int> _selectedShiftOverrideDates = new();
        private readonly Dictionary<DateTime, long> _existingShiftOverrideIds = new();
        private readonly List<StaffScheduleOverrideDto> _shiftOverrideItems = new();
        private int _lastSavedScheduleMode = 0;


        private ServiceItemDto? _selectedService;
        private StaffItemDto? _selectedStaff;
        private ResourceItemDto? _selectedResource;
        private BusinessDetailsDto? _business;
        private int _selectedTimelineRowIndex = -1;
        private bool _isTimelineDragActive;
        private bool _timelineDragPaintValue;

        public SettingsWindow(string jwtToken, long businessId)
        {
            InitializeComponent();
            StaffScheduleModeComboBox.SelectedIndex = 0;
            UpdateScheduleModePanels();
            InitializeDefaultScheduleRows();
            RenderAbsenceCalendar();
            DataContext = this;

            _businessId = businessId;
            _apiClient.SetBearerToken(jwtToken);

            Loaded += SettingsWindow_Loaded;
            PreviewMouseLeftButtonUp += SettingsWindow_PreviewMouseLeftButtonUp;
        }

        private void InitializeDefaultScheduleRows()
        {
            _fixedScheduleRows.Clear();
            _shiftScheduleRows.Clear();
            _splitScheduleRows.Clear();

            for (var day = 1; day <= 7; day++)
            {
                _fixedScheduleRows.Add(new FixedScheduleEditorRow
                {
                    DayOfWeek = day,
                    DayName = GetDayName(day),
                    IsWorkingDay = day <= 6,
                    StartTime = "08:00",
                    EndTime = day == 6 ? "13:00" : "17:00"
                });

                _shiftScheduleRows.Add(new ShiftScheduleEditorRow
                {
                    DayOfWeek = day,
                    DayName = GetDayName(day),
                    IsWorkingDay = day <= 6,
                    BaseShift = 1,
                    RotateWeekly = day <= 5
                });

                _splitScheduleRows.Add(new SplitScheduleEditorRow
                {
                    DayOfWeek = day,
                    DayName = GetDayName(day),
                    IsWorkingDay = day <= 6,
                    StartTime1 = "08:00",
                    EndTime1 = "13:00",
                    StartTime2 = day == 6 ? "" : "14:00",
                    EndTime2 = day == 6 ? "" : "17:00"
                });
            }

            FixedScheduleGrid.ItemsSource = null;
            FixedScheduleGrid.ItemsSource = _fixedScheduleRows;

            ShiftScheduleGrid.ItemsSource = null;
            ShiftScheduleGrid.ItemsSource = _shiftScheduleRows;

            SplitScheduleGrid.ItemsSource = null;
            SplitScheduleGrid.ItemsSource = _splitScheduleRows;
        }


        private void TimelineScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;

            var step = e.Delta > 0 ? 2.0 : -2.0;

            _timelineMinuteWidth = Math.Max(12, Math.Min(60, _timelineMinuteWidth + step));
            _timelineStaffColumnWidth = Math.Max(120, Math.Min(260, _timelineStaffColumnWidth + step * 2));
            _timelineResourceColumnWidth = Math.Max(120, Math.Min(260, _timelineResourceColumnWidth + step * 2));

            RenderTimelineEditor();
            e.Handled = true;
        }

        private async Task LoadStaffServiceAssignmentsMapAsync()
        {
            _staffToServiceIds.Clear();

            foreach (var staff in _staff)
            {
                try
                {
                    var items = await _apiClient.GetStaffServicesAsync(staff.Id);

                    _staffToServiceIds[staff.Id] = items
                        .Where(x => x.IsAssigned)
                        .Select(x => x.ServiceId)
                        .ToHashSet();
                }
                catch
                {
                    _staffToServiceIds[staff.Id] = new HashSet<long>();
                }
            }
        }

        private void SettingsWindow_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isTimelineDragActive = false;
        }

        private List<ResourceItemDto> GetAllowedResourcesForStaff(long? staffId)
        {
            var waitingResource = GetWaitingResource();

            if (!staffId.HasValue)
            {
                return _resources
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Name)
                    .ToList();
            }

            if (!_staffToResourceIds.TryGetValue(staffId.Value, out var resourceIds))
            {
                var resultWithoutAssignments = new List<ResourceItemDto>();

                if (waitingResource is not null)
                    resultWithoutAssignments.Add(waitingResource);

                return resultWithoutAssignments
                    .DistinctBy(x => x.Id)
                    .OrderBy(x => x.Name)
                    .ToList();
            }

            return _resources
                .Where(x =>
                    x.IsActive &&
                    (resourceIds.Contains(x.Id) || (waitingResource is not null && x.Id == waitingResource.Id)))
                .OrderBy(x => x.Name)
                .ToList();
        }



        private static string NormalizeResourceName(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private ResourceItemDto? GetWaitingResource()
        {
            return _resources
                .Where(x => x.IsActive && !x.CreatesOccupancy)
                .OrderBy(x => x.Name)
                .FirstOrDefault(x =>
                {
                    var name = NormalizeResourceName(x.Name);
                    return name == "cekanje" || name == "čekanje";
                });
        }

        private bool IsWaitingResource(long? resourceId)
        {
            if (!resourceId.HasValue)
                return false;

            var resource = _resources.FirstOrDefault(x => x.Id == resourceId.Value);
            if (resource is null)
                return false;

            var name = NormalizeResourceName(resource.Name);
            return !resource.CreatesOccupancy &&
                   (name == "cekanje" || name == "čekanje");
        }

        private bool ResourceRequiresStaff(long? resourceId)
        {
            return !IsWaitingResource(resourceId);
        }

        private long? GetDefaultResourceIdForNewTimelineRow()
        {
            var waitingResource = GetWaitingResource();
            if (waitingResource is not null)
                return waitingResource.Id;

            var defaultStaffId = GetDefaultStaffIdForSelectedService();
            return GetAllowedResourcesForStaff(defaultStaffId)
                .OrderBy(x => x.Name)
                .Select(x => (long?)x.Id)
                .FirstOrDefault();
        }

        private long? GetDefaultStaffIdForResource(long? resourceId)
        {
            if (!ResourceRequiresStaff(resourceId))
                return null;

            return GetDefaultStaffIdForSelectedService();
        }

        private async Task EnsureWaitingResourceExistsAsync()
        {
            var waitingResource = _resources.FirstOrDefault(x =>
            {
                var name = NormalizeResourceName(x.Name);
                return name == "cekanje" || name == "čekanje";
            });

            if (waitingResource is not null)
            {
                if (waitingResource.IsActive && !waitingResource.CreatesOccupancy)
                    return;

                await _apiClient.UpdateResourceAsync(
                    waitingResource.Id,
                    new UpdateResourceRequestDto
                    {
                        Name = waitingResource.Name ?? "Čekanje",
                        Description = waitingResource.Description,
                        CreatesOccupancy = false,
                        IsActive = true
                    });

                return;
            }

            await _apiClient.CreateResourceAsync(new CreateResourceRequestDto
            {
                BusinessId = _businessId,
                Name = "Čekanje",
                Description = "Sistemski resurs za čekanje koji ne pravi zauzeće.",
                CreatesOccupancy = false
            });
        }

        private int GetSelectedStaffScheduleMode()
        {
            if (StaffScheduleModeComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag &&
                int.TryParse(tag, out var value))
            {
                return value;
            }

            return 0;
        }

        private void UpdateScheduleModePanels()
        {
            var mode = GetSelectedStaffScheduleMode();

            FixedSchedulePanel.Visibility = mode == 0 ? Visibility.Visible : Visibility.Collapsed;
            ShiftSchedulePanel.Visibility = mode == 1 ? Visibility.Visible : Visibility.Collapsed;
            SplitSchedulePanel.Visibility = mode == 2 ? Visibility.Visible : Visibility.Collapsed;

            UpdateShiftOverrideTabState();
        }

        private void UpdateShiftOverrideTabState()
        {
            var isShiftMode = GetSelectedStaffScheduleMode() == 1;

            ShiftOverrideTabItem.IsEnabled = isShiftMode;
            ShiftOverrideInfoTextBlock.Text = isShiftMode
                ? "Klik na datum preokreće smenu za taj dan."
                : "Promena smene je dostupna samo za radnike koji rade po smenama.";

            if (!isShiftMode && StaffCalendarTabControl.SelectedItem == ShiftOverrideTabItem)
                StaffCalendarTabControl.SelectedIndex = 0;
        }


        private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadResourcesAsync();
            await EnsureWaitingResourceExistsAsync();
            await LoadResourcesAsync();

            await LoadServicesAsync();
            await LoadStaffAsync();
            await LoadStaffServiceAssignmentsMapAsync();
            await LoadStaffResourceAssignmentsMapAsync();
            await LoadWorkingHoursAsync();
            await LoadBusinessAsync();

            ShowServicesSection();

            if (ServicesGrid.Items.Count > 0)
                ServicesGrid.SelectedIndex = 0;
            else
                RenderTimelineEditor();
        }

        private void ApplyTimelineMinute(TimelineEditorRow row, int rowIndex, int minute, bool isActive)
        {
            if (isActive)
            {
                if (CanActivateMinute(row, rowIndex, minute))
                    row.ActiveMinutes.Add(minute);
                else
                    StatusTextBlock.Text = "Isti resurs ne sme biti zauzet u isto vreme u dva reda, a svaki red sme imati samo jedan neprekinuti segment.";
            }
            else
            {
                row.ActiveMinutes.Remove(minute);
            }
        }

        private List<StaffItemDto> GetAllowedStaffForSelectedService()
        {
            if (_selectedService is null)
            {
                return _staff
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.DisplayName)
                    .ToList();
            }

            return _staff
                .Where(x =>
                    x.IsActive &&
                    _staffToServiceIds.TryGetValue(x.Id, out var serviceIds) &&
                    serviceIds.Contains(_selectedService.Id))
                .OrderBy(x => x.DisplayName)
                .ToList();
        }

        private void StaffScheduleModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            _lastSavedScheduleMode = GetSelectedStaffScheduleMode();
            UpdateScheduleModePanels();
        }

        private void StaffCalendarTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            RenderAbsenceCalendar();
            RenderShiftOverrideCalendar();
        }

        private long? GetDefaultStaffIdForSelectedService()
        {
            return GetAllowedStaffForSelectedService()
                .OrderBy(x => x.DisplayName)
                .Select(x => (long?)x.Id)
                .FirstOrDefault();
        }

        private bool HasOverlapWithSameResource(TimelineEditorRow row, int rowIndex, int minute)
        {
            if (!row.ResourceId.HasValue)
                return false;

            for (var i = 0; i < _timelineRows.Count; i++)
            {
                if (i == rowIndex)
                    continue;

                var other = _timelineRows[i];

                if (other.ResourceId != row.ResourceId)
                    continue;

                if (other.ActiveMinutes.Contains(minute))
                    return true;
            }

            return false;
        }

        private bool CanActivateMinute(TimelineEditorRow row, int rowIndex, int minute)
        {
            if (HasOverlapWithSameResource(row, rowIndex, minute))
                return false;

            var trial = row.ActiveMinutes.ToHashSet();
            trial.Add(minute);

            return HasSingleContinuousSegment(trial);
        }

        private static bool HasSingleContinuousSegment(IEnumerable<int> minutes)
        {
            var ordered = minutes
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (ordered.Count <= 1)
                return true;

            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i] != ordered[i - 1] + 1)
                    return false;
            }

            return true;
        }



        private void SectionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SectionsListBox.SelectedItem is not ListBoxItem item || item.Tag is not string tag)
                return;

            ServicesSection.Visibility = Visibility.Collapsed;
            StaffSection.Visibility = Visibility.Collapsed;
            ResourcesSection.Visibility = Visibility.Collapsed;
            WorkingHoursSection.Visibility = Visibility.Collapsed;
            BusinessSection.Visibility = Visibility.Collapsed;
            PlaceholderSection.Visibility = Visibility.Collapsed;

            switch (tag)
            {
                case "Services":
                    ShowServicesSection();
                    break;

                case "Staff":
                    ShowStaffSection();
                    break;

                case "Resources":
                    ShowResourcesSection();
                    break;

                case "WorkingHours":
                    ShowWorkingHoursSection();
                    break;

                case "Business":
                    ShowBusinessSection();
                    break;

                default:
                    PlaceholderSection.Visibility = Visibility.Visible;
                    PlaceholderTextBlock.Text = "Ova sekcija će biti dodata uskoro.";
                    break;
            }
        }

        private void ShowServicesSection()
        {
            ServicesSection.Visibility = Visibility.Visible;
            PlaceholderSection.Visibility = Visibility.Collapsed;
        }

        private async void ShowStaffSection()
        {
            StaffSection.Visibility = Visibility.Visible;
            PlaceholderSection.Visibility = Visibility.Collapsed;

            await ReloadSelectedStaffDetailsAsync();
        }

        private async Task ReloadSelectedStaffDetailsAsync()
        {
            if (_selectedStaff is null)
                return;

            await LoadStaffServicesAsync(_selectedStaff.Id);
            await LoadStaffResourcesAsync(_selectedStaff.Id);
            await LoadStaffScheduleAsync(_selectedStaff.Id, _selectedStaff.ScheduleMode);
            await LoadStaffAbsencesAsync(_selectedStaff.Id);
            await LoadShiftOverridesAsync(_selectedStaff.Id);
        }

        private void ShowResourcesSection()
        {
            ResourcesSection.Visibility = Visibility.Visible;
            PlaceholderSection.Visibility = Visibility.Collapsed;
        }

        private void ShowWorkingHoursSection()
        {
            WorkingHoursSection.Visibility = Visibility.Visible;
            PlaceholderSection.Visibility = Visibility.Collapsed;
        }

        private void ShowBusinessSection()
        {
            BusinessSection.Visibility = Visibility.Visible;
            PlaceholderSection.Visibility = Visibility.Collapsed;
        }

        private async void RefreshResourcesButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadResourcesAsync();
            RenderTimelineEditor();
        }

        private void NewResourceButton_Click(object sender, RoutedEventArgs e)
        {
            PrepareNewResourceForm();
        }

        private void ResourcesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResourcesGrid.SelectedItem is not ResourceItemDto item)
                return;

            _selectedResource = item;

            ResourceNameTextBox.Text = item.Name ?? "";
            ResourceDescriptionTextBox.Text = item.Description ?? "";
            ResourceCreatesOccupancyCheckBox.IsChecked = item.CreatesOccupancy;
            ResourceIsActiveCheckBox.IsChecked = item.IsActive;

            StatusTextBlock.Text = $"Izabran resurs: {item.Name}.";
        }

        private async void SaveNewResourceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Čuvanje novog resursa je u toku...";

                var request = BuildCreateResourceRequestFromForm();
                await _apiClient.CreateResourceAsync(request);

                await LoadResourcesAsync();
                PrepareNewResourceForm();
                RenderTimelineEditor();

                StatusTextBlock.Text = "Novi resurs je sačuvan.";
            }

            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void SaveResourceChangesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedResource is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite resurs iz liste.";
                    return;
                }

                StatusTextBlock.Text = "Čuvanje izmena resursa je u toku...";

                var request = BuildUpdateResourceRequestFromForm();
                var updated = await _apiClient.UpdateResourceAsync(_selectedResource.Id, request);

                await LoadResourcesAsync();
                SelectResourceById(updated.Id);
                RenderTimelineEditor();

                StatusTextBlock.Text = "Izmene resursa su sačuvane.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void ActivateResourceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedResource is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite resurs iz liste.";
                    return;
                }

                await _apiClient.ActivateResourceAsync(_selectedResource.Id);
                await LoadResourcesAsync();
                SelectResourceById(_selectedResource.Id);
                RenderTimelineEditor();

                StatusTextBlock.Text = "Resurs je aktiviran.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void DeactivateResourceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedResource is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite resurs iz liste.";
                    return;
                }

                await _apiClient.DeactivateResourceAsync(_selectedResource.Id);
                await LoadResourcesAsync();
                SelectResourceById(_selectedResource.Id);
                RenderTimelineEditor();

                StatusTextBlock.Text = "Resurs je deaktiviran.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void DeleteResourceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedResource is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite resurs iz liste.";
                    return;
                }

                var result = MessageBox.Show(
                    $"Da li ste sigurni da želite da obrišete resurs \"{_selectedResource.Name}\"?",
                    "Potvrda brisanja",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                await _apiClient.DeleteResourceAsync(_selectedResource.Id);
                await LoadResourcesAsync();
                PrepareNewResourceForm();
                RenderTimelineEditor();

                StatusTextBlock.Text = "Resurs je obrisan.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async Task LoadResourcesAsync()
        {
            try
            {
                var items = await _apiClient.GetResourcesAsync(_businessId);

                _resources.Clear();
                _resources.AddRange(items.OrderBy(x => x.Name));

                ResourcesGrid.ItemsSource = null;
                ResourcesGrid.ItemsSource = _resources;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);

                MessageBox.Show(ex.Message, "Greška pri učitavanju resursa");
            }
        }

        private void PrepareNewResourceForm()
        {
            _selectedResource = null;
            ResourcesGrid.SelectedItem = null;

            ResourceNameTextBox.Text = "";
            ResourceDescriptionTextBox.Text = "";
            ResourceCreatesOccupancyCheckBox.IsChecked = true;
            ResourceIsActiveCheckBox.IsChecked = true;
        }

        private CreateResourceRequestDto BuildCreateResourceRequestFromForm()
        {
            var name = ResourceNameTextBox.Text.Trim();
            var description = ResourceDescriptionTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Unesite naziv resursa.");

            return new CreateResourceRequestDto
            {
                BusinessId = _businessId,
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                CreatesOccupancy = ResourceCreatesOccupancyCheckBox.IsChecked == true
            };
        }

        private UpdateResourceRequestDto BuildUpdateResourceRequestFromForm()
        {
            var name = ResourceNameTextBox.Text.Trim();
            var description = ResourceDescriptionTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Unesite naziv resursa.");

            return new UpdateResourceRequestDto
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                CreatesOccupancy = ResourceCreatesOccupancyCheckBox.IsChecked == true,
                IsActive = ResourceIsActiveCheckBox.IsChecked == true
            };
        }

        private void SelectResourceById(long id)
        {
            var item = _resources.FirstOrDefault(x => x.Id == id);
            if (item is not null)
            {
                ResourcesGrid.SelectedItem = item;
                ResourcesGrid.ScrollIntoView(item);
            }
        }

        private async void RefreshServicesButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadServicesAsync();
        }

        private void NewServiceButton_Click(object sender, RoutedEventArgs e)
        {
            PrepareNewServiceForm();
        }

        private async void ServicesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServicesGrid.SelectedItem is not ServiceItemDto item)
                return;

            _selectedService = item;

            ServiceNameTextBox.Text = item.Name ?? "";
            ServiceDescriptionTextBox.Text = item.Description ?? "";
            ServicePriceTextBox.Text = item.BasePrice?.ToString(CultureInfo.InvariantCulture) ?? "";
            ServiceDurationTextBox.Text = item.EstimatedDurationMin.ToString(CultureInfo.InvariantCulture);
            ServiceIsActiveCheckBox.IsChecked = item.IsActive;

            StatusTextBlock.Text = $"Izabrana usluga: {item.Name}.";

            await LoadServiceResourceUsagesAsync();
        }

        private async void SaveNewServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Čuvanje nove usluge je u toku...";

                var request = BuildCreateRequestFromForm();
                await _apiClient.CreateServiceAsync(request);

                await LoadServicesAsync();
                PrepareNewServiceForm();

                StatusTextBlock.Text = "Nova usluga je sačuvana.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void SaveServiceChangesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedService is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite uslugu iz liste.";
                    return;
                }

                StatusTextBlock.Text = "Čuvanje izmena je u toku...";

                var request = BuildUpdateRequestFromForm();
                var updated = await _apiClient.UpdateServiceAsync(_selectedService.Id, request);

                await LoadServicesAsync();
                SelectServiceById(updated.Id);

                StatusTextBlock.Text = "Izmene su sačuvane.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void ActivateServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedService is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite uslugu iz liste.";
                    return;
                }

                await _apiClient.ActivateServiceAsync(_selectedService.Id);
                await LoadServicesAsync();
                SelectServiceById(_selectedService.Id);

                StatusTextBlock.Text = "Usluga je aktivirana.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void DeactivateServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedService is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite uslugu iz liste.";
                    return;
                }

                await _apiClient.DeactivateServiceAsync(_selectedService.Id);
                await LoadServicesAsync();
                SelectServiceById(_selectedService.Id);

                StatusTextBlock.Text = "Usluga je deaktivirana.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void DeleteServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedService is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite uslugu iz liste.";
                    return;
                }

                var result = MessageBox.Show(
                    $"Da li ste sigurni da želite da obrišete uslugu \"{_selectedService.Name}\"?",
                    "Potvrda brisanja",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                await _apiClient.DeleteServiceAsync(_selectedService.Id);
                await LoadServicesAsync();
                PrepareNewServiceForm();
                RenderTimelineEditor();

                StatusTextBlock.Text = "Usluga je obrisana.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async Task LoadServiceResourceUsagesAsync()
        {
            _serviceResourceUsages.Clear();
            _timelineRows.Clear();
            _selectedTimelineRowIndex = -1;

            if (_selectedService is null)
            {
                RenderTimelineEditor();
                return;
            }

            var items = await _apiClient.GetServiceResourceUsagesAsync(_selectedService.Id);

            _serviceResourceUsages.AddRange(items.OrderBy(x => x.StartMinute).ThenBy(x => x.ResourceName));

            BuildTimelineRowsFromUsages();
            RenderTimelineEditor();

            var availableCount = _resources.Count(x => x.IsActive);
            StatusTextBlock.Text = $"Učitano zauzeća: {_serviceResourceUsages.Count}. Aktivnih resursa: {availableCount}.";
        }

        private void BuildTimelineRowsFromUsages()
        {
            _timelineRows.Clear();

            var totalMinutes = GetCurrentServiceDuration();

            foreach (var usage in _serviceResourceUsages.OrderBy(x => x.StartMinute).ThenBy(x => x.ResourceName))
            {
                var row = new TimelineEditorRow
                {
                    ExistingUsageId = usage.Id,
                    StaffId = usage.StaffId,
                    ResourceId = usage.ResourceId,
                    IsRequired = usage.IsRequired
                };

                var endMinute = Math.Min(totalMinutes, usage.StartMinute + usage.DurationMin);

                for (var minute = usage.StartMinute; minute < endMinute; minute++)
                    row.ActiveMinutes.Add(minute);

                _timelineRows.Add(row);
            }
        }

        private void RenderTimelineEditor()
        {
            TimelineHeaderGrid.Children.Clear();
            TimelineHeaderGrid.ColumnDefinitions.Clear();
            TimelineRowsPanel.Children.Clear();

            var totalMinutes = GetCurrentServiceDuration();

            if (totalMinutes <= 0)
                return;

            TimelineHeaderGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(_timelineStaffColumnWidth) });

            TimelineHeaderGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(_timelineResourceColumnWidth) });

            for (var minute = 0; minute < totalMinutes; minute++)
            {
                TimelineHeaderGrid.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(_timelineMinuteWidth) });
            }

            var staffHeader = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = Brushes.WhiteSmoke,
                Child = new TextBlock
                {
                    Text = "Radnik",
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 4, 8, 4)
                }
            };
            Grid.SetColumn(staffHeader, 0);
            TimelineHeaderGrid.Children.Add(staffHeader);

            var resourceHeader = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = Brushes.WhiteSmoke,
                Child = new TextBlock
                {
                    Text = "Resurs",
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 4, 8, 4)
                }
            };
            Grid.SetColumn(resourceHeader, 1);
            TimelineHeaderGrid.Children.Add(resourceHeader);

            for (var minute = 0; minute < totalMinutes; minute++)
            {
                var minuteBorder = new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Background = Brushes.WhiteSmoke,
                    Child = new TextBlock
                    {
                        Text = (minute + 1).ToString(CultureInfo.InvariantCulture),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 11,
                        Margin = new Thickness(0, 4, 0, 4)
                    }
                };

                Grid.SetColumn(minuteBorder, minute + 2);
                TimelineHeaderGrid.Children.Add(minuteBorder);
            }

            if (_timelineRows.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = "Nema redova. Kliknite 'Dodaj red'.",
                    Margin = new Thickness(4, 6, 4, 6),
                    Foreground = Brushes.Gray
                };
                TimelineRowsPanel.Children.Add(emptyText);
                return;
            }

            var activeStaff = GetAllowedStaffForSelectedService();


            for (var rowIndex = 0; rowIndex < _timelineRows.Count; rowIndex++)
            {
                var row = _timelineRows[rowIndex];
                var isSelectedRow = rowIndex == _selectedTimelineRowIndex;

                var rowBorder = new Border
                {
                    BorderBrush = isSelectedRow ? Brushes.DodgerBlue : Brushes.LightGray,
                    BorderThickness = new Thickness(isSelectedRow ? 2 : 1),
                    Background = isSelectedRow ? new SolidColorBrush(Color.FromRgb(240, 247, 255)) : Brushes.Transparent,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(2)
                };

                var rowGrid = new Grid();

                rowGrid.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(_timelineStaffColumnWidth) });

                rowGrid.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(_timelineResourceColumnWidth) });

                for (var minute = 0; minute < totalMinutes; minute++)
                {
                    rowGrid.ColumnDefinitions.Add(
                        new ColumnDefinition { Width = new GridLength(_timelineMinuteWidth) });
                }

                if (ResourceRequiresStaff(row.ResourceId))
                {
                    var staffCombo = new ComboBox
                    {
                        Height = 30,
                        Margin = new Thickness(4, 2, 4, 2),
                        DisplayMemberPath = "DisplayName",
                        SelectedValuePath = "Id",
                        ItemsSource = activeStaff,
                        SelectedValue = row.StaffId
                    };
                    staffCombo.PreviewMouseLeftButtonDown += (_, _) =>
                    {
                        _selectedTimelineRowIndex = rowIndex;
                    };
                    staffCombo.DropDownOpened += (_, _) =>
                    {
                        _selectedTimelineRowIndex = rowIndex;
                    };
                    staffCombo.SelectionChanged += (_, _) =>
                    {
                        if (staffCombo.SelectedValue is long selectedStaffId)
                        {
                            row.StaffId = selectedStaffId;
                            _selectedTimelineRowIndex = rowIndex;

                            if (row.ResourceId.HasValue &&
                                _staffToResourceIds.TryGetValue(selectedStaffId, out var allowedResourceIds) &&
                                !allowedResourceIds.Contains(row.ResourceId.Value))
                            {
                                row.ResourceId = null;
                            }

                            RenderTimelineEditor();
                        }
                    };

                    Grid.SetColumn(staffCombo, 0);
                    rowGrid.Children.Add(staffCombo);
                }
                else
                {
                    row.StaffId = null;

                    var waitingLabel = new Border
                    {
                        Margin = new Thickness(4, 2, 4, 2),
                        Padding = new Thickness(8, 6, 8, 6),
                        Background = Brushes.WhiteSmoke,
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Child = new TextBlock
                        {
                            Text = "Nije potreban radnik",
                            Foreground = Brushes.Gray,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };

                    Grid.SetColumn(waitingLabel, 0);
                    rowGrid.Children.Add(waitingLabel);
                }

                var activeResources = GetAllowedResourcesForStaff(row.StaffId);

                var resourceCombo = new ComboBox
                {
                    Height = 30,
                    Margin = new Thickness(4, 2, 4, 2),
                    DisplayMemberPath = "Name",
                    SelectedValuePath = "Id",
                    ItemsSource = activeResources,
                    SelectedValue = row.ResourceId
                };
                resourceCombo.PreviewMouseLeftButtonDown += (_, _) =>
                {
                    _selectedTimelineRowIndex = rowIndex;
                };
                resourceCombo.DropDownOpened += (_, _) =>
                {
                    _selectedTimelineRowIndex = rowIndex;
                };
                resourceCombo.SelectionChanged += (_, _) =>
                {
                    if (resourceCombo.SelectedValue is long selectedResourceId)
                    {
                        row.ResourceId = selectedResourceId;
                        _selectedTimelineRowIndex = rowIndex;

                        if (IsWaitingResource(selectedResourceId))
                        {
                            row.StaffId = null;
                        }
                        else if (!row.StaffId.HasValue || row.StaffId.Value <= 0)
                        {
                            row.StaffId = GetDefaultStaffIdForSelectedService();
                        }

                        RenderTimelineEditor();
                    }
                };

                Grid.SetColumn(resourceCombo, 1);
                rowGrid.Children.Add(resourceCombo);

                for (var minute = 0; minute < totalMinutes; minute++)
                {
                    var localMinute = minute;
                    var isActive = row.ActiveMinutes.Contains(localMinute);

                    var cell = new Border
                    {
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(0.5),
                        Background = isActive ? Brushes.LightSkyBlue : Brushes.White,
                        Margin = new Thickness(0),
                        Cursor = Cursors.Hand,
                        ToolTip = isActive
                            ? $"Minut {localMinute + 1}: zauzeto"
                            : $"Minut {localMinute + 1}: slobodno",
                        Tag = new TimelineCellTag
                        {
                            RowIndex = rowIndex,
                            Minute = localMinute
                        }
                    };

                    cell.MouseLeftButtonDown += TimelineCell_MouseLeftButtonDown;
                    cell.MouseEnter += TimelineCell_MouseEnter;

                    Grid.SetColumn(cell, localMinute + 2);
                    rowGrid.Children.Add(cell);
                }

                rowBorder.MouseLeftButtonDown += (_, _) =>
                {
                    _selectedTimelineRowIndex = rowIndex;
                    RenderTimelineEditor();
                };

                rowBorder.Child = rowGrid;
                TimelineRowsPanel.Children.Add(rowBorder);
            }
        }


    

        private void TimelineCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border cell || cell.Tag is not TimelineCellTag tag)
                return;

            if (tag.RowIndex < 0 || tag.RowIndex >= _timelineRows.Count)
                return;

            var row = _timelineRows[tag.RowIndex];
            _selectedTimelineRowIndex = tag.RowIndex;

            _timelineDragPaintValue = !row.ActiveMinutes.Contains(tag.Minute);
            _isTimelineDragActive = true;

            ApplyTimelineMinute(row, tag.RowIndex, tag.Minute, _timelineDragPaintValue);
            RenderTimelineEditor();

            e.Handled = true;
        }


        private void TimelineCell_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!_isTimelineDragActive)
                return;

            if (sender is not Border cell || cell.Tag is not TimelineCellTag tag)
                return;

            if (tag.RowIndex < 0 || tag.RowIndex >= _timelineRows.Count)
                return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _isTimelineDragActive = false;
                return;
            }

            var row = _timelineRows[tag.RowIndex];
            _selectedTimelineRowIndex = tag.RowIndex;

            ApplyTimelineMinute(row, tag.RowIndex, tag.Minute, _timelineDragPaintValue);
            RenderTimelineEditor();
        }

        private sealed class TimelineCellTag
        {
            public int RowIndex { get; set; }
            public int Minute { get; set; }
        }

        private async void AddTimelineRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedService is null)
            {
                StatusTextBlock.Text = "Prvo izaberite uslugu.";
                return;
            }

            var defaultResourceId = GetDefaultResourceIdForNewTimelineRow();
            var defaultStaffId = GetDefaultStaffIdForResource(defaultResourceId);

            _timelineRows.Add(new TimelineEditorRow
            {
                StaffId = defaultStaffId,
                ResourceId = defaultResourceId,
                IsRequired = true
            });

            _selectedTimelineRowIndex = _timelineRows.Count - 1;
            RenderTimelineEditor();

            StatusTextBlock.Text = "Dodat je novi red u timeline.";
            await Task.CompletedTask;
        }

        private async void RemoveTimelineRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTimelineRowIndex < 0 || _selectedTimelineRowIndex >= _timelineRows.Count)
            {
                StatusTextBlock.Text = "Prvo izaberite red koji želite da obrišete.";
                return;
            }

            _timelineRows.RemoveAt(_selectedTimelineRowIndex);

            if (_selectedTimelineRowIndex >= _timelineRows.Count)
                _selectedTimelineRowIndex = _timelineRows.Count - 1;

            RenderTimelineEditor();
            StatusTextBlock.Text = "Red je obrisan iz timeline-a.";
            await Task.CompletedTask;
        }

        private async void SaveTimelineButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedService is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite uslugu.";
                    return;
                }

                var totalMinutes = GetCurrentServiceDuration();
                var rowsToSave = BuildUsageRequestsFromTimeline(totalMinutes);

                StatusTextBlock.Text = "Čuvanje timeline-a je u toku...";

                foreach (var existing in _serviceResourceUsages)
                    await _apiClient.DeleteServiceResourceUsageAsync(existing.Id);

                foreach (var row in rowsToSave)
                {
                    await _apiClient.CreateServiceResourceUsageAsync(
                        new CreateServiceResourceUsageRequestDto
                        {
                            ServiceId = _selectedService.Id,
                            StaffId = row.StaffId,
                            ResourceId = row.ResourceId,
                            StartMinute = row.StartMinute,
                            DurationMin = row.DurationMin,
                            IsRequired = row.IsRequired
                        });
                }

                await LoadServiceResourceUsagesAsync();
                StatusTextBlock.Text = "Timeline je sačuvan.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private List<TimelineSaveRow> BuildUsageRequestsFromTimeline(int totalMinutes)
        {
            var result = new List<TimelineSaveRow>();

            for (var rowIndex = 0; rowIndex < _timelineRows.Count; rowIndex++)
            {
                var row = _timelineRows[rowIndex];
                var rowNumber = rowIndex + 1;

                if (!row.ResourceId.HasValue || row.ResourceId.Value <= 0)
                    throw new InvalidOperationException($"Red {rowNumber}: izaberite resurs.");

                if (ResourceRequiresStaff(row.ResourceId) &&
                    (!row.StaffId.HasValue || row.StaffId.Value <= 0))
                {
                    throw new InvalidOperationException($"Red {rowNumber}: izaberite radnika.");
                }

                if (row.ActiveMinutes.Count == 0)
                    throw new InvalidOperationException($"Red {rowNumber}: obojite bar jednu kocku.");

                var ordered = row.ActiveMinutes.OrderBy(x => x).ToList();

                if (ordered.Any(x => x < 0 || x >= totalMinutes))
                    throw new InvalidOperationException($"Red {rowNumber}: neki minuti su van opsega trajanja usluge.");

                for (var i = 1; i < ordered.Count; i++)
                {
                    if (ordered[i] != ordered[i - 1] + 1)
                        throw new InvalidOperationException($"Red {rowNumber}: zauzeće mora biti jedan neprekinuti segment. Za više odvojenih delova koristite novi red.");
                }

                var startMinute = ordered.First();
                var durationMin = ordered.Count;

                result.Add(new TimelineSaveRow
                {
                    StaffId = row.StaffId,
                    ResourceId = row.ResourceId.Value,
                    StartMinute = startMinute,
                    DurationMin = durationMin,
                    IsRequired = row.IsRequired
                });
            }

            return result;
        }

        private int GetCurrentServiceDuration()
        {
            if (int.TryParse(ServiceDurationTextBox.Text?.Trim(), out var fromTextBox) && fromTextBox > 0)
                return fromTextBox;

            if (_selectedService is not null && _selectedService.EstimatedDurationMin > 0)
                return _selectedService.EstimatedDurationMin;

            return 0;
        }

        private async Task LoadServicesAsync()
        {
            try
            {
                StatusTextBlock.Text = "Učitavanje usluga...";

                var items = await _apiClient.GetServicesAsync(_businessId);

                _services.Clear();
                _services.AddRange(items.OrderBy(x => x.Name));

                ServicesGrid.ItemsSource = null;
                ServicesGrid.ItemsSource = _services;

                StatusTextBlock.Text = _services.Count == 0
                    ? "Još nema unetih usluga."
                    : $"Učitano usluga: {_services.Count}.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void PrepareNewServiceForm()
        {
            _selectedService = null;
            ServicesGrid.SelectedItem = null;

            ServiceNameTextBox.Text = "";
            ServiceDescriptionTextBox.Text = "";
            ServicePriceTextBox.Text = "";
            ServiceDurationTextBox.Text = "30";
            ServiceIsActiveCheckBox.IsChecked = true;

            _serviceResourceUsages.Clear();
            _timelineRows.Clear();
            _selectedTimelineRowIndex = -1;
            RenderTimelineEditor();
        }

        private CreateServiceRequestDto BuildCreateRequestFromForm()
        {
            var name = ServiceNameTextBox.Text.Trim();
            var description = ServiceDescriptionTextBox.Text.Trim();
            var priceText = ServicePriceTextBox.Text.Trim();
            var durationText = ServiceDurationTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Unesite naziv usluge.");

            if (!int.TryParse(durationText, out var durationMin) || durationMin <= 0)
                throw new InvalidOperationException("Trajanje mora biti pozitivan broj minuta.");

            double? price = null;
            if (!string.IsNullOrWhiteSpace(priceText))
            {
                if (!double.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedPrice) &&
                    !double.TryParse(priceText, NumberStyles.Any, new CultureInfo("sr-Latn-RS"), out parsedPrice))
                {
                    throw new InvalidOperationException("Cena nije u ispravnom formatu.");
                }

                price = parsedPrice;
            }

            return new CreateServiceRequestDto
            {
                BusinessId = _businessId,
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                BasePrice = price,
                EstimatedDurationMin = durationMin,
                BookingStrategyType = 0
            };
        }

        private UpdateServiceRequestDto BuildUpdateRequestFromForm()
        {
            var name = ServiceNameTextBox.Text.Trim();
            var description = ServiceDescriptionTextBox.Text.Trim();
            var priceText = ServicePriceTextBox.Text.Trim();
            var durationText = ServiceDurationTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Unesite naziv usluge.");

            if (!int.TryParse(durationText, out var durationMin) || durationMin <= 0)
                throw new InvalidOperationException("Trajanje mora biti pozitivan broj minuta.");

            double? price = null;
            if (!string.IsNullOrWhiteSpace(priceText))
            {
                if (!double.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedPrice) &&
                    !double.TryParse(priceText, NumberStyles.Any, new CultureInfo("sr-Latn-RS"), out parsedPrice))
                {
                    throw new InvalidOperationException("Cena nije u ispravnom formatu.");
                }

                price = parsedPrice;
            }

            return new UpdateServiceRequestDto
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                BasePrice = price,
                EstimatedDurationMin = durationMin,
                BookingStrategyType = 0,
                IsActive = ServiceIsActiveCheckBox.IsChecked == true
            };
        }

        private void SelectServiceById(long id)
        {
            var item = _services.FirstOrDefault(x => x.Id == id);
            if (item is not null)
            {
                ServicesGrid.SelectedItem = item;
                ServicesGrid.ScrollIntoView(item);
            }
        }

        private async void RefreshStaffButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadStaffAsync();
        }

        private void NewStaffButton_Click(object sender, RoutedEventArgs e)
        {
            PrepareNewStaffForm();
        }

        private void StaffGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StaffGrid.SelectedItem is not StaffItemDto item)
                return;

            _selectedStaff = item;

            StaffDisplayNameTextBox.Text = item.DisplayName ?? "";
            StaffTitleTextBox.Text = item.Title ?? "";
            StaffIsBookableCheckBox.IsChecked = item.IsBookable;
            StaffIsActiveCheckBox.IsChecked = item.IsActive;

            StaffScheduleModeComboBox.SelectedIndex = item.ScheduleMode;
            _lastSavedScheduleMode = item.ScheduleMode;
            UpdateScheduleModePanels();

            StatusTextBlock.Text = $"Izabran radnik: {item.DisplayName}.";

            _ = LoadStaffServicesAsync(item.Id);
            _ = LoadStaffResourcesAsync(item.Id);
            _ = LoadStaffScheduleAsync(item.Id);
            _ = LoadStaffAbsencesAsync(item.Id);
            _ = LoadShiftOverridesAsync(item.Id);
        }

        private async void SaveAbsencesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedStaff is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite radnika.";
                    return;
                }

                StatusTextBlock.Text = "Čuvanje odsustava je u toku...";

                var existingDates = new HashSet<DateTime>();

                foreach (var block in _staffAbsenceBlocks)
                {
                    var localStart = block.StartAtUtc.ToLocalTime().Date;
                    var localEndExclusive = block.EndAtUtc.ToLocalTime().Date;

                    for (var date = localStart; date < localEndExclusive; date = date.AddDays(1))
                        existingDates.Add(date);
                }

                var datesToAdd = _selectedAbsenceDates.Except(existingDates).OrderBy(x => x).ToList();
                var datesToRemove = existingDates.Except(_selectedAbsenceDates).OrderBy(x => x).ToList();

                foreach (var date in datesToAdd)
                {
                    var startLocal = date.Date;
                    var endLocal = date.Date.AddDays(1);

                    await _apiClient.CreateTimeOffBlockAsync(new CreateTimeOffBlockRequestDto
                    {
                        BusinessId = _businessId,
                        StaffMemberId = _selectedStaff.Id,
                        StartAtUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime(),
                        EndAtUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime(),
                        BlockType = 0,
                        Reason = "Odsustvo"
                    });
                }

                foreach (var date in datesToRemove)
                {
                    var block = _staffAbsenceBlocks.FirstOrDefault(x =>
                        x.StartAtUtc.ToLocalTime().Date <= date &&
                        x.EndAtUtc.ToLocalTime().Date > date);

                    if (block is not null)
                        await _apiClient.DeleteTimeOffBlockAsync(block.Id);
                }

                await LoadStaffAbsencesAsync(_selectedStaff.Id);
                StatusTextBlock.Text = "Odsustva radnika su sačuvana.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async Task LoadStaffScheduleAsync(long staffId, int? forcedMode = null)
        {
            try
            {
                var items = await _apiClient.GetStaffScheduleRulesAsync(staffId);

                InitializeDefaultScheduleRows();

                var detectedMode = DetectScheduleModeFromRules(items);

                var mode = forcedMode
                    ?? (items.Count > 0 ? detectedMode : (_selectedStaff?.ScheduleMode ?? 0));

                StaffScheduleModeComboBox.SelectedIndex = mode;
                _lastSavedScheduleMode = mode;

                if (_selectedStaff is not null)
                    _selectedStaff.ScheduleMode = mode;

                UpdateScheduleModePanels();

                if (mode == 0)
                {
                    foreach (var row in _fixedScheduleRows)
                    {
                        var rule = items
                            .Where(x => x.DayOfWeek == row.DayOfWeek && x.IsActive)
                            .OrderBy(x => x.Id)
                            .FirstOrDefault();

                        if (rule is null)
                        {
                            row.IsWorkingDay = false;
                            row.StartTime = "08:00";
                            row.EndTime = "17:00";
                        }
                        else
                        {
                            row.IsWorkingDay = true;
                            row.StartTime = string.IsNullOrWhiteSpace(rule.StartTime) ? "08:00" : rule.StartTime!;
                            row.EndTime = string.IsNullOrWhiteSpace(rule.EndTime) ? "17:00" : rule.EndTime!;
                        }
                    }

                    FixedScheduleGrid.ItemsSource = null;
                    FixedScheduleGrid.ItemsSource = _fixedScheduleRows;
                }
                else if (mode == 1)
                {
                    var shift1 = items.FirstOrDefault(x => x.SegmentType == 1 && x.IsActive);
                    var shift2 = items.FirstOrDefault(x => x.SegmentType == 2 && x.IsActive);

                    if (shift1 is not null)
                    {
                        Shift1StartTextBox.Text = string.IsNullOrWhiteSpace(shift1.StartTime) ? "08:00" : shift1.StartTime!;
                        Shift1EndTextBox.Text = string.IsNullOrWhiteSpace(shift1.EndTime) ? "15:00" : shift1.EndTime!;
                    }

                    if (shift2 is not null)
                    {
                        Shift2StartTextBox.Text = string.IsNullOrWhiteSpace(shift2.StartTime) ? "14:00" : shift2.StartTime!;
                        Shift2EndTextBox.Text = string.IsNullOrWhiteSpace(shift2.EndTime) ? "17:00" : shift2.EndTime!;
                    }

                    foreach (var row in _shiftScheduleRows)
                    {
                        var dayRules = items
                            .Where(x => x.DayOfWeek == row.DayOfWeek && x.IsActive)
                            .ToList();

                        if (dayRules.Count == 0)
                        {
                            row.IsWorkingDay = false;
                            row.BaseShift = 1;
                            row.RotateWeekly = false;
                            continue;
                        }

                        row.IsWorkingDay = true;

                        var hasWeekRules = dayRules.Any(x => x.WeekType == 1 || x.WeekType == 2);
                        row.RotateWeekly = hasWeekRules;

                        if (row.RotateWeekly)
                        {
                            var weekARule = dayRules
                                .Where(x => x.WeekType == 1)
                                .OrderBy(x => x.Id)
                                .FirstOrDefault();

                            if (weekARule is not null && (weekARule.SegmentType == 1 || weekARule.SegmentType == 2))
                                row.BaseShift = weekARule.SegmentType;
                            else
                                row.BaseShift = 1;
                        }
                        else
                        {
                            var fixedRule = dayRules
                                .Where(x => x.WeekType == 0)
                                .OrderBy(x => x.Id)
                                .FirstOrDefault();

                            if (fixedRule is not null && (fixedRule.SegmentType == 1 || fixedRule.SegmentType == 2))
                                row.BaseShift = fixedRule.SegmentType;
                            else
                                row.BaseShift = 1;
                        }
                    }

                    ShiftScheduleGrid.ItemsSource = null;
                    ShiftScheduleGrid.ItemsSource = _shiftScheduleRows;
                }
                else
                {
                    foreach (var row in _splitScheduleRows)
                    {
                        var dayRules = items
                            .Where(x => x.DayOfWeek == row.DayOfWeek && x.IsActive)
                            .OrderBy(x => x.SegmentType)
                            .ToList();

                        if (dayRules.Count == 0)
                        {
                            row.IsWorkingDay = false;
                            row.StartTime1 = "08:00";
                            row.EndTime1 = "13:00";
                            row.StartTime2 = "";
                            row.EndTime2 = "";
                            continue;
                        }

                        row.IsWorkingDay = true;

                        var part1 = dayRules.FirstOrDefault(x => x.SegmentType == 3);
                        var part2 = dayRules.FirstOrDefault(x => x.SegmentType == 4);

                        row.StartTime1 = part1?.StartTime ?? "08:00";
                        row.EndTime1 = part1?.EndTime ?? "13:00";
                        row.StartTime2 = part2?.StartTime ?? "";
                        row.EndTime2 = part2?.EndTime ?? "";
                    }

                    SplitScheduleGrid.ItemsSource = null;
                    SplitScheduleGrid.ItemsSource = _splitScheduleRows;
                }

                RenderShiftOverrideCalendar();
            }
            catch (Exception ex)
            {
                ShowError($"Učitavanje rasporeda radnika nije uspelo: {ex.Message}");
            }
        }

        private int DetectScheduleModeFromRules(List<StaffScheduleRuleDto> items)
        {
            if (items.Any(x => x.SegmentType == 3 || x.SegmentType == 4))
                return 2; // Dvokratno

            if (items.Any(x => x.SegmentType == 1 || x.SegmentType == 2))
                return 1; // Smenski rad

            return 0; // Fiksno
        }

        private async void SaveStaffServicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedStaff is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite radnika.";
                    return;
                }

                var selectedServiceIds = _staffServiceRows
                    .Where(x => x.IsAssigned)
                    .Select(x => x.Id)
                    .ToList();

                StatusTextBlock.Text = "Čuvanje usluga radnika je u toku...";

                await _apiClient.UpdateStaffServicesAsync(
                    _selectedStaff.Id,
                    new UpdateStaffServicesRequestDto
                    {
                        ServiceIds = selectedServiceIds
                    });

                await LoadStaffServiceAssignmentsMapAsync();

                if (_selectedService is not null &&
                    (!_staffToServiceIds.TryGetValue(_selectedStaff.Id, out var allowedServiceIds) ||
                     !allowedServiceIds.Contains(_selectedService.Id)))
                {
                    foreach (var row in _timelineRows.Where(x => x.StaffId == _selectedStaff.Id))
                    {
                        if (row.ResourceId.HasValue &&
                            !IsWaitingResource(row.ResourceId) &&
                            (!_staffToResourceIds.TryGetValue(_selectedStaff.Id, out var allowedIds) ||
                             !allowedIds.Contains(row.ResourceId.Value)))
                        {
                            row.ResourceId = null;
                        }
                    }
                }

                RenderTimelineEditor();
                StatusTextBlock.Text = "Usluge radnika su sačuvane.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async Task LoadStaffResourcesAsync(long staffId)
        {
            try
            {
                var items = await _apiClient.GetStaffResourcesAsync(staffId);

                _staffResourceRows.Clear();
                _staffResourceRows.AddRange(
                    items
                        .OrderBy(x => x.ResourceName)
                        .Select(x => new StaffResourceEditorRow
                        {
                            Id = x.ResourceId,
                            Name = x.ResourceName ?? "",
                            ResourceType = x.ResourceType,
                            IsAssigned = x.IsAssigned
                        }));

                StaffResourcesListBox.ItemsSource = null;
                StaffResourcesListBox.ItemsSource = _staffResourceRows;
            }
            catch (Exception ex)
            {
                ShowError($"Učitavanje resursa radnika nije uspelo: {ex.Message}");
                StaffResourcesListBox.ItemsSource = null;
            }
        }

        private async void SaveStaffResourcesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedStaff is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite radnika.";
                    return;
                }

                var selectedResourceIds = _staffResourceRows
                    .Where(x => x.IsAssigned)
                    .Select(x => x.Id)
                    .ToList();

                StatusTextBlock.Text = "Čuvanje resursa radnika je u toku...";

                await _apiClient.UpdateStaffResourcesAsync(
                    _selectedStaff.Id,
                    new UpdateStaffResourcesRequestDto
                    {
                        ResourceIds = selectedResourceIds
                    });

                await LoadStaffResourceAssignmentsMapAsync();

                foreach (var row in _timelineRows.Where(x => x.StaffId == _selectedStaff.Id))
                {
                    if (row.ResourceId.HasValue &&
                        !IsWaitingResource(row.ResourceId) &&
                        (!_staffToResourceIds.TryGetValue(_selectedStaff.Id, out var allowedIds) ||
                         !allowedIds.Contains(row.ResourceId.Value)))
                    {
                        row.ResourceId = null;
                    }
                }

                RenderTimelineEditor();
                StatusTextBlock.Text = "Resursi radnika su sačuvani.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }
        private async Task LoadStaffServicesAsync(long staffId)
        {
            try
            {
                var items = await _apiClient.GetStaffServicesAsync(staffId);

                _staffServiceRows.Clear();
                _staffServiceRows.AddRange(
                    items
                        .OrderBy(x => x.ServiceName)
                        .Select(x => new StaffServiceEditorRow
                        {
                            Id = x.ServiceId,
                            Name = x.ServiceName ?? "",
                            IsAssigned = x.IsAssigned
                        }));

                StaffServicesListBox.ItemsSource = null;
                StaffServicesListBox.ItemsSource = _staffServiceRows;

                StatusTextBlock.Text = $"Učitano usluga za radnika: {_staffServiceRows.Count}.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Učitavanje usluga radnika nije uspelo: {ex.Message}";
                StaffServicesListBox.ItemsSource = null;
            }
        }

        private async void SaveNewStaffButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Čuvanje novog radnika je u toku...";

                var request = BuildCreateStaffRequestFromForm();
                await _apiClient.CreateStaffAsync(request);

                await LoadStaffAsync();
                PrepareNewStaffForm();

                StatusTextBlock.Text = "Novi radnik je sačuvan.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void SaveStaffChangesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedStaff is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite radnika iz liste.";
                    return;
                }

                StatusTextBlock.Text = "Čuvanje izmena radnika je u toku...";

                var request = BuildUpdateStaffRequestFromForm();
                var updated = await _apiClient.UpdateStaffAsync(_selectedStaff.Id, request);

                await LoadStaffAsync();
                SelectStaffById(updated.Id);

                StatusTextBlock.Text = "Izmene radnika su sačuvane.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void ActivateStaffButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedStaff is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite radnika iz liste.";
                    return;
                }

                await _apiClient.ActivateStaffAsync(_selectedStaff.Id);
                await LoadStaffAsync();
                SelectStaffById(_selectedStaff.Id);

                StatusTextBlock.Text = "Radnik je aktiviran.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void DeactivateStaffButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedStaff is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite radnika iz liste.";
                    return;
                }

                await _apiClient.DeactivateStaffAsync(_selectedStaff.Id);
                await LoadStaffAsync();
                SelectStaffById(_selectedStaff.Id);

                StatusTextBlock.Text = "Radnik je deaktiviran.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void DeleteStaffButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedStaff is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite radnika iz liste.";
                    return;
                }

                var result = MessageBox.Show(
                    $"Da li ste sigurni da želite da obrišete radnika \"{_selectedStaff.DisplayName}\"?",
                    "Potvrda brisanja",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                await _apiClient.DeleteStaffAsync(_selectedStaff.Id);
                await LoadStaffAsync();
                PrepareNewStaffForm();

                StatusTextBlock.Text = "Radnik je obrisan.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async Task LoadStaffAsync()
        {
            try
            {
                var previouslySelectedStaffId = _selectedStaff?.Id;

                var items = await _apiClient.GetStaffAsync(_businessId);

                _staff.Clear();
                _staff.AddRange(items.OrderBy(x => x.DisplayName));

                StaffGrid.ItemsSource = null;
                StaffGrid.ItemsSource = _staff;

                if (previouslySelectedStaffId.HasValue)
                {
                    var selected = _staff.FirstOrDefault(x => x.Id == previouslySelectedStaffId.Value);
                    if (selected is not null)
                    {
                        StaffGrid.SelectedItem = selected;
                        StaffGrid.ScrollIntoView(selected);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void PrepareNewStaffForm()
        {
            _selectedStaff = null;
            StaffGrid.SelectedItem = null;

            StaffDisplayNameTextBox.Text = "";
            StaffTitleTextBox.Text = "";
            StaffIsBookableCheckBox.IsChecked = true;
            StaffIsActiveCheckBox.IsChecked = true;

            _staffServiceRows.Clear();
            StaffServicesListBox.ItemsSource = null;

            StaffScheduleModeComboBox.SelectedIndex = 0;
            UpdateScheduleModePanels();
            InitializeDefaultScheduleRows();

            StatusTextBlock.Text = "Forma za novog radnika je spremna.";
        }



        private async Task LoadStaffResourceAssignmentsMapAsync()
        {
            _staffToResourceIds.Clear();

            foreach (var staff in _staff)
            {
                try
                {
                    var items = await _apiClient.GetStaffResourcesAsync(staff.Id);

                    _staffToResourceIds[staff.Id] = items
                        .Where(x => x.IsAssigned)
                        .Select(x => x.ResourceId)
                        .ToHashSet();
                }
                catch
                {
                    _staffToResourceIds[staff.Id] = new HashSet<long>();
                }
            }
        }

        private async void SaveStaffScheduleRulesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedStaff is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite radnika.";
                    return;
                }

                var mode = GetSelectedStaffScheduleMode();
                _lastSavedScheduleMode = mode;
                var rules = new List<StaffScheduleRuleRowDto>();

                if (mode == 0)
                {
                    foreach (var row in _fixedScheduleRows)
                    {
                        if (!row.IsWorkingDay)
                            continue;

                        rules.Add(new StaffScheduleRuleRowDto
                        {
                            DayOfWeek = row.DayOfWeek,
                            WeekType = 0,
                            SegmentType = 0,
                            StartTime = NormalizeWorkingHourTime(row.StartTime),
                            EndTime = NormalizeWorkingHourTime(row.EndTime),
                            IsActive = true
                        });
                    }
                }
                else if (mode == 1)
                {
                    var shift1Start = NormalizeWorkingHourTime(Shift1StartTextBox.Text);
                    var shift1End = NormalizeWorkingHourTime(Shift1EndTextBox.Text);
                    var shift2Start = NormalizeWorkingHourTime(Shift2StartTextBox.Text);
                    var shift2End = NormalizeWorkingHourTime(Shift2EndTextBox.Text);

                    foreach (var row in _shiftScheduleRows)
                    {
                        if (!row.IsWorkingDay)
                            continue;

                        if (row.RotateWeekly)
                        {
                            rules.Add(new StaffScheduleRuleRowDto
                            {
                                DayOfWeek = row.DayOfWeek,
                                WeekType = 1,
                                SegmentType = row.BaseShift,
                                StartTime = row.BaseShift == 1 ? shift1Start : shift2Start,
                                EndTime = row.BaseShift == 1 ? shift1End : shift2End,
                                IsActive = true
                            });

                            var otherShift = row.BaseShift == 1 ? 2 : 1;

                            rules.Add(new StaffScheduleRuleRowDto
                            {
                                DayOfWeek = row.DayOfWeek,
                                WeekType = 2,
                                SegmentType = otherShift,
                                StartTime = otherShift == 1 ? shift1Start : shift2Start,
                                EndTime = otherShift == 1 ? shift1End : shift2End,
                                IsActive = true
                            });
                        }
                        else
                        {
                            rules.Add(new StaffScheduleRuleRowDto
                            {
                                DayOfWeek = row.DayOfWeek,
                                WeekType = 0,
                                SegmentType = row.BaseShift,
                                StartTime = row.BaseShift == 1 ? shift1Start : shift2Start,
                                EndTime = row.BaseShift == 1 ? shift1End : shift2End,
                                IsActive = true
                            });
                        }
                    }
                }
                else
                {
                    foreach (var row in _splitScheduleRows)
                    {
                        if (!row.IsWorkingDay)
                            continue;

                        rules.Add(new StaffScheduleRuleRowDto
                        {
                            DayOfWeek = row.DayOfWeek,
                            WeekType = 0,
                            SegmentType = 3,
                            StartTime = NormalizeWorkingHourTime(row.StartTime1),
                            EndTime = NormalizeWorkingHourTime(row.EndTime1),
                            IsActive = true
                        });

                        if (!string.IsNullOrWhiteSpace(row.StartTime2) &&
                            !string.IsNullOrWhiteSpace(row.EndTime2))
                        {
                            rules.Add(new StaffScheduleRuleRowDto
                            {
                                DayOfWeek = row.DayOfWeek,
                                WeekType = 0,
                                SegmentType = 4,
                                StartTime = NormalizeWorkingHourTime(row.StartTime2),
                                EndTime = NormalizeWorkingHourTime(row.EndTime2),
                                IsActive = true
                            });
                        }
                    }
                }

                StatusTextBlock.Text = "Čuvanje rasporeda radnika je u toku...";

                var request = new ReplaceStaffScheduleRulesRequestDto
                {
                    StaffMemberId = _selectedStaff.Id,
                    ScheduleMode = mode,
                    Rules = rules
                };

                await _apiClient.ReplaceStaffScheduleRulesAsync(request);

                _selectedStaff.ScheduleMode = mode;
                _lastSavedScheduleMode = mode;

                await LoadStaffScheduleAsync(_selectedStaff.Id, mode);

                StatusTextBlock.Text = "Raspored radnika je sačuvan.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private CreateStaffRequestDto BuildCreateStaffRequestFromForm()
        {
            var displayName = StaffDisplayNameTextBox.Text.Trim();
            var title = StaffTitleTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException("Unesite ime radnika.");

            return new CreateStaffRequestDto
            {
                BusinessId = _businessId,
                DisplayName = displayName,
                Title = string.IsNullOrWhiteSpace(title) ? null : title,
                IsBookable = StaffIsBookableCheckBox.IsChecked == true,
                ScheduleMode = GetSelectedStaffScheduleMode()
            };
        }



        private UpdateStaffRequestDto BuildUpdateStaffRequestFromForm()
        {
            var displayName = StaffDisplayNameTextBox.Text.Trim();
            var title = StaffTitleTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException("Unesite ime radnika.");

            return new UpdateStaffRequestDto
            {
                DisplayName = displayName,
                Title = string.IsNullOrWhiteSpace(title) ? null : title,
                IsBookable = StaffIsBookableCheckBox.IsChecked == true,
                IsActive = StaffIsActiveCheckBox.IsChecked == true,
                ScheduleMode = GetSelectedStaffScheduleMode()
            };
        }

        private void SelectStaffById(long id)
        {
            var item = _staff.FirstOrDefault(x => x.Id == id);
            if (item is not null)
            {
                StaffGrid.SelectedItem = item;
                StaffGrid.ScrollIntoView(item);
            }
        }

        private async void RefreshWorkingHoursButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadWorkingHoursAsync();
        }

        private async void SaveWorkingHoursButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Čuvanje radnog vremena je u toku...";

                foreach (var row in _workingHours.OrderBy(x => x.DayOfWeek))
                {
                    var request = new
                    {
                        businessId = _businessId,
                        dayOfWeek = row.DayOfWeek,
                        startTime = row.IsWorkingDay ? NormalizeWorkingHourTime(row.StartTime) : "00:00",
                        endTime = row.IsWorkingDay ? NormalizeWorkingHourTime(row.EndTime) : "00:00",
                        isClosed = !row.IsWorkingDay
                    };

                    await _apiClient.UpdateBusinessWorkingHoursAsync(request);
                }

                await LoadWorkingHoursAsync();
                StatusTextBlock.Text = "Radno vreme je sačuvano.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async Task LoadWorkingHoursAsync()
        {
            List<BusinessWorkingHourDto> items;

            try
            {
                items = await _apiClient.GetBusinessWorkingHoursAsync(_businessId);
            }
            catch (Exception ex)
            {
                items = new List<BusinessWorkingHourDto>();
                StatusTextBlock.Text = $"Radno vreme još nije podešeno. Možete ga uneti ručno. Detalj: {ex.Message}";
            }

            _workingHours.Clear();

            for (var day = 1; day <= 7; day++)
            {
                var existing = items.FirstOrDefault(x => x.DayOfWeek == day);

                _workingHours.Add(new WorkingHourEditorRow
                {
                    DayOfWeek = day,
                    DayName = GetDayName(day),
                    StartTime = existing?.StartTime ?? "09:00",
                    EndTime = existing?.EndTime ?? "17:00",
                    IsWorkingDay = existing?.IsWorkingDay ?? (day <= 5)
                });
            }

            WorkingHoursGrid.ItemsSource = null;
            WorkingHoursGrid.ItemsSource = _workingHours;
        }

        private async void RefreshBusinessButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadBusinessAsync();
        }

        private async void SaveBusinessButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_business is null)
                {
                    StatusTextBlock.Text = "Podaci o biznisu nisu učitani.";
                    return;
                }

                StatusTextBlock.Text = "Čuvanje podataka o biznisu je u toku...";

                var request = BuildUpdateBusinessRequestFromForm();
                var updated = await _apiClient.UpdateBusinessAsync(_business.Id, request);

                _business = updated;
                FillBusinessForm(updated);

                StatusTextBlock.Text = "Podaci o biznisu su sačuvani.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async Task LoadBusinessAsync()
        {
            try
            {
                _business = await _apiClient.GetBusinessByIdAsync(_businessId);
                FillBusinessForm(_business);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }


        private void FillBusinessForm(BusinessDetailsDto business)
        {
            BusinessNameTextBox.Text = business.Name ?? "";
            BusinessPhoneTextBox.Text = business.Phone ?? "";
            BusinessEmailTextBox.Text = business.Email ?? "";
            BusinessDescriptionTextBox.Text = business.Description ?? "";
            BusinessSlotIntervalTextBox.Text = business.SlotIntervalMin.ToString(CultureInfo.InvariantCulture);

            BusinessStreetTextBox.Text = business.Street ?? "";
            BusinessStreetNumberTextBox.Text = business.StreetNumber ?? "";
            BusinessCityTextBox.Text = business.City ?? "";
            BusinessPostalCodeTextBox.Text = business.PostalCode ?? "";
            BusinessCountryTextBox.Text = business.Country ?? "";
            BusinessLatitudeTextBox.Text = business.Latitude?.ToString(CultureInfo.InvariantCulture) ?? "";
            BusinessLongitudeTextBox.Text = business.Longitude?.ToString(CultureInfo.InvariantCulture) ?? "";
            BusinessGooglePlaceIdTextBox.Text = business.GooglePlaceId ?? "";
            BusinessIsActiveCheckBox.IsChecked = business.IsActive;

            SelectBusinessType(business.BusinessType);
        }

        private UpdateBusinessRequestDto BuildUpdateBusinessRequestFromForm()
        {
            var name = BusinessNameTextBox.Text.Trim();
            var slotIntervalText = BusinessSlotIntervalTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Unesite naziv biznisa.");

            if (!int.TryParse(slotIntervalText, out var slotIntervalMin) || slotIntervalMin <= 0)
                throw new InvalidOperationException("Interval termina mora biti pozitivan broj.");

            return new UpdateBusinessRequestDto
            {
                Name = name,
                BusinessType = GetSelectedBusinessType(),
                Description = NullIfEmpty(BusinessDescriptionTextBox.Text),
                Phone = NullIfEmpty(BusinessPhoneTextBox.Text),
                Email = NullIfEmpty(BusinessEmailTextBox.Text),
                SlotIntervalMin = slotIntervalMin,
                Street = NullIfEmpty(BusinessStreetTextBox.Text),
                StreetNumber = NullIfEmpty(BusinessStreetNumberTextBox.Text),
                City = NullIfEmpty(BusinessCityTextBox.Text),
                PostalCode = NullIfEmpty(BusinessPostalCodeTextBox.Text),
                Country = NullIfEmpty(BusinessCountryTextBox.Text),
                Latitude = TryParseNullableDouble(BusinessLatitudeTextBox.Text),
                Longitude = TryParseNullableDouble(BusinessLongitudeTextBox.Text),
                GooglePlaceId = NullIfEmpty(BusinessGooglePlaceIdTextBox.Text),
                IsActive = BusinessIsActiveCheckBox.IsChecked == true
            };
        }

        private void SelectBusinessType(int businessType)
        {
            foreach (var item in BusinessTypeComboBox.Items)
            {
                if (item is ComboBoxItem combo && combo.Tag is string tag &&
                    int.TryParse(tag, out var value) && value == businessType)
                {
                    BusinessTypeComboBox.SelectedItem = combo;
                    return;
                }
            }

            if (BusinessTypeComboBox.Items.Count > 0)
                BusinessTypeComboBox.SelectedIndex = 0;
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

            if (double.TryParse(value.Trim(), NumberStyles.Any, new CultureInfo("sr-Latn-RS"), out parsed))
                return parsed;

            throw new InvalidOperationException($"Vrednost '{value}' nije u ispravnom formatu.");
        }

        private static string NormalizeTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "09:00";

            var trimmed = value.Trim();

            if (TimeSpan.TryParse(trimmed, out var time))
                return time.ToString(@"hh\:mm");

            throw new InvalidOperationException($"Vreme '{value}' nije u ispravnom formatu. Koristite npr. 09:00.");
        }

        private static string NormalizeWorkingHourTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "09:00";

            var trimmed = value.Trim();

            if (TimeSpan.TryParse(trimmed, out var time))
                return time.ToString(@"hh\:mm");

            throw new InvalidOperationException($"Vreme '{value}' nije u ispravnom formatu. Koristite npr. 09:00.");
        }

        private void ShowError(string message)
        {
            StatusTextBlock.Text = message;
            MessageBox.Show(message, "Greška", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ShowSuccess(string message)
        {
            StatusTextBlock.Text = message;
        }

        private static int MapUiDayOfWeekToApiDayOfWeek(int uiDayOfWeek)
        {
            return uiDayOfWeek switch
            {
                >= 1 and <= 7 => uiDayOfWeek,
                _ => throw new InvalidOperationException("Dan u nedelji nije ispravno zadat.")
            };
        }

        private void RenderAbsenceCalendar()
        {
            _absenceCalendarCells.Clear();

            var headers = new[] { "Pon", "Uto", "Sre", "Čet", "Pet", "Sub", "Ned" };
            foreach (var header in headers)
            {
                _absenceCalendarCells.Add(new AbsenceCalendarCell
                {
                    DayText = header,
                    IsHeader = true,
                    IsCurrentMonth = true
                });
            }

            var firstDay = new DateTime(_absenceMonth.Year, _absenceMonth.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(_absenceMonth.Year, _absenceMonth.Month);

            var offset = ((int)firstDay.DayOfWeek + 6) % 7; // Monday=0

            for (var i = 0; i < offset; i++)
            {
                var prevDate = firstDay.AddDays(-(offset - i));
                _absenceCalendarCells.Add(new AbsenceCalendarCell
                {
                    DayText = prevDate.Day.ToString(),
                    Date = prevDate,
                    IsCurrentMonth = false,
                    IsSelectedAbsence = _selectedAbsenceDates.Contains(prevDate.Date)
                });
            }

            for (var day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(_absenceMonth.Year, _absenceMonth.Month, day);

                _absenceCalendarCells.Add(new AbsenceCalendarCell
                {
                    DayText = day.ToString(),
                    Date = date,
                    IsCurrentMonth = true,
                    IsSelectedAbsence = _selectedAbsenceDates.Contains(date.Date)
                });
            }

            while (_absenceCalendarCells.Count % 7 != 0)
            {
                var nextDate = firstDay.AddDays(_absenceCalendarCells.Count - 7 - offset);
                _absenceCalendarCells.Add(new AbsenceCalendarCell
                {
                    DayText = nextDate.Day.ToString(),
                    Date = nextDate,
                    IsCurrentMonth = false,
                    IsSelectedAbsence = _selectedAbsenceDates.Contains(nextDate.Date)
                });
            }

            AbsenceMonthTextBlock.Text = _absenceMonth.ToString("MMMM yyyy", new CultureInfo("sr-Latn-RS"));
            AbsenceCalendarItemsControl.ItemsSource = null;
            AbsenceCalendarItemsControl.ItemsSource = _absenceCalendarCells;
        }

        private async Task LoadStaffAbsencesAsync(long staffId)
        {
            _selectedAbsenceDates.Clear();
            _staffAbsenceBlocks.Clear();

            var fromLocal = new DateTime(_absenceMonth.Year, _absenceMonth.Month, 1);
            var toLocal = fromLocal.AddMonths(1);

            var fromUtc = DateTime.SpecifyKind(fromLocal, DateTimeKind.Local).ToUniversalTime();
            var toUtc = DateTime.SpecifyKind(toLocal, DateTimeKind.Local).ToUniversalTime();

            var items = await _apiClient.GetTimeOffBlocksAsync(
                _businessId,
                staffId,
                fromUtc,
                toUtc);

            _staffAbsenceBlocks.AddRange(
                items.Where(x => x.StaffMemberId == staffId));

            foreach (var block in _staffAbsenceBlocks)
            {
                var localStart = block.StartAtUtc.ToLocalTime().Date;
                var localEndExclusive = block.EndAtUtc.ToLocalTime().Date;

                for (var date = localStart; date < localEndExclusive; date = date.AddDays(1))
                    _selectedAbsenceDates.Add(date);
            }

            RenderAbsenceCalendar();
        }

        private async void PreviousAbsenceMonthButton_Click(object sender, RoutedEventArgs e)
        {
            _absenceMonth = _absenceMonth.AddMonths(-1);

            if (_selectedStaff is not null)
            {
                await LoadStaffAbsencesAsync(_selectedStaff.Id);
                await LoadShiftOverridesAsync(_selectedStaff.Id);
            }
            else
            {
                RenderAbsenceCalendar();
                RenderShiftOverrideCalendar();
            }
        }

        private async void SaveShiftOverridesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedStaff is null)
                {
                    StatusTextBlock.Text = "Prvo izaberite radnika.";
                    return;
                }

                if (GetSelectedStaffScheduleMode() != 1)
                {
                    StatusTextBlock.Text = "Promena smene je dostupna samo za smenski rad.";
                    return;
                }

                StatusTextBlock.Text = "Čuvanje promena smene je u toku...";

                var existingDates = _shiftOverrideItems
                    .Where(x => x.ShiftType is 1 or 2)
                    .ToDictionary(x => x.Date.Date, x => x);

                var selectedDates = _selectedShiftOverrideDates.Keys.ToHashSet();

                var datesToCreate = selectedDates.Except(existingDates.Keys).OrderBy(x => x).ToList();
                var datesToDelete = existingDates.Keys.Except(selectedDates).OrderBy(x => x).ToList();
                var datesToUpdate = selectedDates.Intersect(existingDates.Keys)
                    .Where(date => existingDates[date].ShiftType != _selectedShiftOverrideDates[date])
                    .OrderBy(x => x)
                    .ToList();

                foreach (var date in datesToCreate)
                {
                    var shift = _selectedShiftOverrideDates[date];

                    await _apiClient.CreateStaffScheduleOverrideAsync(new CreateOrUpdateStaffScheduleOverrideRequestDto
                    {
                        StaffMemberId = _selectedStaff.Id,
                        Date = date,
                        OverrideType = shift == 1 ? 1 : 2,
                        ShiftType = shift,
                        StartTime = null,
                        EndTime = null,
                        IsDayOff = false,
                        Reason = "Promena smene"
                    });
                }

                foreach (var date in datesToUpdate)
                {
                    var existing = existingDates[date];
                    var shift = _selectedShiftOverrideDates[date];

                    await _apiClient.UpdateStaffScheduleOverrideAsync(existing.Id, new CreateOrUpdateStaffScheduleOverrideRequestDto
                    {
                        StaffMemberId = _selectedStaff.Id,
                        Date = date,
                        OverrideType = shift == 1 ? 1 : 2,
                        ShiftType = shift,
                        StartTime = null,
                        EndTime = null,
                        IsDayOff = false,
                        Reason = "Promena smene"
                    });
                }

                foreach (var date in datesToDelete)
                {
                    await _apiClient.DeleteStaffScheduleOverrideAsync(existingDates[date].Id);
                }

                await LoadShiftOverridesAsync(_selectedStaff.Id);
                StatusTextBlock.Text = "Promene smene su sačuvane.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void NextAbsenceMonthButton_Click(object sender, RoutedEventArgs e)
        {
            _absenceMonth = _absenceMonth.AddMonths(1);

            if (_selectedStaff is not null)
            {
                await LoadStaffAbsencesAsync(_selectedStaff.Id);
                await LoadShiftOverridesAsync(_selectedStaff.Id);
            }
            else
            {
                RenderAbsenceCalendar();
                RenderShiftOverrideCalendar();
            }
        }

        private void AbsenceDayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedStaff is null)
            {
                StatusTextBlock.Text = "Prvo izaberite radnika.";
                return;
            }

            if (sender is not Button button || button.Tag is not AbsenceCalendarCell cell)
                return;

            if (cell.IsHeader || cell.Date is null)
                return;

            var date = cell.Date.Value.Date;

            if (_selectedAbsenceDates.Contains(date))
                _selectedAbsenceDates.Remove(date);
            else
                _selectedAbsenceDates.Add(date);

            RenderAbsenceCalendar();
        }



        private int CalculateBaseShiftForDate(DateTime date)
        {
            var dayOfWeek = ((int)date.DayOfWeek + 6) % 7 + 1; // Monday=1 ... Sunday=7

            var row = _shiftScheduleRows.FirstOrDefault(x => x.DayOfWeek == dayOfWeek);
            if (row is null || !row.IsWorkingDay)
                return 0;

            var shift = row.BaseShift;

            if (!row.RotateWeekly)
                return shift;

            var weekOfYear = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                date,
                CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);

            var isEvenWeek = weekOfYear % 2 == 0;

            if (isEvenWeek)
                return shift == 1 ? 2 : 1;

            return shift;
        }

        private static string GetShiftLabel(int shift)
        {
            return shift switch
            {
                1 => "P1",
                2 => "D2",
                _ => ""
            };
        }

        private async Task LoadShiftOverridesAsync(long staffId)
        {
            _selectedShiftOverrideDates.Clear();
            _existingShiftOverrideIds.Clear();
            _shiftOverrideItems.Clear();

            var from = new DateTime(_absenceMonth.Year, _absenceMonth.Month, 1);
            var to = from.AddMonths(1);

            var items = await _apiClient.GetStaffScheduleOverridesAsync(staffId, from, to);

            _shiftOverrideItems.AddRange(items);

            foreach (var item in items)
            {
                var date = item.Date.Date;

                if (item.ShiftType is 1 or 2)
                {
                    _selectedShiftOverrideDates[date] = item.ShiftType.Value;
                    _existingShiftOverrideIds[date] = item.Id;
                }
            }

            RenderShiftOverrideCalendar();
        }

        private void ShiftOverrideDayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedStaff is null)
            {
                StatusTextBlock.Text = "Prvo izaberite radnika.";
                return;
            }

            if (GetSelectedStaffScheduleMode() != 1)
            {
                StatusTextBlock.Text = "Promena smene je dostupna samo za smenski rad.";
                return;
            }

            if (sender is not Button button || button.Tag is not ShiftOverrideCalendarCell cell)
                return;

            if (cell.IsHeader || cell.Date is null)
                return;

            var date = cell.Date.Value.Date;
            var baseShift = _baseShiftByDate.TryGetValue(date, out var value) ? value : 0;

            if (baseShift == 0)
                return;

            if (_selectedShiftOverrideDates.ContainsKey(date))
                _selectedShiftOverrideDates.Remove(date);
            else
                _selectedShiftOverrideDates[date] = baseShift == 1 ? 2 : 1;

            RenderShiftOverrideCalendar();
        }

        private void RenderShiftOverrideCalendar()
        {
            _shiftOverrideCalendarCells.Clear();
            _baseShiftByDate.Clear();

            var headers = new[] { "Pon", "Uto", "Sre", "Čet", "Pet", "Sub", "Ned" };
            foreach (var header in headers)
            {
                _shiftOverrideCalendarCells.Add(new ShiftOverrideCalendarCell
                {
                    DayText = header,
                    IsHeader = true,
                    IsCurrentMonth = true
                });
            }

            var firstDay = new DateTime(_absenceMonth.Year, _absenceMonth.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(_absenceMonth.Year, _absenceMonth.Month);
            var offset = ((int)firstDay.DayOfWeek + 6) % 7;

            for (var i = 0; i < offset; i++)
            {
                var prevDate = firstDay.AddDays(-(offset - i));
                var shift = GetEffectiveShiftForDate(prevDate.Date);

                _shiftOverrideCalendarCells.Add(new ShiftOverrideCalendarCell
                {
                    DayText = prevDate.Day.ToString(),
                    Date = prevDate.Date,
                    IsCurrentMonth = false,
                    IsShiftOverride = _selectedShiftOverrideDates.ContainsKey(prevDate.Date),
                    ShiftText = GetShiftLabel(shift)
                });
            }

            for (var day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(_absenceMonth.Year, _absenceMonth.Month, day);
                var baseShift = CalculateBaseShiftForDate(date);
                _baseShiftByDate[date.Date] = baseShift;

                var effectiveShift = GetEffectiveShiftForDate(date.Date);

                _shiftOverrideCalendarCells.Add(new ShiftOverrideCalendarCell
                {
                    DayText = day.ToString(),
                    Date = date.Date,
                    IsCurrentMonth = true,
                    IsShiftOverride = _selectedShiftOverrideDates.ContainsKey(date.Date),
                    ShiftText = GetShiftLabel(effectiveShift)
                });
            }

            while (_shiftOverrideCalendarCells.Count % 7 != 0)
            {
                var nextDate = firstDay.AddDays(_shiftOverrideCalendarCells.Count - 7 - offset);
                var shift = GetEffectiveShiftForDate(nextDate.Date);

                _shiftOverrideCalendarCells.Add(new ShiftOverrideCalendarCell
                {
                    DayText = nextDate.Day.ToString(),
                    Date = nextDate.Date,
                    IsCurrentMonth = false,
                    IsShiftOverride = _selectedShiftOverrideDates.ContainsKey(nextDate.Date),
                    ShiftText = GetShiftLabel(shift)
                });
            }

            ShiftOverrideMonthTextBlock.Text = _absenceMonth.ToString("MMMM yyyy", new CultureInfo("sr-Latn-RS"));
            ShiftOverrideCalendarItemsControl.ItemsSource = null;
            ShiftOverrideCalendarItemsControl.ItemsSource = _shiftOverrideCalendarCells;
        }

        private int GetEffectiveShiftForDate(DateTime date)
        {
            if (_selectedShiftOverrideDates.TryGetValue(date.Date, out var overriddenShift))
                return overriddenShift;

            if (_baseShiftByDate.TryGetValue(date.Date, out var baseShift))
                return baseShift;

            return 0;
        }

        public sealed class LookupOption
        {
            public int Value { get; }
            public string Label { get; }

            public LookupOption(int value, string label)
            {
                Value = value;
                Label = label;
            }
        }


        private static string GetDayName(int dayOfWeek)
        {
            return dayOfWeek switch
            {
                1 => "Ponedeljak",
                2 => "Utorak",
                3 => "Sreda",
                4 => "Četvrtak",
                5 => "Petak",
                6 => "Subota",
                7 => "Nedelja",
                _ => "Nepoznato"
            };
        }

        private sealed class StaffServiceEditorRow
        {
            public long Id { get; set; }
            public string Name { get; set; } = "";
            public bool IsAssigned { get; set; }
        }

        private sealed class WorkingHourEditorRow
        {
            public int DayOfWeek { get; set; }
            public string DayName { get; set; } = "";
            public string StartTime { get; set; } = "";
            public string EndTime { get; set; } = "";
            public bool IsWorkingDay { get; set; }
        }

        private sealed class TimelineEditorRow
        {
            public long? ExistingUsageId { get; set; }
            public long? StaffId { get; set; }
            public long? ResourceId { get; set; }
            public bool IsRequired { get; set; } = true;
            public HashSet<int> ActiveMinutes { get; } = new();
        }

        private sealed class StaffResourceEditorRow
        {
            public long Id { get; set; }
            public string Name { get; set; } = "";
            public int ResourceType { get; set; }
            public bool IsAssigned { get; set; }
        }

private sealed class TimelineSaveRow
{
    public long? StaffId { get; set; }
    public long ResourceId { get; set; }
    public int StartMinute { get; set; }
    public int DurationMin { get; set; }
    public bool IsRequired { get; set; }
}



        private sealed class FixedScheduleEditorRow
        {
            public int DayOfWeek { get; set; }
            public string DayName { get; set; } = "";
            public bool IsWorkingDay { get; set; }
            public string StartTime { get; set; } = "08:00";
            public string EndTime { get; set; } = "17:00";
        }

        private sealed class ShiftScheduleEditorRow
        {
            public int DayOfWeek { get; set; }
            public string DayName { get; set; } = "";
            public bool IsWorkingDay { get; set; }
            public int BaseShift { get; set; } = 1; // 1 = prva, 2 = druga
            public bool RotateWeekly { get; set; }
        }

        private sealed class SplitScheduleEditorRow
        {
            public int DayOfWeek { get; set; }
            public string DayName { get; set; } = "";
            public bool IsWorkingDay { get; set; }
            public string StartTime1 { get; set; } = "08:00";
            public string EndTime1 { get; set; } = "13:00";
            public string StartTime2 { get; set; } = "14:00";
            public string EndTime2 { get; set; } = "17:00";
        }

        private sealed class ShiftOverrideCalendarCell
        {
            public string DayText { get; set; } = "";
            public DateTime? Date { get; set; }
            public bool IsHeader { get; set; }
            public bool IsCurrentMonth { get; set; }
            public bool IsShiftOverride { get; set; }
            public string ShiftText { get; set; } = "";
        }

        private sealed class AbsenceCalendarCell
        {
            public string DayText { get; set; } = "";
            public DateTime? Date { get; set; }
            public bool IsHeader { get; set; }
            public bool IsCurrentMonth { get; set; }
            public bool IsSelectedAbsence { get; set; }
        }
    }
}