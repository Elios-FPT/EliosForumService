
using ForumService.Core.Extensions;
using ForumService.Core.Interfaces;
using ForumService.Infrastructure.Data;
using ForumService.Infrastructure.Implementations;
using ForumService.Infrastructure.Kafka;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Redis;
using Microsoft.OpenApi.Models;
using SUtility.Infrastructure.Implementations;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;


var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<ForumDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ForumDb")));

// Add services to the container
builder.Services.AddControllers();

// Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ForumService API",
        Version = "v1",
        Description = "API forum operations"
    });
    c.AddServer(new OpenApiServer { Url = "/" });
});

builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
builder.Services.AddScoped<IAppCacheService, AppCacheService>();
builder.Services.AddScoped<IAppConfiguration, AppConfiguration>();
builder.Services.AddScoped<ICombinedTransaction, CombinedTransaction>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IKafkaProducer, KafkaProducer>();
builder.Services.AddScoped(typeof(IKafkaRepository<>), typeof(KafkaRepository<>));
builder.Services.AddScoped<IKafkaTransaction, KafkaTransaction>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

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

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ForumService.Core.AssemblyReference).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(ForumService.Contract.AssemblyReference).Assembly);
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ForumService API v1");
        c.DocumentTitle = "ForumService API Documentation";
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseRouting(); // Thêm UseRouting để tối ưu middleware
app.UseAuthorization();
app.MapControllers();

app.Run();