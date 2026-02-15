namespace Sample.Views;

public partial class VisualStatesPage : ContentPage
{
    public VisualStatesPage()
    {
        InitializeComponent();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        wheelPicker.CancelAllAnimations();
    }
}