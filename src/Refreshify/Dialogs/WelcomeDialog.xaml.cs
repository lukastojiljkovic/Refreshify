using Microsoft.UI.Xaml.Controls;

namespace Refreshify.Dialogs;

/// <summary>Explains what Refreshify does when it starts, until the user opts out.</summary>
public sealed partial class WelcomeDialog : ContentDialog
{
    public WelcomeDialog() => InitializeComponent();

    public bool DontShowAgain => DontShowAgainBox.IsChecked == true;
}
