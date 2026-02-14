[assembly: XmlnsDefinition("http://schemas.sbc.com/maui/wheelpicker", "SBC.WheelPicker")]

#if ANDROID
[assembly: Android.App.UsesPermission(Android.Manifest.Permission.Vibrate)]
#endif