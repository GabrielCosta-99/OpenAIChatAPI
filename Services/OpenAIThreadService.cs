using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class OpenAIThreadService
{
    private readonly HttpClient _httpClient;
    private readonly string _assistantId;

    public OpenAIThreadService(HttpClient httpClient, string assistantId)
    {
        _httpClient = httpClient;
        _assistantId = assistantId ?? throw new Exception("AssistantId não pode ser nulo");
    }

    public async Task<string> GetResponseFromThreadAsync(string pergunta)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            // 1. Criar thread com a mensagem inicial
            var threadPayload = new
            {
                messages = new[]
                {
                    new { role = "user", content = pergunta }
                }
            };

            var threadResponse = await _httpClient.PostAsync(
                "/v1/threads",
                new StringContent(
                    JsonSerializer.Serialize(threadPayload),
                    Encoding.UTF8,
                    "application/json"
                )
            );

            threadResponse.EnsureSuccessStatusCode();

            var threadJson = await threadResponse.Content.ReadAsStringAsync();

            Console.WriteLine("Thread response JSON:");
            Console.WriteLine(threadJson);

            var threadData = JsonSerializer.Deserialize<ThreadResponse>(threadJson, options);

            if (threadData?.id == null)
                throw new Exception("Falha ao criar thread na API.");

            // 2. Criar uma run para processar a thread
            var runPayload = new
            {
                assistant_id = _assistantId
            };

            var runResponse = await _httpClient.PostAsync(
                $"/v1/threads/{threadData.id}/runs",
                new StringContent(
                    JsonSerializer.Serialize(runPayload),
                    Encoding.UTF8,
                    "application/json"
                )
            );

            runResponse.EnsureSuccessStatusCode();

            var runJson = await runResponse.Content.ReadAsStringAsync();

            Console.WriteLine("Run response JSON:");
            Console.WriteLine(runJson);

            var runData = JsonSerializer.Deserialize<RunResponse>(runJson, options);

            if (runData?.id == null)
                throw new Exception("Falha ao criar run para a thread.");

            // 3. Loop de polling para esperar a run finalizar
            bool runCompleted = false;
            int maxRetries = 120; // Dobrar o número de tentativas
            int delayMs = 1000;   // Intervalo de 1 segundo entre tentativas

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var runStatusResponse = await _httpClient.GetAsync($"/v1/threads/{threadData.id}/runs/{runData.id}");
                    runStatusResponse.EnsureSuccessStatusCode();

                    var runStatusJson = await runStatusResponse.Content.ReadAsStringAsync();
                    var runStatusData = JsonSerializer.Deserialize<RunStatusResponse>(runStatusJson, options);

                    Console.WriteLine($"Polling tentativa {i + 1}/{maxRetries} - Status atual: {runStatusData?.status ?? "desconhecido"}");

                    if (runStatusData?.status == "succeeded" || runStatusData?.status == "completed")
                    {
                        runCompleted = true;
                        Console.WriteLine("Run finalizada com sucesso.");
                        break;
                    }
                    else if (runStatusData?.status == "failed")
                    {
                        throw new Exception("A execução da run falhou antes do tempo limite.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao verificar status da run (tentativa {i + 1}): {ex.Message}");
                }

                await Task.Delay(delayMs);
            }

            if (!runCompleted)
            {
                throw new Exception("Tempo esgotado aguardando a finalização da run.");
            }

            // 4. Obter mensagens da thread
            try
            {
                var messagesResponse = await _httpClient.GetAsync($"/v1/threads/{threadData.id}/messages");
                messagesResponse.EnsureSuccessStatusCode();

                var messagesJson = await messagesResponse.Content.ReadAsStringAsync();
                Console.WriteLine("Messages response JSON:");
                Console.WriteLine(messagesJson);

                var messagesData = JsonSerializer.Deserialize<MessageListResponse>(messagesJson, options);

                // Extrair mensagem do assistente
                var assistantMessage = messagesData?.data?
                    .Where(m => m.role == "assistant")
                    .SelectMany(m => m.content ?? new List<MessageContent>())
                    .FirstOrDefault(c => c.type == "text")
                    ?.text?.value;

                return assistantMessage ?? "Nenhuma resposta encontrada.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter mensagens da thread: {ex.Message}");
                throw; // Relança a exceção para manipulação no nível superior
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro geral no processamento da thread: {ex.Message}");
            throw;
        }
    }

    // Classes para desserialização
    public class ThreadResponse
    {
        public string? id { get; set; }
    }

    public class RunResponse
    {
        public string? id { get; set; }
    }

    public class RunStatusResponse
    {
        public string? id { get; set; }
        public string? status { get; set; }  // "running", "succeeded", "failed", "completed", etc.
    }

    public class MessageListResponse
    {
        public List<Message>? data { get; set; }
    }

    public class Message
    {
        public string? role { get; set; }
        public List<MessageContent>? content { get; set; }
    }

    public class MessageContent
    {
        public string? type { get; set; }
        public MessageText? text { get; set; }
    }

    public class MessageText
    {
        public string? value { get; set; }
    }
}
