using System.Collections.ObjectModel;
using System.Globalization;
using SelectionChangedEventArgs = SBC.WheelPicker.SelectionChangedEventArgs;

namespace Sample.SampleViews;

public partial class DatePicker : VerticalStackLayout
{
    private const int MinYear = 1920;
    private const int MaxYear = 2100;

    private readonly ObservableCollection<string> _days = new();
    private readonly ObservableCollection<string> _months = new();
    private readonly ObservableCollection<string> _years = new();

    private bool _suppressSync;
    private int _currentDaysInMonth;

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

        dayPicker.ItemsSource = _days;
        monthPicker.ItemsSource = _months;
        yearPicker.ItemsSource = _years;

        SyncPickersToDate(SelectedDate);
        selectedDateLabel.Text = $"{SelectedDate:dd MMMM yyyy}";

        dayPicker.SelectedIndexChanged += OnDayChanged;
        monthPicker.SelectedIndexChanged += OnMonthChanged;
        yearPicker.SelectedIndexChanged += OnYearChanged;
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

        _suppressSync = true;

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

        _suppressSync = false;
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