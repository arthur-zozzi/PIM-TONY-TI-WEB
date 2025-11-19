using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TonyTI_Web.Data;
using TonyTI_Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Recupera e valida a connection string (lança exceção se não existir)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada em appsettings.json");

// Registra a fábrica de conexão com a connection string válida
builder.Services.AddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

// Serviços de negócio (injeção de dependência)
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IChamadoService, ChamadoService>();

// Registra o email sender (use Transient ou Scoped conforme sua necessidade).
// Atenção: certifique-se de ter apenas UMA interface IEmailSender no projeto.
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

// (Opcional) Swagger (apenas para desenvolvimento)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Autenticação via cookie (configuração básica)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    });

var app = builder.Build();

// Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
