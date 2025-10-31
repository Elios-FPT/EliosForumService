using ForumService.Contract.Message;
using ForumService.Contract.Models;
using ForumService.Core.Extensions;
using ForumService.Core.Interfaces;
using ForumService.Core.Interfaces.Comment;
using ForumService.Core.Interfaces.Post;
using ForumService.Core.Interfaces.Tag;
using ForumService.Domain.Models;
using ForumService.Infrastructure.Data;
using ForumService.Infrastructure.Implementations;
using ForumService.Infrastructure.Implementations.Comment;
using ForumService.Infrastructure.Implementations.Post;
using ForumService.Infrastructure.Implementations.Tag;
using ForumService.Infrastructure.Kafka;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Linq;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;


var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory(),
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
});

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("forum", new OpenApiInfo
    {
        Title = "ForumService API",
        Version = "forum",
        Description = "API utility operations"
    });
    c.AddServer(new OpenApiServer { Url = "/" });
});

// Services Registration

builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
builder.Services.AddScoped<IAppConfiguration, AppConfiguration>();
builder.Services.AddScoped<ICombinedTransaction, CombinedTransaction>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IKafkaProducer, KafkaProducer>();
builder.Services.AddScoped(typeof(IKafkaProducerRepository<>), typeof(KafkaProducerRepository<>));
builder.Services.AddScoped(typeof(IKafkaConsumerRepository<>), typeof(KafkaConsumerRepository<>));
builder.Services.AddScoped(typeof(IKafkaConsumerFactory<>), typeof(KafkaConsumerFactory<>));
builder.Services.AddScoped(typeof(IKafkaResponseHandler<>), typeof(KafkaResponseHandler<>));
builder.Services.AddScoped<IKafkaTransaction, KafkaTransaction>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

builder.Services.AddHttpClient<ISUtilityServiceClient, SUtilityServiceClient>(client =>
{
    string? serviceUrl = builder.Configuration["ServiceUrls:SUtilityService"];
    if (string.IsNullOrEmpty(serviceUrl))
    {
        throw new InvalidOperationException("SUtilityService URL is not configured in appsettings.json.");
    }
    client.BaseAddress = new Uri(serviceUrl);
});

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() 
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

builder.Services.AddScoped<IPostQueryRepository, PostQueryRepository>();
builder.Services.AddScoped<ITagQueryRepository, TagQueryRepository>();
builder.Services.AddScoped<ICommentQueryRepository, CommentQueryRepository>();

builder.Services.AddFusionCache()
    .WithSerializer(new FusionCacheSystemTextJsonSerializer())
    .WithDefaultEntryOptions(new FusionCacheEntryOptions
    {
        Duration = TimeSpan.FromDays(builder.Configuration.GetValue<double>("Cache:DurationDays", 14)),
        DistributedCacheDuration = TimeSpan.FromDays(builder.Configuration.GetValue<double>("Cache:DurationDays", 14)),
        AllowBackgroundDistributedCacheOperations = true
    });

var redisConnection = builder.Configuration["Cache:RedisConnectionString"];
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddFusionCache().WithDistributedCache(new RedisCache(new RedisCacheOptions
    {
        Configuration = redisConnection
    }));
}

// Database Context
builder.Services.AddDbContext<ForumDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ForumDb")));



// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ForumService.Core.AssemblyReference).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(ForumService.Contract.AssemblyReference).Assembly);
});

// Kafka Consumers
var sourceServices = builder.Configuration.GetSection("Kafka:SourceServices").Get<string[]>() ?? [];

Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Registering {sourceServices.Length} Kafka consumers for sources: [{string.Join(", ", sourceServices)}]");


foreach (var sourceService in sourceServices)
{
    var currentSource = sourceService;

    builder.Services.AddSingleton<IHostedService>(sp =>
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        return ActivatorUtilities.CreateInstance<KafkaConsumerHostedService<Post>>(
            sp,
            scopeFactory,
            currentSource
        );
    });
}


var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    using var scope = app.Services.CreateScope();
    var appConfiguration = scope.ServiceProvider.GetRequiredService<IAppConfiguration>();
    KafkaResponseConsumer.Initialize(appConfiguration);
    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] KafkaResponseConsumer initialized");
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/forum/swagger.json", "ForumService API forum");
    c.DocumentTitle = "ForumService API Documentation";
    c.RoutePrefix = "swagger";
});

app.UseAuthorization();
app.MapControllers();

var currentService = builder.Configuration["KafkaCommunication:CurrentService"];
Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {currentService} Service Started!");
Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Kafka Consumers registered for: [{string.Join(", ", sourceServices)}]");

app.Run();