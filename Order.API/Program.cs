using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderAPI.DataBase;
using OrderAPI.Services;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Diagnostics;
using OrderAPI.Models;

var builder = WebApplication.CreateBuilder(args);

// Configura��o dos servi�os
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(
            UnicodeRanges.BasicLatin,
            UnicodeRanges.Latin1Supplement,
            UnicodeRanges.LatinExtendedA
        );
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configura��o do RabbitMQ
builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();
builder.Services.Configure<RabbitMQSettings>(
    builder.Configuration.GetSection("RabbitMQ"));

//// Configura��o das respostas de erro de valida��o
//builder.Services.Configure<ApiBehaviorOptions>(options =>
//{
//    options.InvalidModelStateResponseFactory = context =>
//    {
//        var errors = context.ModelState
//            .Where(e => e.Value?.Errors.Count > 0)
//            .SelectMany(x => x.Value!.Errors)
//            .Select(x => CleanErrorMessage(x.ErrorMessage))
//            .ToList();

//        return new BadRequestObjectResult(new
//        {
//            Message = "Os dados informados s�o inv�lidos",
//            Errors = errors,
//            Code = "VALIDATION_ERROR",
//            Timestamp = DateTime.UtcNow
//        });
//    };
//});

//// Fun��o auxiliar para limpar mensagens de erro
//string CleanErrorMessage(string errorMessage)
//{
//    return errorMessage switch
//    {
//        string s when s.Contains("could not be converted") => "Tipo de dado inv�lido",
//        string s when s.Contains("required") => "Campo obrigat�rio",
//        _ => errorMessage
//    };
//}

// Configura��o do contexto do banco de dados
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configura��o do pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware de tratamento de exce��es
app.UseExceptionHandler(a => a.Run(async context =>
{
    context.Response.ContentType = "application/json; charset=utf-8";
    var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
    var exception = exceptionHandlerPathFeature?.Error;

    await context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        Message = "Ocorreu um erro inesperado",
        Details = exception?.Message,
        Code = "INTERNAL_SERVER_ERROR",
        Timestamp = DateTime.UtcNow
    }, new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }));
}));

// Ordem correta dos middlewares
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();