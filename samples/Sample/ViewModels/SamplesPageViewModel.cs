using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Sample.ViewModels
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
    }
}
