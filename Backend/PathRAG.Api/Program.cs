using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Pgvector.EntityFrameworkCore;
using PathRAG.Core.Services;
using PathRAG.Infrastructure.Data;
using PathRAG.Core.Services.Graph;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Services.Entity;
using PathRAG.Core.Services.Query;
using PathRAG.Core.Services.Vector;
using PathRAG.Core;
using PathRAG.Core.Services.Cache;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Distributed Cache
builder.Services.AddDistributedMemoryCache();

// Add Logging
builder.Services.AddLogging();

// Add Authentication with Microsoft Identity and Cookie Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Cookie";
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = "Cookie";
})
// Add Cookie Authentication
.AddScheme<AuthenticationSchemeOptions, PathRAG.Api.Auth.CookieAuthenticationHandler>("Cookie", options => { })
// Add Microsoft Identity Web API
.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// Add Authorization
builder.Services.AddAuthorization();

// Configure Azure OpenAI Client
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>().GetSection("PathRAG");
    return new OpenAIClient(
        new Uri(config["Endpoint"]!),
        new Azure.AzureKeyCredential(config["ApiKey"]!)
    );
});

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Add HTTP client factory
builder.Services.AddHttpClient();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("default", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true); // Allow any origin for EventSource
    });
});

// Add DbContext
builder.Services.AddDbContext<PathRagDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.UseVector(); // Enable pgvector extension
            npgsqlOptions.EnableRetryOnFailure(5);
            npgsqlOptions.CommandTimeout(60);
        }));

// Register Core PathRAG Services
builder.Services.AddScoped<ITextChunkService, TextChunkService>();
builder.Services.AddScoped<IDocumentExtractor, DocumentExtractor>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ILLMService, LLMService>();

// Register Graph and Entity Services
builder.Services.AddScoped<IGraphStorageService, PostgresAGEGraphStorageService>();
builder.Services.AddScoped<IEntityExtractionService, EntityExtractionService>();
builder.Services.AddScoped<IEntityEmbeddingService, EntityEmbeddingService>();
builder.Services.AddScoped<IRelationshipService, RelationshipService>();

// Register Query Processing Services
builder.Services.AddScoped<IKeywordExtractionService, KeywordExtractionService>();
builder.Services.AddScoped<IHybridQueryService, HybridQueryService>();
builder.Services.AddScoped<IContextBuilderService, ContextBuilderService>();
builder.Services.AddScoped<IVectorSearchService, VectorSearchService>();

// Register Cache Services
builder.Services.AddScoped<IEmbeddingCacheService, EmbeddingCacheService>();
builder.Services.AddScoped<ILLMResponseCacheService, LLMResponseCacheService>();

// Configure PathRAG Options
builder.Services.Configure<PathRagOptions>(builder.Configuration.GetSection("PathRAG"));

// Add MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(PathRAG.Core.Commands.UploadDocumentCommand).Assembly);
});

// No streaming extensions needed for standard API approach



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Configure custom static file options
app.UseStaticFiles(new StaticFileOptions
{
    // Optionally, change the root folder for static files (if not the default wwwroot)
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),

    // Custom cache control: serves files with caching headers
    OnPrepareResponse = ctx =>
    {
        // Cache static content for 7 days (604800 seconds)
        ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=604800";
    }
});
app.UseHttpsRedirection();
// Use session
app.UseSession();

// Use CORS
app.UseCors("default");

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Add SPA fallback route to handle client-side routing
app.MapFallbackToFile("index.html");

// Ensure Database and Graph Storage are initialized
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<PathRagDbContext>();
        var graphStorage = scope.ServiceProvider.GetRequiredService<IGraphStorageService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // First ensure extensions are installed
        logger.LogInformation("Installing PostgreSQL extensions...");
        await dbContext.EnsureExtensionAsync();

        // Create database if it doesn't exist (public schema tables)
        logger.LogInformation("Creating database schema (public schema)...");

        await dbContext.Database.EnsureCreatedAsync();

        logger.LogInformation("Database schema created or verified.");

        // Initialize graph storage
        logger.LogInformation("Initializing graph storage...");
        await graphStorage.InitializeAsync();

        // Log success message
        logger.LogInformation("All tables created successfully - public schema by EF Core and ag_catalog schema by SQL");

        logger.LogInformation("Database and extensions initialized successfully");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database");
        throw; // Re-throw to prevent the application from starting with an improperly initialized database
    }
}

app.Run();

