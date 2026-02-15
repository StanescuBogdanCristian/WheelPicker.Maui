using SBC.WheelPicker;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using SelectionChangedEventArgs = SBC.WheelPicker.SelectionChangedEventArgs;

namespace Sample.SampleViews;

public partial class DatePicker : Grid
{
    private const int MinYear = 1920;
    private const int MaxYear = 2100;

    private readonly ObservableCollection<string> _days = new();
    private readonly ObservableCollection<string> _months = new();
    private readonly ObservableCollection<string> _years = new();

    private bool _suppressSync;
    private int _currentDaysInMonth;
    private bool _pendingDaysRefresh;

    private DateTime _selectedDate = DateTime.Today;

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (_selectedDate != value)
            {
                _selectedDate = value;
                OnSelectedDateChanged(_selectedDate);
            }
        }
    }

    public DatePicker()
    {
        InitializeComponent();

        PopulateYears();
        PopulateMonths();
        PopulateDays();

        selectedDateLabel.Text = $"{SelectedDate:dd MMMM yyyy}";

        dayPicker.SelectedIndexChanged += OnDayChanged;
        monthPicker.SelectedIndexChanged += OnMonthChanged;
        yearPicker.SelectedIndexChanged += OnYearChanged;

        dayPicker.PropertyChanged += OnDayPickerPropertyChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        dayPicker.ItemsSource = _days;
        monthPicker.ItemsSource = _months;
        yearPicker.ItemsSource = _years;

        SyncPickersToDate(SelectedDate);
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        dayPicker.CancelAllAnimations();
        monthPicker.CancelAllAnimations();
        yearPicker.CancelAllAnimations();
    }


    #region Population

    private void PopulateYears()
    {
        for (int y = MinYear; y <= MaxYear; y++)
            _years.Add(y.ToString());
    }

    private void PopulateMonths()
    {
        var culture = CultureInfo.CurrentCulture;
        for (int m = 1; m <= 12; m++)
            _months.Add(culture.DateTimeFormat.GetMonthName(m));
    }

    private void PopulateDays()
    {
        int year = GetSelectedYear();
        int month = GetSelectedMonth();
        int daysInMonth = DateTime.DaysInMonth(year, month);

        if (_currentDaysInMonth == daysInMonth)
            return;

        _currentDaysInMonth = daysInMonth;
        bool dayPickerBusy = dayPicker.IsSpinning || dayPicker.IsDragging;

        _suppressSync = true;

        if (dayPickerBusy)
        {
            // Don't touch the collection while day wheel is scrolling
            if (dayPicker.SelectedIndex >= daysInMonth)
                dayPicker.SelectedIndex = daysInMonth - 1;

            _suppressSync = false;
            _pendingDaysRefresh = true;
            return;
        }

        RebuildDayItems(daysInMonth);

        _suppressSync = false;
    }

    private void RebuildDayItems(int daysInMonth)
    {
        if (dayPicker.SelectedIndex >= daysInMonth)
            dayPicker.SelectedIndex = daysInMonth - 1;

        while (_days.Count > daysInMonth)
            _days.RemoveAt(_days.Count - 1);

        for (int d = 0; d < daysInMonth; d++)
        {
            string text = (d + 1).ToString("D2");
            if (d < _days.Count)
            {
                if (_days[d] != text)
                    _days[d] = text;
            }
            else
            {
                _days.Add(text);
            }
        }
    }

    #endregion

    #region Selection Handlers

    private void OnDayChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_suppressSync)
            SyncDateFromPickers();
    }

    private void OnMonthChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSync) return;

        PopulateDays();
        SyncDateFromPickers();
    }

    private void OnYearChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSync) return;

        PopulateDays();
        SyncDateFromPickers();
    }

    #endregion

    private void OnDayPickerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WheelPicker.IsSpinning)) return;
        if (dayPicker.IsSpinning || !_pendingDaysRefresh) return;

        _pendingDaysRefresh = false;

        _suppressSync = true;
        RebuildDayItems(_currentDaysInMonth);
        _suppressSync = false;

        SyncDateFromPickers();
    }

    #region Sync Logic

    private void SyncDateFromPickers()
    {
        int day = GetSelectedDay();
        int month = GetSelectedMonth();
        int year = GetSelectedYear();

        if (day < 1 || month < 1 || year < 1)
            return;

        int maxDay = DateTime.DaysInMonth(year, month);
        if (day > maxDay)
            day = maxDay;

        var newDate = new DateTime(year, month, day);

        _suppressSync = true;
        SelectedDate = newDate;
        _suppressSync = false;
    }

    private void SyncPickersToDate(DateTime date)
    {
        _suppressSync = true;

        int yearIndex = date.Year - MinYear;
        if (yearIndex >= 0 && yearIndex < _years.Count)
            yearPicker.SelectedIndex = yearIndex;

        monthPicker.SelectedIndex = date.Month - 1;

        // Force days rebuild (pickers are stopped, safe to modify)
        _currentDaysInMonth = 0;
        _pendingDaysRefresh = false;

        PopulateDays();

        dayPicker.SelectedIndex = date.Day - 1;

        _suppressSync = false;
    }

    #endregion

    #region Value Extractors

    private int GetSelectedDay() =>
        dayPicker.SelectedIndex >= 0 ? dayPicker.SelectedIndex + 1 : 1;

    private int GetSelectedMonth() =>
        monthPicker.SelectedIndex >= 0 ? monthPicker.SelectedIndex + 1 : 1;

    private int GetSelectedYear() =>
        yearPicker.SelectedIndex >= 0 ? MinYear + yearPicker.SelectedIndex : DateTime.Today.Year;

    #endregion

    private void OnSelectedDateChanged(DateTime value)
    {
        if (!_suppressSync)
            SyncPickersToDate(value);

        selectedDateLabel.Text = $"{value:dd MMMM yyyy}";
        todayButton.IsEnabled = value.Date != DateTime.Today;
        dayPicker.IsSelectionAnimated = true;
    }

    private void OnTodayButtonClicked(object sender, EventArgs e)
    {
        SelectedDate = DateTime.Today;
    }
}