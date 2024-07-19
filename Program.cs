using test.Components;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Net.Http;
using test;
using test.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
// Ajouter les services au conteneur.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddTransient<JwtAuthorizationHandler>();

builder.Services.AddHttpClient("authClientAPI", client =>
{
    client.BaseAddress = new Uri("http://localhost:5208/api/");
    // Remplacez par l'URL de votre API
}).AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient("noauthClientAPI", client =>
{
    client.BaseAddress = new Uri("http://localhost:5208/api/");
});

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("authClientAPI"));
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("noauthClientAPI"));
builder.Services.AddHttpContextAccessor();

// builder.Services.AddAuthentication(options =>
// {
//     options.DefaultScheme = "Cookies";
//     options.DefaultSignInScheme = "Cookies";
// })
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
    // options.Events = new JwtBearerEvents
    // {
    //     OnMessageReceived = context =>
    //     {
    //         var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
    //         if (!string.IsNullOrEmpty(token))
    //         {
    //             Console.WriteLine($"Token in request: {token}");
    //         }
    //         return Task.CompletedTask;
    //     },
    //     OnTokenValidated = context =>
    //     {
    //         var claims = context.Principal.Claims.Select(c => $"{c.Type}: {c.Value}");
    //         Console.WriteLine("Claims:");
    //         foreach (var claim in claims)
    //         {
    //             Console.WriteLine(claim);
    //         }
    //         return Task.CompletedTask;
    //     },
    //     OnAuthenticationFailed = context =>
    //     {
    //         Console.WriteLine($"Authentication failed: {context.Exception.Message}");
    //         return Task.CompletedTask;
    //     }
    // };
});
// .AddCookie(options =>
// {
//     options.Cookie.HttpOnly = true;
//     options.Cookie.SameSite = SameSiteMode.Strict;
//     options.Cookie.Domain = "localhost"; // Juste le domaine, sans protocole
//     options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
//     options.Cookie.Path = "/";
//     options.SlidingExpiration = true;
// })
builder.Services.AddAuthorizationCore();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("iss", policy => policy.RequireClaim("iss", "webapi_todolist"));
    options.AddPolicy("My", policy => policy.RequireClaim("nameid", "18"));
    options.AddPolicy("waka", policy => policy.RequireRole("User"));
    options.AddPolicy("waka", policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddScoped<TodoItemService>();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<CustomAuthenticationStateProvider>());
builder.Services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
        });
builder.Services.AddServerSideBlazor();

var app = builder.Build();

// Configurer le pipeline de traitement des requêtes HTTP.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();



app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
