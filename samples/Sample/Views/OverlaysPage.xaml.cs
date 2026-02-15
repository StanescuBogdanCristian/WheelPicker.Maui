namespace Sample.Views;

public partial class OverlaysPage : ContentPage
{
	public OverlaysPage()
	{
		InitializeComponent();
	}

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        wheelPickeriOS.CancelAllAnimations();
        wheelPickerAndroid.CancelAllAnimations();
        wheelPickerOneUI.CancelAllAnimations();
        wheelPickerOther.CancelAllAnimations();
    }
}