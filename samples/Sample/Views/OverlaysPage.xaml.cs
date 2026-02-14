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
        wheelPickeriOS.CancelInertia();
        wheelPickerAndroid.CancelInertia();
        wheelPickerOneUI.CancelInertia();
        wheelPickerOther.CancelInertia();
    }
}