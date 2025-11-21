using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp;
using ASI.Basecode.WebApp.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = Directory.GetCurrentDirectory(),
});

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// IIS / Logging
builder.WebHost.UseIISIntegration();

builder.Logging
    .AddConfiguration(builder.Configuration.GetLoggingSection())
    .AddConsole()
    .AddDebug();

PasswordManager.SetUp(builder.Configuration.GetSection("TokenAuthentication"));

// Services via your StartupConfigurer (keeps your existing pattern)
var configurer = new StartupConfigurer(builder.Configuration);
configurer.ConfigureServices(builder.Services);

builder.Services.AddScoped<IStudentDashboardService, StudentDashboardService>();
builder.Services.AddScoped<IStudyLoadService, StudyLoadService>();
builder.Services.AddScoped<ITeacherDashboardService, TeacherDashboardService>();
builder.Services.AddScoped<IAccountService, AccountService>();

// IHttpContextAccessor for teacher
builder.Services.AddHttpContextAccessor();

// Build
var app = builder.Build();

// Global exception handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Security / static assets
app.UseHttpsRedirection();
app.UseStaticFiles();

// Routing + Auth
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// If you register middleware in StartupConfigurer, call it here:
configurer.ConfigureApp(app, app.Environment);

// Endpoints
// Attribute-routed controllers work with MapControllers()
// Conventional MVC routes work with MapControllerRoute()
app.MapControllers();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Start
app.Run();
