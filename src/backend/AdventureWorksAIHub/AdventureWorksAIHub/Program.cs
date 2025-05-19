using AdventureWorksAIHub.Core.Application.Mappings;
using AdventureWorksAIHub.Infrastructure;
using AdventureWorksAIHub.Middleware;
using AutoMapper;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Add Infrastructure services (DI çaðrýsý eklendi)
builder.Services.AddInfrastructure(builder.Configuration);

// Add Endpoints API Explorer
builder.Services.AddEndpointsApiExplorer();
// Þu anda eksik olan bir þeyse, ekleyebilirsiniz
builder.Services.AddAutoMapper(typeof(Program).Assembly, typeof(MappingProfile).Assembly);

// Veya daha spesifik olarak
builder.Services.AddAutoMapper(cfg => {
    cfg.AddProfile<MappingProfile>();
}, typeof(MappingProfile).Assembly);

// Add Swagger Generator
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Use custom error handling middleware
app.UseMiddleware<ErrorHandlingMiddleware>();

// Swagger configuration for Scalar 1.2.66
app.UseSwagger(opt => opt.RouteTemplate = "openapi/{documentName}.json");

// Scalar API Reference
app.MapScalarApiReference(
    opt => {
        opt.Title = "AdventureWorks AI Hub API";
        opt.Theme = ScalarTheme.Kepler;
        opt.DefaultHttpClient = new(ScalarTarget.Http, ScalarClient.Http11);

    }
);

// HTTP Redirection
app.UseHttpsRedirection();

// Authorization
app.UseAuthorization();

// Map controllers
app.MapControllers();

app.Run();