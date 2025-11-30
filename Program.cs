using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TonyTI_Web.Data;
using TonyTI_Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Registra HttpClientFactory genérico (útil para controllers que usam IHttpClientFactory)
builder.Services.AddHttpClient();

// se tiver um serviço tipado para OpenAI, mantém também a linha abaixo.
// Certifique-se de que a classe OpenAiChatService e a interface IChatService existam no projeto.
// Essa linha registra um typed client que pode ser injetado como IChatService.
builder.Services.AddHttpClient<IChatService, OpenAiChatService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Necessário para acessar o HttpContext em partials (ex: _Nav)
builder.Services.AddHttpContextAccessor();

// Recupera connection string do appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

// Registra a factory de conexão SQL
builder.Services.AddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

// Injeção dos serviços da aplicação
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IChamadoService, ChamadoService>();

// Serviço de envio de e-mail
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

// Swagger (opcional)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Autenticação via cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
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

// Rota padrão
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();