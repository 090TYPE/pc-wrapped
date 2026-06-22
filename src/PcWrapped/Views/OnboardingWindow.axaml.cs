using Avalonia.Controls;
using PcWrapped.Localization;

namespace PcWrapped.Views;

public partial class OnboardingWindow : Window
{
    public bool CountInput { get; private set; }
    public bool Autostart { get; private set; }

    public OnboardingWindow()
    {
        InitializeComponent();
        Title = Loc.T("onb.title");
        this.FindControl<TextBlock>("PrivacyTitle")!.Text = Loc.T("onb.privacyTitle");
        this.FindControl<TextBlock>("PrivacyBody")!.Text = Loc.T("onb.privacyBody");
        this.FindControl<CheckBox>("VanityToggle")!.Content = Loc.T("onb.vanity");
        this.FindControl<CheckBox>("AutostartToggle")!.Content = Loc.T("onb.autostart");
        this.FindControl<Button>("StartBtn")!.Content = Loc.T("onb.start");
        this.FindControl<Button>("StartBtn")!.Click += (_, _) =>
        {
            CountInput = this.FindControl<CheckBox>("VanityToggle")!.IsChecked == true;
            Autostart = this.FindControl<CheckBox>("AutostartToggle")!.IsChecked == true;
            Close();
        };
    }
}
