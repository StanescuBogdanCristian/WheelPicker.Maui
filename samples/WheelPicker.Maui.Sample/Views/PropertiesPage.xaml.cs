namespace WheelPicker.Maui.Sample.Views;

public partial class PropertiesPage : ContentPage
{
    public PropertiesPage()
    {
        InitializeComponent();

        // hack for slider binding to work properly
        VisibleItemsCountSlider.Value = WheelPicker.VisibleItemsCount;
        EdgeItemTiltAngleSlider.Value = WheelPicker.EdgeItemTiltAngle;
    }

    private void OnVisibleItemsCountSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        var value = (int)Math.Round(e.NewValue);

        if (WheelPicker?.VisibleItemsCount != value)
            WheelPicker?.VisibleItemsCount = value;
    }

    private void OnEdgeItemTiltAngleSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        var value = Math.Round(e.NewValue);

        if (WheelPicker?.EdgeItemTiltAngle != value)
            WheelPicker?.EdgeItemTiltAngle = value;
    }
}