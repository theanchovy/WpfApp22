using System.Windows;

namespace WpfApp22
{
    public partial class PasswordDialog : Window
    {
        public PasswordDialog()
        {
            InitializeComponent();
            // Сразу ставим фокус в поле ввода — удобно для пользователя
            PasswordBox.Focus();
        }

        public bool? ShowAndWait(Window owner, string promptTitle = "Авторизация")
        {
            Owner = owner;                  // Окно будет поверх главного окна
            Title = promptTitle;            // Меняем заголовок
            return ShowDialog();            // Блокирует до закрытия окна
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;            // Результат: «подтверждено»
            Close();
        }
    }
}