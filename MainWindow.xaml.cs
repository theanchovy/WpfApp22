using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WpfApp22
{
    public partial class MainWindow : Window
    {
        private bool _isTitlePlaceholder = true;
        private bool _isDescriptionPlaceholder = true;
        private Event? _selectedEvent; 
        private DispatcherTimer _notificationTimer;
        private bool _isShowingNotification = false;
        private HashSet<int> _shownNotificationIds = new HashSet<int>();
        private string _lastSearchTerm = string.Empty;
        private DateTime? _lastDateFrom = null;
        private DateTime? _lastDateTo = null;
        private readonly ApiService _apiService;
        private bool _isAuthorized = false;

        public MainWindow()
        {
            InitializeComponent();

            _apiService = new ApiService();

            TitleTextBox.GotFocus += (s, e) =>
            {
                if (_isTitlePlaceholder)
                {
                    TitleTextBox.Text = string.Empty;
                    TitleTextBox.Foreground = Brushes.Black;
                    _isTitlePlaceholder = false;
                }
            };

            TitleTextBox.LostFocus += (s, e) => UpdatePlaceholderVisibility();

            DescriptionTextBox.GotFocus += (s, e) =>
            {
                if (_isDescriptionPlaceholder)
                {
                    DescriptionTextBox.Text = string.Empty;
                    DescriptionTextBox.Foreground = Brushes.Black;
                    _isDescriptionPlaceholder = false;
                }
            };

            DescriptionTextBox.LostFocus += (s, e) => UpdateDescriptionPlaceholderVisibility();

            // Инициализация placeholder’ов
            UpdatePlaceholderVisibility();
            UpdateDescriptionPlaceholderVisibility();

            // Инициализация сервисов
            _apiService = new ApiService();

            // Настройка таймера уведомлений
            _notificationTimer = new DispatcherTimer();
            _notificationTimer.Interval = TimeSpan.FromSeconds(5);
            _notificationTimer.Tick += CheckForNotifications;
            _notificationTimer.Start();

            // Инициализация часов (0–23)
            for (int i = 0; i < 24; i++)
            {
                HourComboBox.Items.Add(i.ToString("D2"));
            }
            HourComboBox.SelectedIndex = DateTime.Now.Hour;

            // Инициализация минут (0–59)
            for (int i = 0; i < 60; i += 1) // С шагом 1
            {
                MinuteComboBox.Items.Add(i.ToString("D2"));
            }
            MinuteComboBox.SelectedIndex = (DateTime.Now.Minute / 1);

            LoadUpcomingEventsAsync();
        }

        // Асинхронный метод загрузки данных
        private async Task LoadUpcomingEventsAsync()
        {
            try
            {
                // Явно запрашиваем ближайшие события через отдельный метод API
                var upcomingEvents = await _apiService.GetUpcomingEventsAsync();
                EventsList.ItemsSource = upcomingEvents;

                // Обновляем кэш уже показанных уведомлений
                _shownNotificationIds.Clear();
                foreach (var @event in upcomingEvents)
                {
                    if (@event.IsNotificationShown)
                    {
                        _shownNotificationIds.Add(@event.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки ближайших событий: {ex.Message}");
            }
        }

        // Метод проверки уведомлений
        private async void CheckForNotifications(object sender, EventArgs e)
        {
            if (_isShowingNotification)
                return;

            try
            {
                var upcomingEvents = await _apiService.GetUpcomingEventsAsync();
                var eventsToNotify = upcomingEvents.Where(e =>
                    !e.IsNotificationShown &&
                    !_shownNotificationIds.Contains(e.Id) &&
                    (
                        // Уведомление за 24 часа (сутки)
                        (e.EventDate - DateTime.Now).TotalHours <= 24 &&
                        (e.EventDate - DateTime.Now).TotalHours > 0 ||
                        // Уведомление за 5 минут
                        (e.EventDate - DateTime.Now).TotalMinutes <= 5 &&
                        (e.EventDate - DateTime.Now).TotalMinutes > 0
                    )).ToList();

                if (eventsToNotify.Count == 0)
                    return;

                foreach (var @event in eventsToNotify)
                {
                    _isShowingNotification = true;

                    // Определяем тип уведомления
                    string notificationType;
                    if ((@event.EventDate - DateTime.Now).TotalHours <= 24)
                    {
                        notificationType = ""; //за 24 часа
                    }
                    else
                    {
                        notificationType = ""; //за 5 минут
                    }

                    MessageBox.Show(
                        $"Скоро событие: {@event.Title}\n" +
                        $"Время: {@event.EventDate:dd.MM.yyyy HH:mm}\n" +
                        $"Уведомление! {notificationType}",
                        "Напоминание о событии");

                    _shownNotificationIds.Add(@event.Id);
                    _isShowingNotification = false;

                    // Обновляем флаг в API
                    @event.IsNotificationShown = true;
                    await _apiService.UpdateEventAsync(@event);
                }
            }
            catch (Exception ex)
            {
                _isShowingNotification = false;
                MessageBox.Show($"Ошибка проверки уведомлений: {ex.Message}");
            }
        }

        // Обработчик кнопки «Загрузить ближайшие события»
        private async void LoadUpcomingEvents_Click(object sender, RoutedEventArgs e)
        {
            await LoadUpcomingEventsAsync();
        }

        // Показывает диалог ввода пароля. Возвращает true, если пароль верный.
        private bool PromptForPassword(Window owner)
        {
            var dialog = new PasswordDialog();
            if (dialog.ShowAndWait(owner) != true)
                return false;

            string entered = dialog.PasswordBox.Password;
            string expected = App.Settings!.Security!.AdminPassword!;

            return entered == expected;
        }

        // Проверяет авторизацию. Если сессии нет — запрашивает пароль.
        private async Task EnsureAuthorizedAsync()
        {
            if (_isAuthorized)
                return;

            if (!PromptForPassword(this))
                throw new UnauthorizedAccessException("Доступ запрещён: неверный пароль или отмена.");

            _isAuthorized = true;
        }


        // Кнопка «Добавить событие»
        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EnsureAuthorizedAsync().Wait(); // Синхронно ждём завершения проверки
                _selectedEvent = null;
                ShowEventDialog();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Неверный пароль или отмена операции.", "Доступ запрещён");
            }
        }

        // Кнопка «Редактировать»
        private void EditEvent_Click(object sender, RoutedEventArgs e)
        {
            if (!(EventsList.SelectedItem is Event selectedEvent))
            {
                MessageBox.Show("Выберите событие для редактирования");
                return;
            }

            try
            {
                EnsureAuthorizedAsync().Wait();
                _selectedEvent = selectedEvent;
                LoadEventToDialog(selectedEvent);
                ShowEventDialog();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Неверный пароль или отмена операции.", "Доступ запрещён");
            }
        }

        // Кнопка «Удалить»
        private async void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (!(EventsList.SelectedItem is Event eventToDelete))
            {
                MessageBox.Show("Выберите событие для удаления");
                return;
            }

            // Сначала подтверждение удаления
            var result = MessageBox.Show(
                $"Удалить событие '{eventToDelete.Title}'?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            // Потом проверка пароля
            try
            {
                await EnsureAuthorizedAsync(); // Асинхронно
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Неверный пароль или отмена операции.", "Доступ запрещён");
                return;
            }

            // Если дошли сюда — удаляем
            try
            {
                await _apiService.DeleteEventAsync(eventToDelete.Id);
                await LoadUpcomingEventsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}");
            }
        }

        // Показать диалоговое окно
        private void ShowEventDialog()
        {
            EventDialog.Visibility = Visibility.Visible;
            DialogTitle.Text = "Добавление события";

            // Очистка и инициализация полей
            TitleTextBox.Text = string.Empty;
            DatePicker.SelectedDate = DateTime.Now; // Устанавливаем текущую дату по умолчанию
            IsHolidayCheckBox.IsChecked = false;
            DescriptionTextBox.Text = string.Empty;

            // Обновляем видимость placeholder'ов после очистки полей
            _isTitlePlaceholder = true;
            _isDescriptionPlaceholder = true;
            UpdatePlaceholderVisibility();
            UpdateDescriptionPlaceholderVisibility();
        }

        // Скрыть диалоговое окно
        private void CancelDialog_Click(object sender, RoutedEventArgs e)
        {
            HideEventDialog();
        }

        // Загрузить данные события в диалоговое окно (для редактирования)
        private void LoadEventToDialog(Event eventItem)
        {
            DialogTitle.Text = "Редактирование события";
            TitleTextBox.Text = eventItem.Title;
            DatePicker.SelectedDate = eventItem.EventDate; // Программно устанавливаем дату из события
            IsHolidayCheckBox.IsChecked = eventItem.Annual;
            DescriptionTextBox.Text = eventItem.Description ?? string.Empty;

            // Обновляем видимость placeholder'ов при загрузке данных для редактирования
            if (!string.IsNullOrWhiteSpace(eventItem.Title))
            {
                _isTitlePlaceholder = false;
            }
            else
            {
                _isTitlePlaceholder = true;
            }

            if (!string.IsNullOrWhiteSpace(eventItem.Description))
            {
                _isDescriptionPlaceholder = false;
            }
            else
            {
                _isDescriptionPlaceholder = true;
            }
            UpdatePlaceholderVisibility();
            UpdateDescriptionPlaceholderVisibility();
        }

        // Сохранить событие (добавление/редактирование)
        private async void SaveEvent_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("Введите название события");
                return;
            }

            if (!DatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Выберите дату события");
                return;
            }

            // Собираем дату и время
            var selectedDate = DatePicker.SelectedDate.Value;
            var hour = int.Parse(HourComboBox.SelectedItem?.ToString() ?? "0");
            var minute = int.Parse(MinuteComboBox.SelectedItem?.ToString() ?? "0");

            var eventDateTime = new DateTime
            (
                selectedDate.Year,
                selectedDate.Month,
                selectedDate.Day,
                hour,
                minute,
                0
            );

            var newEvent = new Event
            {
                Title = TitleTextBox.Text,
                EventDate = eventDateTime,
                Annual = IsHolidayCheckBox.IsChecked ?? false,
                Description = DescriptionTextBox.Text
            };

            if (eventDateTime < DateTime.Now)
            {
                MessageBox.Show("Дата и время события не могут быть в прошлом");
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                if (_selectedEvent != null)
                {
                    newEvent.Id = _selectedEvent.Id;
                    await _apiService.UpdateEventAsync(newEvent);
                }
                else
                {
                    await _apiService.AddEventAsync(newEvent);
                }

                HideEventDialog();
                await LoadUpcomingEventsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void HideEventDialog()
        {
            EventDialog.Visibility = Visibility.Collapsed;

            // Очистка полей
            TitleTextBox.Text = string.Empty;
            DatePicker.SelectedDate = null;
            IsHolidayCheckBox.IsChecked = false;
            DescriptionTextBox.Text = string.Empty;

            // Обновляем видимость placeholder'ов
            UpdatePlaceholderVisibility();
            UpdateDescriptionPlaceholderVisibility();

            _selectedEvent = null;
        }
        // Обработчики событий для поля «Название»
        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePlaceholderVisibility();
        private void TitleTextBox_GotFocus(object sender, RoutedEventArgs e) => UpdatePlaceholderVisibility();
        private void TitleTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdatePlaceholderVisibility();

        // Обработчики событий для поля «Описание»
        private void DescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateDescriptionPlaceholderVisibility();
        private void DescriptionTextBox_GotFocus(object sender, RoutedEventArgs e) => UpdateDescriptionPlaceholderVisibility();
        private void DescriptionTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdateDescriptionPlaceholderVisibility();

        // Метод обновления видимости placeholder'а для «Названия события»
        private void UpdatePlaceholderVisibility()
        {
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                if (!TitleTextBox.IsFocused && _isTitlePlaceholder)
                    return; // Уже в режиме placeholder’а

                TitleTextBox.Text = "Название события";
                TitleTextBox.Foreground = Brushes.Gray;
                _isTitlePlaceholder = true;
            }
            else
            {
                if (TitleTextBox.IsFocused || !_isTitlePlaceholder)
                    return; // Уже в режиме ввода

                TitleTextBox.Foreground = Brushes.Black;
                _isTitlePlaceholder = false;
            }
        }

        // Метод обновления видимости placeholder'а для «Описания»
        private void UpdateDescriptionPlaceholderVisibility()
        {
            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                if (!DescriptionTextBox.IsFocused && _isDescriptionPlaceholder)
                    return;

                DescriptionTextBox.Text = "Введите описание события...";
                DescriptionTextBox.Foreground = Brushes.LightGray;
                _isDescriptionPlaceholder = true;
            }
            else
            {
                if (DescriptionTextBox.IsFocused || !_isDescriptionPlaceholder)
                    return;

                DescriptionTextBox.Foreground = Brushes.Black;
                _isDescriptionPlaceholder = false;
            }
        }

        /// <summary>
        /// Переопределяем метод OnClosed для корректного завершения работы таймера
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Останавливаем таймер уведомлений
            _notificationTimer?.Stop();
        }



        // Обработчик изменения текста поиска
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _lastSearchTerm = SearchTextBox.Text.Trim();
        }

        // Обработчики фокусов для placeholder’а
        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text) || SearchTextBox.Text == "Введите название или описание")
            {
                SearchTextBox.Text = string.Empty;
                SearchTextBox.Foreground = Brushes.Black;
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Введите название или описание";
                SearchTextBox.Foreground = Brushes.Gray;
            }
        }

        // Обработка нажатия кнопки «Найти»
        private async void SearchEvents_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Очистка плейсхолдера перед использованием
                string searchTerm = SearchTextBox.Text.Trim();
                if (searchTerm == "Введите название или описание" || string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = string.Empty;
                }

                DateTime? dateFrom = DateFromPicker.SelectedDate;
                DateTime? dateTo = DateToPicker.SelectedDate;

                // Валидация диапазона дат
                if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
                {
                    MessageBox.Show("Дата «от» не может быть позже даты «до»");
                    return;
                }

                _lastSearchTerm = searchTerm;
                _lastDateFrom = dateFrom;
                _lastDateTo = dateTo;

                var events = await _apiService.SearchEventsAsync(
                    _lastSearchTerm,
                    dateFrom,
                    dateTo
                );

                EventsList.ItemsSource = events;

                if (!events.Any())
                {
                    MessageBox.Show("По вашему запросу ничего не найдено");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска: {ex.Message}");
            }
        }

        // Обработка сброса поиска
        private async void ResetSearch_Click(object sender, RoutedEventArgs e)
        {
            // Очищаем поля поиска
            SearchTextBox.Text = "Введите название или описание";
            SearchTextBox.Foreground = Brushes.Gray;
            DateFromPicker.SelectedDate = null;
            DateToPicker.SelectedDate = null;

            // Сбрасываем сохранённые параметры поиска
            _lastSearchTerm = string.Empty;
            _lastDateFrom = null;
            _lastDateTo = null;

            // Полностью очищаем список событий в интерфейсе
            EventsList.ItemsSource = new List<Event>();

            // Также очищаем кэш уже показанных уведомлений
            _shownNotificationIds.Clear();

            // Показываем информационное сообщение
            MessageBox.Show("Список событий очищен. Используйте поиск или загрузите ближайшие события.");
        }
    }
}