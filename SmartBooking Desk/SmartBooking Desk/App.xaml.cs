using System.Windows;

namespace SmartBooking_Desk
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            base.OnStartup(e);

            var loginWindow = new LoginWindow();
            var loginResult = loginWindow.ShowDialog();

            if (loginResult == true && loginWindow.AuthResult is not null)
            {
                var mainWindow = new MainWindow(loginWindow.AuthResult);
                MainWindow = mainWindow;

                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
                return;
            }

            Shutdown();
        }
    }
}