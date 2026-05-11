using Microsoft.Extensions.DependencyInjection;

namespace BoardGames;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var navigationPage = new NavigationPage(new StartPage()) // первой должна открыться StartPage
        {
            BarBackgroundColor = Colors.SaddleBrown,
            BarTextColor = Colors.White
        };

        return new Window(navigationPage);
    }

}
