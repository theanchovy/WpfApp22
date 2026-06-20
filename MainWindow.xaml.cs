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
        private readonly ApiService _apiService = new();
        private Event? _selectedEvent; // Для редактирования
        private DispatcherTimer _notificationTimer;
        private bool _isShowingNotification = false;
        private HashSet<int> _shownNotificationIds = new HashSet<int>();

        public MainWindow()
        {
            InitializeComponent();

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
            for (int i = 0; i < 60; i += 5) // С шагом 5 для удобства
            {
                MinuteComboBox.Items.Add(i.ToString("D2"));
            }
            MinuteComboBox.SelectedIndex = (DateTime.Now.Minute / 5);

            LoadUpcomingEventsAsync();
        }

        // Асинхронный метод загрузки данных
        private async Task LoadUpcomingEventsAsync()
        {
            try
            {
                var events = await _apiService.GetUpcomingEventsAsync();
                EventsList.ItemsSource = events;

                // Инициализируем кэш: добавляем ID событий, для которых уведомление уже показано
                foreach (var @event in events)
                {
                    if (@event.IsNotificationShown)
                    {
                        _shownNotificationIds.Add(@event.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
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

        // Кнопка «Добавить событие»
        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            _selectedEvent = null;
            ShowEventDialog();
        }

        // Кнопка «Редактировать»
        private void EditEvent_Click(object sender, RoutedEventArgs e)
        {
            if (EventsList.SelectedItem is Event selectedEvent)
            {
                _selectedEvent = selectedEvent;
                LoadEventToDialog(selectedEvent);
                ShowEventDialog();
            }
            else
            {
                MessageBox.Show("Выберите событие для редактирования");
            }
        }

        // Кнопка «Удалить»
        private async void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (EventsList.SelectedItem is Event eventToDelete)
            {
                if (MessageBox.Show($"Удалить событие '{eventToDelete.Title}'?",
                    "Подтверждение удаления",
            MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _apiService.DeleteEventAsync(eventToDelete.Id);
                        await LoadUpcomingEventsAsync(); // Перезагрузка списка
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления: {ex.Message}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите событие для удаления");
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
            IsHolidayCheckBox.IsChecked = eventItem.IsHoliday;
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
                IsHoliday = IsHolidayCheckBox.IsChecked ?? false,
                Description = DescriptionTextBox.Text
            };

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

            if (eventDateTime < DateTime.Now)
            {
                MessageBox.Show("Дата и время события не могут быть в прошлом");
                return;
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
    }
}