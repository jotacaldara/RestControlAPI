using Microsoft.EntityFrameworkCore;
using RestControlAPI.Models;
using RestControlAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Adicionar Contexto do Banco
builder.Services.AddDbContext<nextlayerapps_SampleDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Adicionar Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IEmailService, EmailService>();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll"); // Ativa o CORS

app.UseAuthorization();

app.MapControllers();

app.Run();