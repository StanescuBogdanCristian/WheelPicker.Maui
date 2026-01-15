using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace WheelPicker.Maui.Sample.ViewModels
{
    public partial class SamplesPageViewModel : ObservableObject
    {
        #region Timer Properties

        private readonly IDispatcherTimer _timer;

        public IList<int> HoursItems { get; }
        public IList<int> MinutesItems { get; }
        public IList<int> SecondsItems { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Duration))]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
        private int hours;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Duration))]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
        private int minutes;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Duration))]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
        private int seconds;

        [ObservableProperty]
        private TimeSpan remaining;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotRunning))]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
        private bool isRunning;

        public bool IsNotRunning => !IsRunning;
        public TimeSpan Duration => new(Hours, Minutes, Seconds);

        private bool CanStart() => !IsRunning && Duration > TimeSpan.Zero;
        private bool CanPause() => IsRunning;
        private bool CanReset() => !IsRunning && Duration > TimeSpan.Zero;

        #endregion

        #region DatePicker Properties

        public ObservableCollection<int> Days { get; } = new();
        public ObservableCollection<DateTime> Months { get; } = new();
        public ObservableCollection<int> Years { get; } = new();

        private bool _suppressUpdate;

        [ObservableProperty]
        private bool isDayAnimated;

        [ObservableProperty]
        private int selectedDay;

        [ObservableProperty]
        private DateTime selectedMonth;

        [ObservableProperty]
        private int selectedYear;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(TodayCommand))]
        private DateTime selectedDate;

        private bool CanSetToday() => SelectedDate != DateTime.Today;

        #endregion

        public SamplesPageViewModel()
        {
            #region Timer

            HoursItems = Enumerable.Range(0, 24).ToList();
            MinutesItems = Enumerable.Range(0, 60).ToList();
            SecondsItems = MinutesItems; // same list

            Remaining = TimeSpan.Zero;

            _timer = Application.Current!.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += OnTick;

            #endregion

            #region DatePicker

            InitYears(1900, 2100);
            InitMonths();

            _suppressUpdate = true;

            // Initial date
            SelectedDate = DateTime.Today;

            SelectedYear = SelectedDate.Year;
            SelectedMonth = Months.First(m => m.Month == SelectedDate.Month);

            RebuildDaysForMonthYear();
            SelectedDay = SelectedDate.Day;

            _suppressUpdate = false;

            #endregion
        }

        #region Timer Methods

        private void OnTick(object? sender, EventArgs e)
        {
            if (Remaining <= TimeSpan.Zero)
            {
                _timer.Stop();
                IsRunning = false;
                Remaining = TimeSpan.Zero;

                // TODO: play sound / haptic here
                return;
            }

            Remaining -= TimeSpan.FromSeconds(1);

            UpdateWheelsToRemainingTime();
        }

        [RelayCommand(CanExecute = nameof(CanStart))]
        private void Start()
        {
            if (Duration <= TimeSpan.Zero)
                return;

            Remaining = Duration;
            IsRunning = true;
            _timer.Start();
        }

        [RelayCommand(CanExecute = nameof(CanPause))]
        private void Pause()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            _timer.Stop();
        }

        [RelayCommand(CanExecute = nameof(CanReset))]
        private void Reset()
        {
            _timer.Stop();
            IsRunning = false;
            Remaining = TimeSpan.Zero;

            UpdateWheelsToRemainingTime();
        }

        private void UpdateWheelsToRemainingTime()
        {
            Hours = Remaining.Hours;
            Minutes = Remaining.Minutes;
            Seconds = Remaining.Seconds;
        }

        #endregion

        #region DatePicker Methods

        private void InitYears(int startYear, int endYear)
        {
            Years.Clear();
            for (int y = startYear; y <= endYear; y++)
            {
                Years.Add(y);
            }
        }

        private void InitMonths()
        {
            Months.Clear();

            var months = Enumerable.Range(1, 12)
                                   .Select(month => new DateTime(DateTime.Now.Year, month, 1));

            foreach (var month in months)
            {
                Months.Add(month);
            }
        }

        partial void OnSelectedDayChanged(int oldValue, int newValue)
        {
            if (_suppressUpdate) return;
            UpdateDateFromWheels();
        }

        partial void OnSelectedMonthChanged(DateTime oldValue, DateTime newValue)
        {
            if (_suppressUpdate) return;
            RebuildDaysForMonthYear();
            UpdateDateFromWheels();
        }

        partial void OnSelectedYearChanged(int oldValue, int newValue)
        {
            if (_suppressUpdate) return;

            RebuildDaysForMonthYear();
            UpdateDateFromWheels();
        }

        partial void OnSelectedDateChanged(DateTime oldValue, DateTime newValue)
        {
            // If someone sets SelectedDate from outside, sync wheels
            if (_suppressUpdate) return;

            _suppressUpdate = true;

            SelectedYear = newValue.Year;

            SelectedMonth = Months.FirstOrDefault(m => m.Month == newValue.Month);

            RebuildDaysForMonthYear();
            SelectedDay = newValue.Day;

            IsDayAnimated = true;

            _suppressUpdate = false;
        }

        private void RebuildDaysForMonthYear()
        {
            int maxDay = DateTime.DaysInMonth(SelectedYear, SelectedMonth.Month);

            if (SelectedDay > maxDay)
            {
                IsDayAnimated = false;
                SelectedDay = maxDay;
            }

            while (Days.Count > maxDay)
                Days.RemoveAt(Days.Count - 1);

            for (int d = Days.Count + 1; d <= maxDay; d++)
                Days.Add(d);

            IsDayAnimated = true;
        }

        private void UpdateDateFromWheels()
        {
            _suppressUpdate = true;

            int maxDay = DateTime.DaysInMonth(SelectedYear, SelectedMonth.Month);
            int day = SelectedDay > maxDay ? maxDay : SelectedDay;

            if (day != SelectedDay)
                SelectedDay = day;

            SelectedDate = new DateTime(SelectedYear, SelectedMonth.Month, day);

            _suppressUpdate = false;
        }

        [RelayCommand(CanExecute = nameof(CanSetToday))]
        private void Today()
        {
            SelectedDate = DateTime.Today;
        }

        #endregion
    }
}
