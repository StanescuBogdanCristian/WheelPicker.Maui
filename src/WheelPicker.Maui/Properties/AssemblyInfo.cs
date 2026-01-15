using XmlnsPrefixAttribute = Microsoft.Maui.Controls.XmlnsPrefixAttribute;

[assembly: XmlnsDefinition("http://schemas.sbc.com/maui/wheelpicker", "WheelPicker.Maui")]
[assembly: XmlnsPrefix("http://schemas.sbc.com/maui/wheelpicker", "wheel")]

#if ANDROID
[assembly: Android.App.UsesPermission(Android.Manifest.Permission.Vibrate)]
#endif