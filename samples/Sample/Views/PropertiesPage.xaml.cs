namespace Sample.Views;

public partial class PropertiesPage : ContentPage
{
    public PropertiesPage()
    {
        InitializeComponent();

        // hack for slider binding to work properly
        visibleItemsCountSlider.Value = wheelPicker.VisibleItemsCount;
        edgeItemTiltAngleSlider.Value = wheelPicker.EdgeItemTiltAngle;
        flingVelocitySlider.Value = wheelPicker.FlingVelocityThreshold;
        inertiaMinVelocitySlider.Value = wheelPicker.InertiaMinVelocity;
        inertiaDecelerationSlider.Value = wheelPicker.InertiaDeceleration;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        wheelPicker.CancelInertia();
    }

    private void OnVisibleItemsCountSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        var value = (int)Math.Round(e.NewValue);

        if (wheelPicker?.VisibleItemsCount != value)
            wheelPicker?.VisibleItemsCount = value;
    }

    private void OnEdgeItemTiltAngleSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        var value = e.NewValue;

        if (wheelPicker?.EdgeItemTiltAngle != value)
            wheelPicker?.EdgeItemTiltAngle = value;
    }

    private void OnFlingVelocitySliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        var value = e.NewValue;

        if (wheelPicker?.FlingVelocityThreshold != value)
            wheelPicker?.FlingVelocityThreshold = value;
    }

    private void OnInertiaMinVelocityValueChanged(object sender, ValueChangedEventArgs e)
    {
        var value = e.NewValue;
        if (wheelPicker?.InertiaMinVelocity != value)
            wheelPicker?.InertiaMinVelocity = value;
    }

    private void OnInertiaDecelerationValueChanged(object sender, ValueChangedEventArgs e)
    {
        var value = e.NewValue;
        if (wheelPicker?.InertiaDeceleration != value)
            wheelPicker?.InertiaDeceleration = value;
    }
}