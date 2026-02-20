using SBC.WheelPicker;

namespace Sample.SampleViews;

public partial class Timer : Grid
{
    private readonly IDispatcherTimer _timer;

    private TimeSpan _remaining = TimeSpan.Zero;
    private bool _isRunning;

    private readonly int[] _hours = Enumerable.Range(0, 24).ToArray();
    private readonly int[] _minsecs = Enumerable.Range(0, 60).ToArray();

    public Timer()
    {
        InitializeComponent();

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTick;

        hourPicker.ItemsSource = _hours;
        minutePicker.ItemsSource = _minsecs;
        secondPicker.ItemsSource = _minsecs;

        hourPicker.SelectedIndexChanged += OnWheelChanged;
        minutePicker.SelectedIndexChanged += OnWheelChanged;
        secondPicker.SelectedIndexChanged += OnWheelChanged;

        UpdateButtons();
    }

    public void CancelAllAnimations()
    {
        hourPicker.CancelAllAnimations();
        minutePicker.CancelAllAnimations();
        secondPicker.CancelAllAnimations();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_remaining <= TimeSpan.Zero)
        {
            StopInternal();
            return;
        }

        _remaining -= TimeSpan.FromSeconds(1);
        UpdateWheelsFromRemaining();
    }

    private void OnWheelChanged(object? sender, IndexChangedEventArgs e)
    {
        if (!_isRunning)
            UpdateButtons();
    }

    private void StopInternal()
    {
        _timer.Stop();
        _isRunning = false;

        SetSwipeEnabled(true);
        UpdateButtons();
    }

    #region Helpers

    private TimeSpan GetDuration()
    {
        return new TimeSpan(
            hourPicker.SelectedIndex,
            minutePicker.SelectedIndex,
            secondPicker.SelectedIndex);
    }

    private void UpdateWheelsFromRemaining()
    {
        hourPicker.SelectedIndex = _remaining.Hours;
        minutePicker.SelectedIndex = _remaining.Minutes;
        secondPicker.SelectedIndex = _remaining.Seconds;
    }

    private void SetSwipeEnabled(bool value)
    {
        hourPicker.IsSwipeEnabled = value;
        minutePicker.IsSwipeEnabled = value;
        secondPicker.IsSwipeEnabled = value;
    }

    private void UpdateButtons()
    {
        startButton.IsEnabled = !_isRunning && GetDuration() > TimeSpan.Zero;
        pauseButton.IsEnabled = _isRunning;
        resetButton.IsEnabled = !_isRunning && GetDuration() > TimeSpan.Zero;
    }

    #endregion

    private void OnStart(object sender, EventArgs e)
    {
        var duration = GetDuration();
        if (_isRunning || duration <= TimeSpan.Zero)
            return;

        _remaining = duration;
        _isRunning = true;
        _timer.Start();

        SetSwipeEnabled(false);
        UpdateButtons();
    }

    private void OnPaused(object sender, EventArgs e)
    {
        if (!_isRunning)
            return;

        _timer.Stop();
        _isRunning = false;

        SetSwipeEnabled(true);
        UpdateButtons();
    }

    private void OnReset(object sender, EventArgs e)
    {
        StopInternal();
        _remaining = TimeSpan.Zero;
        UpdateWheelsFromRemaining();
    }
}