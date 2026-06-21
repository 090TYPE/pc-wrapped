using Avalonia.Controls;

namespace PcWrapped.Views;

public partial class OnboardingWindow : Window
{
    public bool CountInput { get; private set; }
    public bool Autostart { get; private set; }

    public OnboardingWindow()
    {
        InitializeComponent();
        this.FindControl<Button>("StartBtn")!.Click += (_, _) =>
        {
            CountInput = this.FindControl<CheckBox>("VanityToggle")!.IsChecked == true;
            Autostart = this.FindControl<CheckBox>("AutostartToggle")!.IsChecked == true;
            Close();
        };
    }
}
