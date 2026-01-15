using CommunityToolkit.Mvvm.ComponentModel;

namespace WheelPicker.Maui.Sample.ViewModels
{
    public partial class PropertiesPageViewModel : ObservableObject
    {
        public IList<int> Years { get; }

        public IList<int> YearIndexes { get; }

        [ObservableProperty]
        int selectedYear;

        [ObservableProperty]
        int selectedYearIndex;

        public PropertiesPageViewModel()
        {
            Years = Enumerable.Range(1900, 201).ToList();
            SelectedYear = DateTime.Now.Year;

            YearIndexes = Enumerable.Range(0, Years.Count).ToList();
            SelectedYearIndex = Years.IndexOf(DateTime.Now.Year);
        }
    }
}
