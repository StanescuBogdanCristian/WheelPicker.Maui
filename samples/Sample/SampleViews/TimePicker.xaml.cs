using SBC.WheelPicker;
using System.Collections.ObjectModel;

namespace Sample.SampleViews;

public partial class TimePicker : Grid
{
    private TimeSpan _selectedTime = DateTime.Now.TimeOfDay;

    public TimeSpan SelectedTime
    {
        get => _selectedTime;
        set
        {
            if (_selectedTime != value)
            {
                _selectedTime = value;
                OnSelectedTimeChanged(_selectedTime);
            }
        }
    }

    private readonly ObservableCollection<string> _hours = new();
    private readonly ObservableCollection<string> _minutes = new();
    private readonly ObservableCollection<string> _periods = new();

    private bool _suppressSync;

    public TimePicker()
    {
        InitializeComponent();

        hourPicker.ItemsSource = _hours;
        minutePicker.ItemsSource = _minutes;
        periodPicker.ItemsSource = _periods;

        PopulateHours();
        PopulateMinutes();
        PopulatePeriods();

        selectedTimeLabel.Text = $"{SelectedTime:hh\\:mm}";

        hourPicker.SelectedIndexChanged += OnSelectionChanged;
        minutePicker.SelectedIndexChanged += OnSelectionChanged;
        periodPicker.SelectedIndexChanged += OnSelectionChanged;

        SyncPickersToTime(SelectedTime);
    }

    public void CancelAllAnimations()
    {
        hourPicker.CancelAllAnimations();
        minutePicker.CancelAllAnimations();
        periodPicker.CancelAllAnimations();
    }

    #region Population

    private void PopulateHours()
    {
        for (int h = 1; h <= 12; h++)
            _hours.Add(h.ToString("D2"));
    }

    private void PopulateMinutes()
    {
        for (int m = 0; m < 60; m++)
            _minutes.Add(m.ToString("D2"));
    }

    private void PopulatePeriods()
    {
        _periods.Add("AM");
        _periods.Add("PM");
    }

    #endregion

    #region Selection Handler

    private void OnSelectionChanged(object? sender, IndexChangedEventArgs e)
    {
        if (!_suppressSync)
            SyncTimeFromPickers();
    }

    #endregion

    #region Sync Logic

    private void SyncTimeFromPickers()
    {
        int hour12 = GetSelectedHour();
        int minute = GetSelectedMinute();
        bool isPM = GetSelectedPeriod() == 1;

        int hour24 = To24Hour(hour12, isPM);

        var newTime = new TimeSpan(hour24, minute, 0);

        _suppressSync = true;
        SelectedTime = newTime;
        _suppressSync = false;
    }

    private void SyncPickersToTime(TimeSpan time)
    {
        _suppressSync = true;

        int hour24 = time.Hours;
        int minute = time.Minutes;

        bool isPM = hour24 >= 12;
        int hour12 = hour24 % 12;
        if (hour12 == 0) hour12 = 12;

        hourPicker.SelectedIndex = hour12 - 1;
        minutePicker.SelectedIndex = minute;
        periodPicker.SelectedIndex = isPM ? 1 : 0;

        _suppressSync = false;
    }

    #endregion

    #region Value Extractors

    private int GetSelectedHour() =>
        hourPicker.SelectedIndex >= 0 ? hourPicker.SelectedIndex + 1 : 12;

    private int GetSelectedMinute() =>
        minutePicker.SelectedIndex >= 0 ? minutePicker.SelectedIndex : 0;

    private int GetSelectedPeriod() =>
        periodPicker.SelectedIndex >= 0 ? periodPicker.SelectedIndex : 0;

    #endregion

    private static int To24Hour(int hour12, bool isPM)
    {
        if (hour12 == 12)
            return isPM ? 12 : 0;

        return isPM ? hour12 + 12 : hour12;
    }

    private void OnSelectedTimeChanged(TimeSpan value)
    {
        if (!_suppressSync)
            SyncPickersToTime(value);

        var newTime = new TimeSpan(value.Hours, value.Minutes, 0);
        var currentTime = new TimeSpan(DateTime.Now.TimeOfDay.Hours, DateTime.Now.TimeOfDay.Minutes, 0);

        selectedTimeLabel.Text = $"{value:hh\\:mm}";
        nowButton.IsEnabled = newTime != currentTime;
    }

    private void OnNowButtonClicked(object sender, EventArgs e)
    {
        SelectedTime = DateTime.Now.TimeOfDay;
    }
}
