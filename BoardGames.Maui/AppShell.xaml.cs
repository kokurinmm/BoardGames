
// AppShell не используется в этой версии программы

namespace BoardGames;

public partial class AppShell : Shell
{
    public AppShell()
    {

        InitializeComponent();

        // регистрация страниц, к которым происходит переход через GoToAsync
        Routing.RegisterRoute(nameof(GamePage), typeof(GamePage));
        Routing.RegisterRoute(nameof(HelpPage), typeof(HelpPage));

    }

}
