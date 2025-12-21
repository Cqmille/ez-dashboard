using Microsoft.EntityFrameworkCore;
using MamyDashboard.Data;
using MamyDashboard.Models;
using MamyDashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddSingleton<GoogleCalendarService>();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Serve static files (wwwroot)
app.UseDefaultFiles();
app.UseStaticFiles();

var appSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();

// ============== RATE LIMITING (Anti brute-force) ==============
var failedAttempts = new Dictionary<string, (int Count, DateTime? BannedUntil)>();
const int MAX_ATTEMPTS = 5;
const int BAN_HOURS = 24;

string GetClientIp(HttpRequest request) =>
    request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
    ?? request.HttpContext.Connection.RemoteIpAddress?.ToString()
    ?? "unknown";

(bool IsValid, string? Error) ValidatePin(HttpRequest request)
{
    var ip = GetClientIp(request);

    // Vérifier si l'IP est bannie
    if (failedAttempts.TryGetValue(ip, out var attempt) && attempt.BannedUntil.HasValue)
    {
        if (DateTime.Now < attempt.BannedUntil.Value)
        {
            var remaining = attempt.BannedUntil.Value - DateTime.Now;
            return (false, $"IP bannie. Réessayez dans {(int)remaining.TotalHours}h{remaining.Minutes:D2}min");
        }
        // Ban expiré, reset
        failedAttempts.Remove(ip);
    }

    // Vérifier le PIN
    if (!request.Headers.TryGetValue("X-Admin-Pin", out var pin) || pin != appSettings.AdminPin)
    {
        // Incrémenter les tentatives échouées
        var currentCount = failedAttempts.TryGetValue(ip, out var current) ? current.Count + 1 : 1;

        if (currentCount >= MAX_ATTEMPTS)
        {
            failedAttempts[ip] = (currentCount, DateTime.Now.AddHours(BAN_HOURS));
            return (false, $"Trop de tentatives. IP bannie pour {BAN_HOURS}h.");
        }

        failedAttempts[ip] = (currentCount, null);
        return (false, $"PIN incorrect. Tentative {currentCount}/{MAX_ATTEMPTS}");
    }

    // PIN valide - reset les tentatives
    failedAttempts.Remove(ip);
    return (true, null);
}

// ============== API ENDPOINTS ==============

// GET /api/time - Retourne l'heure et la date formatées
app.MapGet("/api/time", () =>
{
    var now = DateTime.Now;
    var hour = now.Hour;

    string moment;
    if (hour >= 5 && hour < 12) moment = "Matin";
    else if (hour >= 12 && hour < 18) moment = "Après-midi";
    else if (hour >= 18 && hour < 22) moment = "Soir";
    else moment = "Nuit";

    return Results.Ok(new
    {
        date = now.ToString("dddd d MMMM yyyy", new System.Globalization.CultureInfo("fr-FR")),
        time = now.ToString("HH'h'mm"),
        moment
    });
});

// GET /api/events - Retourne les événements d'aujourd'hui et demain
app.MapGet("/api/events", async (GoogleCalendarService calendarService) =>
{
    try
    {
        var events = await calendarService.GetEventsAsync();
        return Results.Ok(events);
    }
    catch (Exception ex)
    {
        return Results.Ok(new { today = Array.Empty<object>(), tomorrow = Array.Empty<object>(), error = ex.Message });
    }
});

// GET /api/messages - Retourne les messages actifs
app.MapGet("/api/messages", async (AppDbContext db) =>
{
    var now = DateTime.Now;
    var messages = await db.Messages
        .Where(m => m.ExpiresAt > now)
        .OrderByDescending(m => m.CreatedAt)
        .Take(3) // Max 3 messages affichés
        .ToListAsync();

    return Results.Ok(messages.Select(m => new
    {
        m.Id,
        m.Content,
        m.Author,
        m.CreatedAt,
        m.ExpiresAt,
        timeAgo = GetTimeAgo(m.CreatedAt)
    }));
});

// POST /api/messages - Créer un nouveau message (auth requise, max 3 messages)
app.MapPost("/api/messages", async (HttpRequest request, AppDbContext db, CreateMessageRequest msg) =>
{
    var (isValid, error) = ValidatePin(request);
    if (!isValid)
        return error!.Contains("bannie")
            ? Results.Json(new { error }, statusCode: 429)
            : Results.Json(new { error }, statusCode: 401);

    // Vérifier si on a déjà 3 messages actifs
    var now = DateTime.Now;
    var activeMessagesCount = await db.Messages.CountAsync(m => m.ExpiresAt > now);
    if (activeMessagesCount >= 3)
        return Results.BadRequest(new { error = "Maximum 3 messages actifs autorisés. Supprimez un message existant." });

    var message = new Message
    {
        Content = msg.Content,
        Author = msg.Author,
        CreatedAt = DateTime.Now,
        ExpiresAt = DateTime.Now.AddHours(msg.ExpiresInHours ?? 24)
    };

    db.Messages.Add(message);
    await db.SaveChangesAsync();

    return Results.Created($"/api/messages/{message.Id}", message);
});

// DELETE /api/messages/{id} - Supprimer un message (auth requise)
app.MapDelete("/api/messages/{id}", async (HttpRequest request, AppDbContext db, int id) =>
{
    var (isValid, error) = ValidatePin(request);
    if (!isValid)
        return error!.Contains("bannie")
            ? Results.Json(new { error }, statusCode: 429)
            : Results.Json(new { error }, statusCode: 401);

    var message = await db.Messages.FindAsync(id);
    if (message == null) return Results.NotFound();

    db.Messages.Remove(message);
    await db.SaveChangesAsync();

    return Results.Ok();
});

// POST /api/admin/verify - Vérifier le PIN admin
app.MapPost("/api/admin/verify", (HttpRequest request) =>
{
    var (isValid, error) = ValidatePin(request);
    if (!isValid)
        return error!.Contains("bannie")
            ? Results.Json(new { error }, statusCode: 429)
            : Results.Json(new { error }, statusCode: 401);

    return Results.Ok(new { valid = true });
});

app.Run();

// ============== HELPERS ==============

static string GetTimeAgo(DateTime dateTime)
{
    var span = DateTime.Now - dateTime;
    
    if (span.TotalMinutes < 1) return "À l'instant";
    if (span.TotalMinutes < 60) return $"Il y a {(int)span.TotalMinutes} min";
    if (span.TotalHours < 24) return $"Il y a {(int)span.TotalHours}h";
    return $"Il y a {(int)span.TotalDays} jour(s)";
}

// ============== RECORDS ==============

record CreateMessageRequest(string Content, string Author, int? ExpiresInHours);

public class AppSettings
{
    public string AdminPin { get; set; } = "1234";
    public string ICalUrl { get; set; } = "";
}
