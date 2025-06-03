using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

var builder = WebApplication.CreateBuilder(args);

// Configurando o cliente do Key Vault
var keyVaultUrl = "https://solimkey.vault.azure.net/"; // URL do Key Vault
var secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

// Lendo as configurações da API Key e do Assistant ID do Key Vault
var openAiApiKey = secretClient.GetSecret("SolimChaveAPI")?.Value
    ?? throw new Exception("OpenAI ApiKey não encontrada no Key Vault");

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

// Configurando CORS para aceitar requisições do front 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200") //  URL do front-end
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
