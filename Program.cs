using Microsoft.Extensions.Options;
using OpenAI;
using Qdrant.Client;
using RagApi.Options;
using RagApi.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOptions<OpenAiOptions>()
    .Bind(builder.Configuration.GetSection(OpenAiOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<QdrantOptions>()
    .Bind(builder.Configuration.GetSection(QdrantOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<RagOptions>()
    .Bind(builder.Configuration.GetSection(RagOptions.Section));


// Add services to the container.

builder.Services.AddSingleton(sp =>
    new OpenAIClient(sp.GetRequiredService<IOptions<OpenAiOptions>>().Value.ApiKey));
builder.Services.AddSingleton(sp =>
{
    var q = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
    var host = Uri.TryCreate(q.Host, UriKind.Absolute, out var uri) ? uri.Host : q.Host;
    return new QdrantClient(host, q.Port, q.UseHttps, q.ApiKey);
});

builder.Services.AddSingleton<RecursiveTextSplitter>();
builder.Services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<VectorStoreService>();
builder.Services.AddScoped<IngestionService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<EvaluationService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "LangVector RAG API", Version = "v1" }));


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LangVector RAG API v1");
    c.RoutePrefix = string.Empty;
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
