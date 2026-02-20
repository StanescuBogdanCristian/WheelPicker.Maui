![WheelPicker.Maui banner](https://raw.githubusercontent.com/StanescuBogdanCristian/WheelPicker.Maui/main/src/WheelPicker.Maui/assets/banner.jpg)

## Available on NuGet

[![NuGet](https://img.shields.io/nuget/v/S8C.WheelPicker.Maui.svg?label=NuGet)](https://www.nuget.org/packages/S8C.WheelPicker.Maui)

---

## Features

- **ItemTemplate** (custom content)
- **Default item styling** (text color, font, padding, alignment, line break, max lines, auto-scaling)
- **ItemStringFormat** for default template formatting
- **Loop** mode (infinite wheel)
- **Selection threshold band** (controls how “strict” the center selection is)
- **Visual states** for selected/non-selected items (`CurrentItem` / `DefaultItem`)
- **Curvature / 3D wheel effect** with configurable `tilt`, `scale`, `opacity`
- **Overlay view** (selection band, highlight, etc.)
- **Velocity-adaptive haptic and sound feedback** (tick + snap)
- **Mouse wheel** support for macOS and Windows
- **Parent ScrollView** support (disables parent scrolling when interacting with the wheel)
- Exposes `IsDragging`, `IsSpinning`, and computed `ItemHeight`

---

## Requirements

- .NET 9+

---

## Installation

```bash
dotnet add package S8C.WheelPicker.Maui
```

---

## Quick start (XAML)

Add the namespace:

```xml
xmlns:wp="clr-namespace:SBC.WheelPicker;assembly=SBC.WheelPicker.Maui"
or
xmlns:wp="http://schemas.sbc.com/maui/wheelpicker"
```

Use the control:

```xml
<wp:WheelPicker ItemsSource="{Binding Years}"
                SelectedItem="{Binding SelectedYear}">
...
</wp:WheelPicker>
```

---

## ItemTemplate + Visual States

The WheelPicker uses VisualStateManager to switch between:

- `CurrentItem` (selected)
- `DefaultItem` (non-selected)

These names are defined by the control constants:

```csharp
public const string DefaultItemVisualState = "DefaultItem";
public const string CurrentItemVisualState = "CurrentItem";
```

### Example

```xml
<wp:WheelPicker x:Name="WheelPicker"
                ItemsSource="{Binding Years}"
                SelectedItem="{Binding SelectedYear}">
    <wp:WheelPicker.ItemTemplate>
        <DataTemplate>
            <Label Text="{Binding .}" >
                <VisualStateManager.VisualStateGroups>
                    <VisualStateGroup x:Name="SelectionStates">
                        <VisualState x:Name="DefaultItem" />
                        <VisualState x:Name="CurrentItem">
                            <VisualState.Setters>
                                <Setter Property="TextColor"
                                        Value="DarkOrange" />
                            </VisualState.Setters>
                        </VisualState>
                    </VisualStateGroup>
                </VisualStateManager.VisualStateGroups>
            </Label>
        </DataTemplate>
    </wp:WheelPicker.ItemTemplate>
</wp:WheelPicker>
```

---

## Overlay (selection band)

Use `Overlay` to place a highlight or “selection window” on top of the wheel.

```xml
<wp:WheelPicker x:Name="WheelPicker"
                ItemsSource="{Binding Years}"
                SelectedItem="{Binding SelectedYear}">

.....

    <wp:WheelPicker.Overlay>
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="*" />
                <RowDefinition Height="{Binding Source={x:Reference WheelPicker}, Path=ItemHeight}" />
                <RowDefinition Height="*" />
            </Grid.RowDefinitions>

            <Border StrokeThickness="0"
                    BackgroundColor="#1affffff"
                    Grid.Row="1">
                <Border.StrokeShape>
                    <RoundRectangle CornerRadius="8" />
                </Border.StrokeShape>
                <Border.Shadow>
                    <Shadow Brush="Black"
                            Offset="0,0"
                            Radius="1"
                            Opacity="0.8" />
                </Border.Shadow>
            </Border>

        </Grid>
    </wp:WheelPicker.Overlay>
</wp:WheelPicker>
```

---

## Public API

### Methods

```csharp
// Scrolls the wheel so that the item at the specified "index" becomes centered and selected,
// optionally animating the transition.
wheelPicker.SpinTo(index, animated: true);

// Scrolls the wheel so that the item at the specified "index" becomes centered and selected.
// This is a convenience overload that behaves the same as "SpinTo(int, bool)" with animated controlled by "IsSelectionAnimated".
wheelPicker.SpinTo(index);

// Scrolls the wheel so that the specified "item" becomes centered and selected,
// optionally animating the transition.
wheelPicker.SpinTo(item, animated: true);

// Scrolls the wheel so that the specified "item" becomes centered and selected.
// This is a convenience overload that behaves the same as "SpinTo(object?, bool)" with animated controlled by "IsSelectionAnimated".
wheelPicker.SpinTo(item);
```
---

## Selections

| Property | Type | Default | Description |
|---|---:|---:|---|
| `SelectedIndex` | int | -1 | Index of the currently selected item within `ItemsSource`. This property supports two-way data binding. |
| `SelectedItem` | object | null | Currently selected item within `ItemsSource`. This property supports two-way data binding. |
| `IsSelectionAnimated` | bool | true | A value indicating whether selection changes are animated when the selected item changes. |
| `SelectionThreshold` | double | 0.4 | Relative size of the selection band around the vertical center of the wheel. Valid range: 0.1 – 0.9. |

## Feedbacks

| Property | Type | Default | Description |
|---|---:|---:|---|
| `HapticFeedback` | bool | true | Haptic feedback (tick + snap) when selection changes. |
| `SoundFeedback` | bool | true | Play a tick sound when selection changes. |

## Interactions

| Property | Type | Default | Description |
|---|---:|---:|---|
| `Loop` | bool | true | Whether the wheel loops when the user scrolls past the first or last item. |
| `IsSwipeEnabled` | bool | true | Enable/disable drag interaction. |

## Appearance

| Property | Type | Default | Description |
|---|---:|---:|---|
| `VisibleItemsCount` | int | 5 | Number of items visible at once (odd integers from 1 to 11). |
| `CurvatureFactor` | double | 1.0 | Intensity of the wheel's curvature / 3D bend effect. Valid range: 0.0 – 1.0. |
| `EdgeItemTiltAngle` | double | 70 | Maximum tilt (rotation) angle in degrees applied to edge items. Valid range: 0.1 – 90.0. |
| `EdgeItemScale` | double | 0.5 | Minimum scale applied to edge items. Valid range: 0.1 – 1.0. |
| `EdgeItemOpacity` | double | 0.1 | Minimum opacity applied to edge items. Valid range: 0.1 – 1.0. |

## Default item template

These properties apply to the default item template only (ignored when `ItemTemplate` is set).

| Property | Type | Default | Description |
|---|---:|---:|---|
| `ItemStringFormat` | string | null | String format for the default item template. |
| `ItemTextColor` | Color | theme | Text color for the default item template. |
| `ItemFontSize` | double | 14 | Font size for the default item template. |
| `ItemFontAttributes` | FontAttributes | None | Font attributes for the default item template. |
| `ItemFontFamily` | string | null | Font family for the default item template. |
| `ItemPadding` | Thickness | 0 | Padding for the default item template. |
| `ItemHorizontalTextAlignment` | TextAlignment | Center | Horizontal text alignment. |
| `ItemVerticalTextAlignment` | TextAlignment | Center | Vertical text alignment. |
| `ItemLineBreakMode` | LineBreakMode | NoWrap | Line break mode. |
| `ItemMaxLines` | int | 1 | Maximum lines for the default item template. |
| `ItemFontAutoScalingEnabled` | bool | true | Enables font auto-scaling. |

## Read-only state

| Property | Type | Description |
|---|---:|---|
| `IsDragging` | bool | True while user is dragging (OneWayToSource). |
| `IsSpinning` | bool | True while inertia/animation is active (OneWayToSource). |
| `ItemHeight` | double | Computed item height (read-only). |

## Events

| Event | Description |
|---|---|
| `SelectedIndexChanged` | Fires when `SelectedIndex` changes. |
| `SelectedItemChanged` | Fires when `SelectedItem` changes. |

## Commands

| Property | Type | Description |
|---|---|---|
| `SelectedIndexChangedCommand` | ICommand | Invoked when `SelectedIndex` changes. |
| `SelectedIndexChangedCommandParameter` | object | Optional parameter for `SelectedIndexChangedCommand`. |
| `SelectedItemChangedCommand` | ICommand | Invoked when `SelectedItem` changes. |
| `SelectedItemChangedCommandParameter` | object | Optional parameter for `SelectedItemChangedCommand`. |

