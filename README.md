![nuget.png](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/nuget.png)
# Plugin.Maui.Sheet

`Plugin.Maui.Sheet` shows **truly native bottom sheets** in .NET MAUI 11. Both the sheet container and its header are the platform's own controls; no MAUI overlay or MAUI buttons are drawn:

| | iOS / Mac Catalyst | Android |
|---|---|---|
| Container | `UISheetPresentationController` (page sheet in a `UINavigationController`) | Material `BottomSheetDialog` + `BottomSheetBehavior` |
| Header | `UINavigationBar` with `UIBarButtonItem`s | `MaterialToolbar` with menu actions |
| Grabber | `PrefersGrabberVisible` | `BottomSheetDragHandleView` |

## Install

[![NuGet](https://img.shields.io/nuget/v/Plugin.Maui.Sheet.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.Maui.Sheet/)

```bash
dotnet add package Plugin.Maui.Sheet
```

| Platform | Minimum version |
|---|---|
| iOS | 15 (custom detents need 16, subtitle needs 26) |
| Mac Catalyst | 17 (sheets show as form sheets on Mac) |
| Android | API 24 |

Windows and Tizen have no native sheet and are not supported.

## Setup

```csharp
builder
    .UseMauiApp<App>()
    .UseNativeSheet(); // registers the handler and INativeSheetService
```

## Use it in XAML

Put a `NativeSheet` anywhere in the page's layout. It takes no space; its content appears in the native sheet while `IsOpen` is `true`.

```xml
xmlns:sheet="clr-namespace:Plugin.Maui.Sheet;assembly=Plugin.Maui.Sheet"

<sheet:NativeSheet IsOpen="{Binding IsEditing}"
                   Detents="FitContent, Large"
                   Title="New entry"
                   Subtitle="Sized to its content"
                   PrimaryActionText="Save"
                   PrimaryActionCommand="{Binding SaveCommand}"
                   IsPrimaryActionEnabled="{Binding CanSave}"
                   SecondaryActionText="Cancel">
    <VerticalStackLayout Padding="20">
        <Entry Text="{Binding Name}" />
    </VerticalStackLayout>
</sheet:NativeSheet>
```

### Properties

| Property | Default | Description |
|---|---|---|
| `IsOpen` | `false` | Opens/closes the sheet. TwoWay; set to `false` when the user dismisses it. |
| `IsDismissible` | `true` | Allows swipe down / tap outside / back to dismiss. |
| `ShowGrabber` | `true` | Native drag handle. |
| `CornerRadius` | `-1` | Top corner radius; negative keeps the OS default. |
| `PassThroughInteraction` | `false` | The page below stays interactive and undimmed at every detent except `Large`. |
| `Detents` | `Medium, Large` | `Medium`, `Large`, `FitContent`, `Fraction:0.3`, `Height:250` (comma separated in XAML, any order). In C#: `SheetDetent.Medium`, `SheetDetent.Fraction(0.3)`, `SheetDetent.Height(250)`, … |
| `SelectedDetent` | `null` | TwoWay. The detent the sheet rests at (`null` = smallest); set it to animate. |
| `Title`, `Subtitle` | | Native header texts. The header hides when it has nothing to show. |
| `PrimaryActionText`, `PrimaryActionCommand`, `PrimaryActionCommandParameter`, `IsPrimaryActionEnabled` | | Trailing confirm action (iOS *Done* style). |
| `SecondaryActionText`, `SecondaryActionCommand`, `SecondaryActionCommandParameter` | | Leading cancel action. |
| `ShowNativeCloseButton` | `false` | Native close (X) button at the leading edge. Always closes the sheet. |
| `Content` | | The MAUI view shown below the header. |

An action without a command simply closes the sheet. With a command, the command decides (set `IsOpen = false` to close).

### Events

| Event | When |
|---|---|
| `Opened` | The sheet finished presenting. |
| `Dismissed` | The sheet closed, by code or by the user. |
| `DetentChanged` | The user moved the sheet to another detent. |
| `DismissAttempted` | The user tried to dismiss. `e.Cancel` starts as `!IsDismissible`; set it to keep the sheet open or clear it to let it close. |

`FitContent` re-measures when the root of `Content` invalidates its measure. After size changes deeper inside the content, call `sheet.InvalidateDetents()`.

## Use it from a view model

```csharp
public class OrdersViewModel(INativeSheetService sheets)
{
    public async Task ShowDetailsAsync()
    {
        // OrderDetailsView is created with constructor injection; the task completes when the sheet closes.
        await sheets.ShowSheetAsync<OrderDetailsView>(sheet =>
        {
            sheet.Title = "Order";
            sheet.Detents = [SheetDetent.FitContent, SheetDetent.Large];
            sheet.ShowNativeCloseButton = true;
        });
    }

    public Task CloseAsync() => sheets.CloseSheetAsync();
}
```

## Platform notes

- **Android** `BottomSheetBehavior` has three resting states, so at most three distinct detent heights are used (smallest, middle, largest).
- **Android** pass-through: the dialog window is undimmed and touches outside the sheet are re-dispatched to the activity, so lists and maps below scroll and respond to taps. Text input below the sheet does not receive the keyboard while the sheet is open.
- **Android** the drag handle is Material 3; on MAUI's default MaterialComponents theme it is themed with a Material 3 overlay automatically.
- **iOS 15** has only medium/large detents; `Fraction`, `Height` and `FitContent` fall back to the nearer one.
- Nested `ScrollView`/`CollectionView` content expands the sheet when scrolled to its edge on both platforms.

## Sample

[`samples/Plugin.Maui.Sheet.Sample`](samples/Plugin.Maui.Sheet.Sample) shows a `FitContent` form with a primary action that enables once text is entered, a `Medium/Large` list with `SelectedDetent` binding, a non-dismissible pass-through sheet over a scrollable list, and a sheet opened from a view model through `INativeSheetService`.

## Releasing to NuGet

The [release workflow](.github/workflows/release-nuget.yml) packs and pushes when a tag like `v1.0.0` or `v1.0.0-preview1` is pushed (the tagged commit must be on `main`). Add a repository secret named `NUGET_API_KEY` with a nuget.org API key that may push `Plugin.Maui.Sheet`.
