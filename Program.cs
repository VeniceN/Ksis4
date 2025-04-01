using System.Net;
using System.Net.Sockets;
using System.Text;

// Запуск прокси-сервера
class ProxyServer
{
    private static readonly int ProxyPort = 8080;

    static async Task Main()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, ProxyPort);
        listener.Start();
        Console.WriteLine($"[*] Прокси-сервер запущен на порту {ProxyPort}");

        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleClient(client));
        }
    }

    // Обработка клиентского запроса
    private static async void HandleClient(TcpClient client)
    {
        try
        {
            using (NetworkStream clientStream = client.GetStream())
            using (StreamReader reader = new StreamReader(clientStream, Encoding.ASCII, false, 8192, true))
            using (StreamWriter writer = new StreamWriter(clientStream, Encoding.ASCII) { AutoFlush = true })
            {
                string requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(requestLine)) return;

                string[] requestParts = requestLine.Split(' ');
                if (requestParts.Length < 3) return;

                string method = requestParts[0];
                string fullUrl = requestParts[1];
                string httpVersion = requestParts[2];

                // Если URL не начинается с http:// или https://, добавляем http://
                if (!fullUrl.StartsWith("http://") && !fullUrl.StartsWith("https://"))
                {
                    fullUrl = "http://" + fullUrl;
                }

                if (!Uri.TryCreate(fullUrl, UriKind.Absolute, out Uri targetUri)) return;

                string host = targetUri.Host;
                int port = targetUri.Port == -1 ? (targetUri.Scheme == "https" ? 443 : 80) : targetUri.Port;
                string path = targetUri.PathAndQuery;

                // Проверка на пустой host
                if (string.IsNullOrEmpty(host))
                {
                    Console.WriteLine("[Ошибка] Пустой Host в запросе.");
                    return;
                }

                Console.WriteLine($"[*] Перенаправление запроса: {fullUrl}");

                using (TcpClient serverClient = new TcpClient())
                {
                    await serverClient.ConnectAsync(host, port);
                    using (NetworkStream serverStream = serverClient.GetStream())
                    using (StreamWriter serverWriter = new StreamWriter(serverStream, Encoding.ASCII) { AutoFlush = true })
                    using (StreamReader serverReader = new StreamReader(serverStream, Encoding.ASCII, false, 8192, true))
                    {
                        // Отправляем HTTP-запрос серверу
                        await serverWriter.WriteLineAsync($"{method} {path} {httpVersion}");
                        string headerLine;
                        while (!string.IsNullOrWhiteSpace(headerLine = await reader.ReadLineAsync()))
                        {
                            await serverWriter.WriteLineAsync(headerLine);
                        }
                        await serverWriter.WriteLineAsync();

                        // Читаем ответ от сервера
                        string statusLine = await serverReader.ReadLineAsync();
                        Console.WriteLine($"[*] Ответ от сервера: {statusLine}");

                        await writer.WriteLineAsync(statusLine);
                        while (!string.IsNullOrWhiteSpace(headerLine = await serverReader.ReadLineAsync()))
                        {
                            await writer.WriteLineAsync(headerLine);
                        }
                        await writer.WriteLineAsync();

                        // Передаём тело ответа клиенту без разрыва потока
                        await serverStream.CopyToAsync(clientStream);
                    }
                }
            }
        }
        catch (IOException)
        {
            Console.WriteLine("[!] Соединение разорвано сервером.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ошибка] {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }
}
