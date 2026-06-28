using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;
using WpfApp22;

namespace WpfApp22
{
    public partial class App : Application
{
    // Статическое свойство, чтобы можно было читать из MainWindow
    public static AppSettings? Settings { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        Settings = config.Get<AppSettings>();

        if (Settings?.Security?.AdminPassword == null)
        {
            throw new InvalidOperationException("Пароль администратора не найден в appsettings.json");
        }
    }
}
}
