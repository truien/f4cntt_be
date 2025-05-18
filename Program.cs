using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BACKEND.Models;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.Cookies;
using VNPAY.NET;
using System.Net.Http.Headers;
using BACKEND.Utilities;
using BACKEND.Services;
using BACKEND.Workers;
using Azure.AI.Translation.Text;
using Azure;
using Microsoft.Extensions.FileProviders;
using BACKEND.Configuration;
using Microsoft.Extensions.Options;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<EmailService>();
Env.Load();
builder.Services.AddDbContext<DBContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))));

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret is missing in configuration.");
var key = Encoding.UTF8.GetBytes(secret);

builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "F4CNTT";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.Cookie.SameSite = SameSiteMode.None;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            }
        };
    });


builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "F4CNTT", Version = "pro max" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập token theo định dạng: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] { }
        }
    });
});


// Cấu hình CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://localhost:3000",
            "http://localhost:3001",
            "https://top-cv-n3.vercel.app"
       )
             .AllowAnyHeader()
             .AllowAnyMethod()
             .AllowCredentials();
    });
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5001);
});

builder.Services.AddHostedService<TtsWorker>();
var rapidCfg = builder.Configuration.GetSection("RapidApi");
var rapidHost = rapidCfg["Host"]!;
var rapidKey = rapidCfg["Key"]!;
builder.Services.AddHttpClient<ITranslateService, RapidApiTranslateService>(client =>
{
    client.BaseAddress = new Uri($"https://{rapidHost}");
    client.DefaultRequestHeaders.Add("X-RapidAPI-Host", rapidHost);
    client.DefaultRequestHeaders.Add("X-RapidAPI-Key", rapidKey);
    client.DefaultRequestHeaders.Accept
          .Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddSingleton<PdfCoKeyManager>();
builder.Services.AddHttpClient("PdfCo")
    .ConfigureHttpClient((sp, client) =>
    {
        client.BaseAddress = new Uri("https://api.pdf.co/v1/");
    });
var pdfAiCfg = builder.Configuration.GetSection("PdfAi");
builder.Services.AddHttpClient<IPdfAiService, PdfAiService>(client =>
{
    client.BaseAddress = new Uri(pdfAiCfg["BaseUrl"]!);
    client.DefaultRequestHeaders.Add("X-API-Key", pdfAiCfg["ApiKey"]!);
    client.DefaultRequestHeaders.Accept
            .Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.Configure<PdfAiOptions>(builder.Configuration.GetSection("PdfAi"));
builder.Services.AddHttpClient<IPdfAiService, PdfAiService>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<PdfAiOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl);
    client.DefaultRequestHeaders.Add("X-API-Key", opts.ApiKey);
    client.DefaultRequestHeaders.Accept
            .Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddHostedService<PdfConversionWorker>();
builder.Services.AddHostedService<SummaryWorker>();
builder.Services.AddScoped<IVnpay, Vnpay>();
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddHostedService<TtsWorker>();
var env = builder.Environment;
var ttsFolder = Path.Combine(env.WebRootPath, "tts");
Directory.CreateDirectory(ttsFolder);

// Đăng ký static files cho /tts
builder.Services.AddSingleton<IFileProvider>(new PhysicalFileProvider(ttsFolder));
var app = builder.Build();

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/tts",
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tts"))
});
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    c.RoutePrefix = "swagger"; // Đường dẫn là /swagger
});

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
