using System.Data;
using System.Net;
using BookingReview.Services;
using BookingReview.Services.Interfaces;
using Microsoft.AspNetCore.Diagnostics;
using MySql.Data.MySqlClient;
using PdfSharp.Fonts;
using PdfSharp.Snippets.Font;

var builder = WebApplication.CreateBuilder(args);

// Configure your database connection
builder.Services.AddScoped<IDbConnection>(c => {
    var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"];
    return new MySqlConnection(connectionString);
});

// Add services to the container.
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddRazorPages();
builder.Services.AddTransient<IReviewService, ReviewService>();
builder.Services.AddTransient<IExportService, ExportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Add global exception handling middleware
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "text/html";

        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        // Log the exception
        // TODO: IN Future add logger global

        await context.Response.WriteAsync(exception?.Message ?? "Произошла ошибка. Пожалуйста, повторите попытку позже.");
    });
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "export-pdf",
    pattern: "{controller=Export}/{action=Index}");

GlobalFontSettings.FontResolver = new NewFontResolver();

app.Run();