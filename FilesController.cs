using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace File.Controllers
{
    // API-контроллер 
    [ApiController]
    [Route("{**filepath}")] // filepath — это универсальный шаблон маршрута для обработки вложенных путей
    public class FilesController : ControllerBase
    {
        // Поля для хранения корневой папки файлов
        private readonly string storageRoot;
        private readonly string logFile;
        private readonly ILogger<FilesController> logger;

        // Конструктор
        public FilesController(IConfiguration config, ILogger<FilesController> logger)
        {
            // Определение пути к хранилищу
            var path = config["FileStorage"] ?? Environment.GetEnvironmentVariable("STORAGE_PATH") ?? "FileStorage";
            storageRoot = Path.Combine(Directory.GetCurrentDirectory(), path);
            logFile = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "log.txt");

            // Создание директорий, если они не существуют
            Directory.CreateDirectory(storageRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);

            this.logger = logger;
        }

        // Обработка PUT-запроса
        [HttpPut]
        public async Task<IActionResult> UploadFile(string filepath)
        {
            // Преобразование URL-пути в путь на диске
            string fullPath = Path.Combine(storageRoot, filepath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                // Копирование содержимого запроса в файл
                using (var fileStream = new FileStream(fullPath, FileMode.Create))
                {
                    await Request.Body.CopyToAsync(fileStream);
                }

                Log("PUT", filepath);
                return Ok("Файл загружен");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при загрузке файла");
                return StatusCode(500, "Ошибка сервера");
            }
        }

        // Обработка GET-запроса
        [HttpGet]
        public IActionResult GetFileOrDirectory(string filepath)
        {
            string fullPath = Path.Combine(storageRoot, filepath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            // Если файл существует — отдать файл
            if (System.IO.File.Exists(fullPath))
            {
                Log("GET", filepath);
                return PhysicalFile(fullPath, "application/octet-stream");
            }
            // Если это директория — отдать список файлов
            else if (Directory.Exists(fullPath))
            {
                var files = Directory.GetFiles(fullPath).Select(Path.GetFileName).ToArray();
                Log("GET-DIR", filepath);
                return Ok(JsonSerializer.Serialize(files));
            }

            // Если ничего не найдено — 404
            return NotFound();
        }

        // Обработка HEAD-запроса
        [HttpHead]
        public IActionResult GetFileInfo(string filepath)
        {
            string fullPath = Path.Combine(storageRoot, filepath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            // Если файл не найден — 404
            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            // Получение информации о файле и добавление в заголовки ответа
            var fileInfo = new FileInfo(fullPath);
            Response.Headers["File-Size"] = fileInfo.Length.ToString();
            Response.Headers["Last-Modified"] = fileInfo.LastWriteTimeUtc.ToString("R");

            Log("HEAD", filepath);
            return Ok();
        }

        // Обработка DELETE-запроса
        [HttpDelete]
        public IActionResult DeleteFileOrDirectory(string filepath)
        {
            string fullPath = Path.Combine(storageRoot, filepath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            // Удаление файла
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
                Log("DELETE-FILE", filepath);
                return Ok("Файл удалён");
            }
            // Удаление директории рекурсивно
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
                Log("DELETE-DIR", filepath);
                return Ok("Каталог удалён");
            }

            // Если не найдено — 404
            return NotFound();
        }

        // Метод логирования действий
        private void Log(string method, string path)
        {
            string logEntry = $"[{DateTime.UtcNow:u}] {method} {path}";
            System.IO.File.AppendAllText(logFile, logEntry + Environment.NewLine);
            logger.LogInformation(logEntry);
        }
    }
}
