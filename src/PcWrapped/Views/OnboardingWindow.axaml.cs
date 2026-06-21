using Avalonia.Controls;

namespace PcWrapped.Views;

public partial class OnboardingWindow : Window
{
    public OnboardingWindow()
    {
        InitializeComponent();
        this.FindControl<Button>("StartBtn")!.Click += (_, _) => Close();
    }
}
