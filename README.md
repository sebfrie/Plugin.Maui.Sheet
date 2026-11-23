![nuget.png](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/nuget.png)
# Plugin.Maui.Sheet

[![NuGet](https://img.shields.io/nuget/v/Plugin.Maui.Sheet.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.Maui.Sheet/)

Shows **native bottom sheets** in .NET MAUI 11 with **native in-sheet navigation**. Both the sheet container and its header are the platform's own controls; no MAUI overlay or MAUI buttons are drawn:

| | iOS / Mac Catalyst | Android | Windows |
|---|---|---|---|
| Container | `UISheetPresentationController` (page sheet in a `UINavigationController`) | Material `BottomSheetDialog` + `BottomSheetBehavior` | Light-dismiss WinUI `Popup`, centred like a Mac sheet |
| Header | `UINavigationBar` with `UIBarButtonItem`s | `MaterialToolbar` with menu actions | WinUI `TextBlock`s and `Button`s (accent primary) |
| Grabber | `PrefersGrabberVisible` | `BottomSheetDragHandleView` | – |
| [Navigation](#navigate-inside-a-sheet) | Push/pop in the sheet's `UINavigationController`: back chevron, edge swipe | Material shared-axis transition, toolbar back arrow, system back | Slide transition, back button, Esc |

**Contents**

- [Screenshots](#screenshots)
- [Install](#install)
- [Setup](#setup)
- [Use it in XAML](#use-it-in-xaml): [Properties](#properties), [Events](#events)
- [Use it from a view model](#use-it-from-a-view-model)
- [Navigate inside a sheet](#navigate-inside-a-sheet): [Header per view](#header-per-view), [Alerts, pickers and modal pages](#alerts-pickers-and-modal-pages-from-inside-a-sheet)
- [Platform notes](#platform-notes)
- [Sample](#sample)

## Screenshots

The [sample app](samples/Plugin.Maui.Sheet.Sample), captured on an Android emulator, an iOS simulator and Windows by the [screenshot workflow](.github/workflows/screenshots.yml). Select a screenshot for the full-size image.

| | FitContent form | Medium / Large list | Pass-through |
|---|---|---|---|
| **Android** | [![Android: FitContent form with Cancel and Save](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/android-form.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/android-form.png) | [![Android: Medium / Large list](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/android-list.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/android-list.png) | [![Android: Pass-through sheet over the page](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/android-passthrough.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/android-passthrough.png) |
| **iOS** | [![iOS: FitContent form with Cancel and Save](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/ios-form.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/ios-form.png) | [![iOS: Medium / Large list](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/ios-list.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/ios-list.png) | [![iOS: Pass-through sheet over the page](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/ios-passthrough.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/ios-passthrough.png) |

**Windows** (cropped to the sheet, centred in the window)

[![Windows: FitContent form with Cancel and Save](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/windows-form.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/windows-form.png)
[![Windows: Medium / Large list](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/windows-list.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/windows-list.png)
[![Windows: Pass-through sheet over the page](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/windows-passthrough.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/windows-passthrough.png)

**In-sheet navigation**: a non-dismissible *New entry* sheet with header toolbar items pushes a category picker and pops back with the pick.

| | Root view | Pushed | Back | Overflow menu |
|---|---|---|---|---|
| **Android** | [![Android: Root view: close, ✓ action and overflow button](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/android-nav.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/android-nav.png) | [![Android: Pushed view with a back button](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/android-nav-pushed.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/android-nav-pushed.png) | [![Android: Back at the root with the picked value](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/android-nav-picked.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/android-nav-picked.png) | [![Android: Secondary toolbar items in the overflow menu](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/android-nav-overflow.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/android-nav-overflow.png) |
| **iOS** | [![iOS: Root view: close, ✓ action and overflow button](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/ios-nav.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/ios-nav.png) | [![iOS: Pushed view with a back button](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/ios-nav-pushed.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/ios-nav-pushed.png) | [![iOS: Back at the root with the picked value](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/ios-nav-picked.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/ios-nav-picked.png) | [![iOS: Secondary toolbar items in the overflow menu](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/ios-nav-overflow.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/ios-nav-overflow.png) |

**Windows** (in-sheet navigation)

[![Windows: Root view: close, ✓ action and overflow button](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/windows-nav.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/windows-nav.png)
[![Windows: Pushed view with a back button](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/windows-nav-pushed.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/windows-nav-pushed.png)
[![Windows: Secondary toolbar items in the overflow menu](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/thumbs/windows-nav-overflow.png)](https://raw.githubusercontent.com/sebfrie/Plugin.Maui.Sheet/main/screenshots/windows-nav-overflow.png)

## Install

```bash
dotnet add package Plugin.Maui.Sheet
```

| Platform | Minimum version |
|---|---|
| iOS | 15 (custom detents need 16, subtitle needs 26) |
| Mac Catalyst | 17 (sheets show as form sheets on Mac) |
| Android | API 24 |
| Windows | 10 version 1809 (10.0.17763) |

These are the oldest OS versions the package runs on. The frameworks NuGet lists (e.g. `net11.0-android37.0`) name the platform SDK the package is compiled against, the .NET 11 default, which any `net11.0-android`/`-ios`/`-windows` app matches; they are not minimums.

**AI coding agents**: [AGENTS.md](https://github.com/sebfrie/Plugin.Maui.Sheet/blob/main/AGENTS.md) (also in the package) sums up the API, the in-sheet navigation rules and common mistakes.

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
| `IsDismissible` | `true` | Allows swipe down / tap outside / back (at the root) to dismiss. When `false`, only the close button or code (`IsOpen = false`) close the sheet. |
| `ShowGrabber` | `true` | Native drag handle (iOS, Android). |
| `IsDraggable` | `true` | Lets the user drag the sheet between detents. Android only; iOS and Windows have no public API for it. |
| `CornerRadius` | `-1` | Top corner radius (all corners on Windows); negative keeps the OS default. |
| `BackgroundColor` | `null` | Sheet background; `null` keeps the OS default. |
| `PassThroughInteraction` | `false` | The page below stays interactive and undimmed at every detent except `Large`. |
| `Detents` | `Medium, Large` | `Medium`, `Large`, `FitContent`, `Fraction:0.3`, `Height:250` (comma separated in XAML, any order). In C#: `SheetDetent.Medium`, `SheetDetent.Fraction(0.3)`, `SheetDetent.Height(250)`, … |
| `SelectedDetent` | `null` | TwoWay. The detent the sheet rests at (`null` = smallest); set it to animate. |
| `Title`, `Subtitle` | | Native header texts. The header hides when it has nothing to show. Also attached properties for [each view of the navigation stack](#header-per-view). |
| `PrimaryActionText`, `PrimaryActionCommand`, `PrimaryActionCommandParameter`, `IsPrimaryActionEnabled` | | Trailing confirm action (iOS *Done* style). |
| `SecondaryActionText`, `SecondaryActionCommand`, `SecondaryActionCommandParameter` | | Leading cancel action. |
| `ShowNativeCloseButton` | `false` | Native close (X) button at the leading edge. Always closes the sheet. |
| `ShowOverflowIcons` | `true` | Shows the icons of `Secondary` [toolbar items](#header-per-view) in the overflow menu (iOS, Mac Catalyst, Windows; Material overflow menus on Android have none). |
| `Content` | | The MAUI view shown below the header: the root of the [navigation stack](#navigate-inside-a-sheet). |
| `ContentTemplate` | | Used when `Content` is `null`: the view is created each time the sheet opens and released when it closes, so closed sheets cost nothing. |

An action without a command simply closes the sheet. With a command, the command decides (set `IsOpen = false` to close).

### Events

| Event | When |
|---|---|
| `Opened` | The sheet finished presenting. |
| `Dismissed` | The sheet closed, by code or by the user. |
| `DetentChanged` | The user moved the sheet to another detent. |
| `DismissAttempted` | The user tried to dismiss. `e.Cancel` starts as `!IsDismissible`; set it to keep a dismissible sheet open. A non-dismissible sheet stays open regardless. |
| `Popped` | The user went back a level (back button, iOS edge swipe, Android system back, Esc on Windows). `e.View` is the view that was removed. Not raised for `PopAsync`. |

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

A sheet can return a value: `ShowSheetAsync<TView, TResult>()` completes with what the sheet's view model passes to `CloseSheetAsync(result)`, or `default` when the user dismisses it.

```csharp
// In the calling view model
string? fruit = await sheets.ShowSheetAsync<FruitPickerView, string>(s => s.Detents = [SheetDetent.FitContent]);

// In FruitPickerView's view model
await sheets.CloseSheetAsync("Apple");
```

Or open a sheet without a view type and keep the `NativeSheet`, e.g. to [navigate inside it](#navigate-inside-a-sheet): `OpenAsync` completes once the sheet is presented.

```csharp
NativeSheet sheet = await sheets.OpenAsync(new NewEntryView(), s =>
{
    s.IsDismissible = false; // only the close button or code close it
    s.ShowNativeCloseButton = true;
    s.Detents = [SheetDetent.Large];
});
sheet.Dismissed += OnNewEntryClosed;
```

## Navigate inside a sheet

A sheet holds a stack of MAUI views and shows the last one. `PushAsync` shows another view in the same sheet with the platform's push transition, and the native header follows the view on top, with a back button. This works for sheets declared in XAML and for sheets from `INativeSheetService` alike.

```csharp
// In NewEntryView, the sheet's root view (its Parent is the sheet)
await ((NativeSheet)Parent).PushAsync(new CategoryPickerView());

// In CategoryPickerView, after the user picked
await ((NativeSheet)Parent).PopAsync();
```

| Member | Description |
|---|---|
| `PushAsync(view, animated = true)` | Shows `view` on top and completes when the transition finished. The view becomes a logical child of the sheet: `StaticResource`, `DynamicResource`, `AppThemeBinding` and styles resolve, and it inherits the sheet's binding context unless you set its own. |
| `PopAsync(count = 1, animated = true)` | Removes the top `count` views (never the root) with the back transition and disconnects their handlers. Does not raise `Popped`. |
| `NavigationStack` | The views, root (`Content`) first. |
| `Popped` | The user went back: back button, iOS edge swipe, Android system back, Esc on Windows. |

At the root, Android's system back and Esc on Windows try to dismiss the sheet (`DismissAttempted`). When the sheet closes, pushed views are removed and the handlers of all its views are disconnected.

### Header per view

Each view brings its own header through attached properties. The root view falls back to the sheet's own `Title` and `Subtitle`; the sheet's `PrimaryActionText` and `SecondaryActionText` only show at the root.

```xml
<ContentView xmlns:sheet="clr-namespace:Plugin.Maui.Sheet;assembly=Plugin.Maui.Sheet"
             sheet:NativeSheet.Title="New entry"
             sheet:NativeSheet.Subtitle="Draft">
    <sheet:NativeSheet.ToolbarItems>
        <ToolbarItem Text="Save" Command="{Binding SaveCommand}" AutomationId="Save">
            <ToolbarItem.IconImageSource>
                <FontImageSource Glyph="✓" />
            </ToolbarItem.IconImageSource>
        </ToolbarItem>
        <ToolbarItem Text="Share" Order="Secondary" Command="{Binding ShareCommand}" />
        <ToolbarItem Text="Delete" Order="Secondary" Command="{Binding DeleteCommand}" />
    </sheet:NativeSheet.ToolbarItems>
    <!-- … -->
</ContentView>
```

The header follows each item's `Text`, `IconImageSource` (including `FontImageSource`), `IsEnabled` and `Command.CanExecute(CommandParameter)` while it is shown; `AutomationId` becomes the native accessibility identifier (Android: content description). Toolbar items inherit their view's binding context. In C#: `NativeSheet.SetTitle(view, "…")`, `NativeSheet.GetToolbarItems(view).Add(item)`.

| | iOS / Mac Catalyst | Android | Windows |
|---|---|---|---|
| `Order="Primary"` / `Default` | Trailing `UIBarButtonItem`: icon, else text | Toolbar action: icon, else text; moves into the overflow menu when it does not fit | Header button: icon, else text |
| `Order="Secondary"` | `ellipsis.circle` button with a `UIMenu`; items show their icon | The toolbar's overflow menu (⋮), text only | `…` button with a `MenuFlyout`; items show their icon |
| Leading | Close (X) at the root with `ShowNativeCloseButton`; back chevron and edge swipe on pushed views | Close at the root; back arrow on pushed views | Close at the root; back button on pushed views |

Icons take the header's color (like template images on iOS) unless a `FontImageSource` sets its own `Color`. Set `ShowOverflowIcons="False"` on the sheet for text-only overflow menus everywhere.

### Alerts, pickers and modal pages from inside a sheet

`DisplayAlertAsync`, `DisplayActionSheetAsync`, `DatePicker`/`Picker` and modal pages (`PushModalAsync`, and so CommunityToolkit.Maui popups) opened while a sheet is up appear above the sheet ([check screenshots](screenshots/checks)). On iOS, MAUI presents modal pages from the window's root controller, which is already presenting the sheet; while a sheet is up, the plugin presents them from the sheet instead (other presentations are unchanged). On Windows, MAUI draws modal pages inside the window, below the sheet's `Popup`, so the sheet hides while a modal page opened over it is up and comes back when that page is popped.

## Platform notes

- **Android** `Detents="Large"` alone opens the sheet expanded to the full height below the status bar, without a collapsed state. With the keyboard up, the sheet resizes so the focused field stays visible.
- **Android** `BottomSheetBehavior` has three resting states, so at most three distinct detent heights are used (smallest, middle, largest).
- **Android** pass-through: the dialog window is undimmed and touches outside the sheet are re-dispatched to the activity, so lists and maps below scroll and respond to taps. Text input below the sheet does not receive the keyboard while the sheet is open.
- **Android** the drag handle is Material 3; on MAUI's default MaterialComponents theme it is themed with a Material 3 overlay automatically.
- **Android** the sheet is a dialog window, and while it has focus Android takes the status bar icon color from it. The sheet copies the activity's light/dark status bar icon setting, so icons you switched at runtime (e.g. light icons over a dark page) don't flip while a sheet is open.
- **Windows** has no bottom sheet. The sheet is shown like on Mac Catalyst: a card (max 540 wide) centred over the window in a `Popup`, over a dimmed backdrop. Clicking the backdrop or pressing Esc dismisses it (cancelable through `DismissAttempted`; on a pushed view, Esc goes back instead); unlike WinUI's built-in light dismiss it stays open when the window is resized or loses focus. Detents only choose its height (`FitContent` sizes to the content); there is no dragging or grabber. With `PassThroughInteraction` (below `Large`) there is no backdrop and the page stays usable.
- **iOS 15** has only medium/large detents; `Fraction`, `Height` and `FitContent` fall back to the nearer one.
- Nested `ScrollView`/`CollectionView` content expands the sheet when scrolled to its edge on both platforms.

## Sample

[`samples/Plugin.Maui.Sheet.Sample`](samples/Plugin.Maui.Sheet.Sample) shows a `FitContent` form with a primary action that enables once text is entered, a `Medium/Large` list with `SelectedDetent` binding, a non-dismissible pass-through sheet over a scrollable list, a sheet opened from a view model through `INativeSheetService` that returns the user's pick, and a non-dismissible *New entry* sheet (`OpenAsync`) with header toolbar items, a pushed category picker and buttons that show an alert and a modal page from inside the sheet.
