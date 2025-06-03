var builder = WebApplication.CreateBuilder(args);

// Lendo as configurações da API Key e do Assistant ID
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new Exception("OpenAI ApiKey não encontrada nas configurações");

var assistantId = builder.Configuration["OpenAI:AssistantId"]
    ?? throw new Exception("AssistantId não encontrado nas configurações");

// Registrando HttpClient para OpenAI com base na API Key e cabeçalho beta
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiApiKey}");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");
});

// Registrando o serviço com o HttpClient criado e Assistant ID
builder.Services.AddScoped(provider =>
{
    var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("OpenAI");
    return new OpenAIThreadService(httpClient, assistantId);
});

// Configurando CORS para aceitar requisições do front (exemplo com Angular)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200") // Atualize para a URL do seu front-end
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Logging.AddConsole();

var app = builder.Build();

// Usar CORS com a política configurada
app.UseCors("AllowAngularApp");

app.MapControllers();

app.Run();