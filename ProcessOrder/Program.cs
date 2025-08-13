using Microsoft.EntityFrameworkCore;
using ProcessOrder;
using ProcessOrder.DataBase;
using ProcessOrder.Models;
using ProcessOrder.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMQSettings>(
    builder.Configuration.GetSection("RabbitMQ")
);

builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

var host = builder.Build();
host.Run();
