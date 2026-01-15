using CommunityToolkit.Mvvm.ComponentModel;

namespace WheelPicker.Maui.Sample.ViewModels
{
    public partial class OverlaysPageViewModel : ObservableObject
    {
        public IList<DateTime> Months { get; }
        public IList<int> Years { get; }
        public IList<int> Numbers { get; }
        public IList<DateTime> Days { get; }

        [ObservableProperty]
        int selectedYear;

        [ObservableProperty]
        DateTime selectedMonth;

        [ObservableProperty]
        int selectedNumber;

        [ObservableProperty]
        DateTime selectedDay;

        public OverlaysPageViewModel()
        {
            var now = DateTime.Now;

            Months = Enumerable.Range(1, 12)
                              .Select(month => new DateTime(now.Year, month, 1))
                              .ToList();
            SelectedMonth = new DateTime(now.Year, now.Month, 1);

            Years = Enumerable.Range(1900, 201).ToList();
            SelectedYear = now.Year;

            Numbers = Enumerable.Range(1, 99).ToList();
            SelectedNumber = 50;


            Days = Enumerable.Range(1, DateTime.DaysInMonth(now.Year, now.Month))
                              .Select(day => new DateTime(DateTime.Now.Year, now.Month, day))
                              .ToList();
            SelectedDay = now.Date;
        }
    }
}
