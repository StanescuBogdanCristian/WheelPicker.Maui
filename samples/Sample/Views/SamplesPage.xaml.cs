namespace Sample.Views;

public partial class SamplesPage : ContentPage
{
    public SamplesPage()
    {
        InitializeComponent();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        timer.CancelAllAnimations();
        datePicker.CancelAllAnimations();
        timePicker.CancelAllAnimations();
    }
}