using Fri3ndsDrive.Api.Data;
using Microsoft.EntityFrameworkCore;
using Fri3ndsDrive.Api.Services;
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

builder.Services.AddControllers();
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddScoped<TextExtractorService>();


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("Fri3ndsDrivePolicy", policy =>
    {
        policy.WithOrigins("https://fri3ndsdrive.netlify.app")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Fri3ndsDrivePolicy");

app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();