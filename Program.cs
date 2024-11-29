using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.ToString() == "/swagger" || ctx.Request.Path.ToString().StartsWith("/swagger/"))
    {
        await next();
        return;
    }

    if (!ctx.Request.Headers.TryGetValue("SecretKey", out var val) || val != builder.Configuration["SecretKey"])
    {
        ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
        await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("Forbidden"));
        return;
    }
    await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files");
if (!Directory.Exists(baseDir))
{
    Directory.CreateDirectory(baseDir);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(baseDir),
    RequestPath = ""
});

app.MapPut("/{dir}", async (IFormFile file, [FromRoute] string dir, [FromHeader] string secretKey) =>
{
    dir = WebUtility.UrlDecode(dir);
    var targetDir = Path.GetFullPath(Path.Combine(baseDir, dir));
    if (!targetDir.StartsWith(baseDir))
    {
        return Results.Forbid();
    }
    var filePath = Path.GetFullPath(Path.Combine(targetDir, file.FileName));
    if (file.FileName.Contains('/') || file.FileName.Contains('\\'))
    {
        return Results.Forbid();
    }
    if (!filePath.StartsWith(targetDir))
    {
        return Results.Forbid();
    }
    CreateParentDirectory(filePath);

    using var fs = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
    await file.CopyToAsync(fs);
    await fs.FlushAsync();
    return Results.Ok();
})
.WithOpenApi()
.DisableAntiforgery();

app.Run();


static void CreateParentDirectory(string path)
{
    var parent = Path.GetDirectoryName(path);
    if (string.IsNullOrWhiteSpace(parent))
    {
        return;
    }
    if (Directory.Exists(parent))
    {
        return;
    }
    CreateParentDirectory(parent);
    Directory.CreateDirectory(parent);
}
