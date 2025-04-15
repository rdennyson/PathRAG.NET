using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Services;
using PathRAG.Infrastructure.Data;
using PathRAG.Core.Services.Graph;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Services.Entity;
using PathRAG.Core.Services.Query;
using PathRAG.Core;
using PathRAG.Core.Services.Cache;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Azure OpenAI Client
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>().GetSection("PathRAG");
    return new OpenAIClient(
        new Uri(config["Endpoint"]!),
        new Azure.AzureKeyCredential(config["ApiKey"]!)
    );
});

// Add DbContext
builder.Services.AddDbContext<PathRagDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Core PathRAG Services
builder.Services.AddScoped<ITextChunkService, TextChunkService>();
builder.Services.AddScoped<IDocumentExtractor, DocumentExtractor>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();

// Register Graph and Entity Services
builder.Services.AddScoped<IGraphStorageService, PostgresAGEGraphStorageService>();
builder.Services.AddScoped<IEntityExtractionService, EntityExtractionService>();
builder.Services.AddScoped<IEntityEmbeddingService, EntityEmbeddingService>();
builder.Services.AddScoped<IRelationshipService, RelationshipService>();

// Register Query Processing Services
builder.Services.AddScoped<IKeywordExtractionService, KeywordExtractionService>();
builder.Services.AddScoped<IHybridQueryService, HybridQueryService>();
builder.Services.AddScoped<IContextBuilderService, ContextBuilderService>();

// Register Cache Services
builder.Services.AddScoped<IEmbeddingCacheService, EmbeddingCacheService>();
builder.Services.AddScoped<ILLMResponseCacheService, LLMResponseCacheService>();

// Configure PathRAG Options
builder.Services.Configure<PathRagOptions>(builder.Configuration.GetSection("PathRAG"));

// Add MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(PathRAG.Core.Commands.InsertDocumentCommand).Assembly);
});

// CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Ensure Database and Graph Storage are initialized
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PathRagDbContext>();
    var graphStorage = scope.ServiceProvider.GetRequiredService<IGraphStorageService>();

    // Create database if it doesn't exist
    await dbContext.Database.EnsureCreatedAsync();

    // Ensure vector extension is installed
    await dbContext.EnsureVectorExtensionAsync();

    // Initialize graph storage
    await graphStorage.InitializeAsync();

    app.Logger.LogInformation("Database and extensions initialized successfully");
}

app.Run();

