using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;
using ProfessorAPI.Binding;
using System.Text;
using ProfessorAPI.Connexions;
using ProfessorAPI.Interface;
using ProfessorAPI.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<BlobService>();
builder.Services.AddTransient<FileController>();
builder.Services.AddControllers(options =>
{
    options.ModelBinderProviders.Insert(0, new FromFormJsonModelBinderProvider());
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

var mysqlConnString = builder.Configuration.GetConnectionString("MySQLConnnection")
    ?? builder.Configuration.GetConnectionString("MySQLConnection")
    ?? builder.Configuration["MYSQL_ADDON_URI"];

builder.Services.AddTransient(sp => new MySqlConnection(mysqlConnString));

builder.Services.AddScoped<InterProfessors, ProfessorsController>();
builder.Services.AddScoped<FileController>();
builder.Services.AddScoped<MyJWT>();

var configuredOrigins = builder.Configuration["AllowedCorsOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? new[] { "https://virtual-class-room-peach.vercel.app", "https://localhost:5173", "https://10.0.0.5:5173" };
var allowedOrigins = configuredOrigins
    .Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
    .Append("https://virtual-class-room-peach.vercel.app")
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options => { 
    options.AddPolicy(name: "Policy", 
        policy =>
        { 
            policy.WithOrigins(allowedOrigins)
                  .AllowCredentials()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }); 
});

builder.Services.AddAuthorization();

builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", opt =>
{
    var jwtKey = builder.Configuration.GetSection("JWTkey").GetValue<string>("key")
        ?? "new-jwt-key-for-classroom-api-in-project";
    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256Signature);

    opt.RequireHttpsMetadata = false;
    opt.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateAudience = false,
        ValidateIssuer = false,
        IssuerSigningKey = signingKey,
    };
    opt.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Cookies["SchoolWebToken506"];
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("Policy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
