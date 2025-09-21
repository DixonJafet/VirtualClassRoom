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

builder.Services.AddControllers(options =>
{
    options.ModelBinderProviders.Insert(0, new FromFormJsonModelBinderProvider());
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(new MySqlConnection(builder.Configuration.GetConnectionString("MySQLConnnection")));


builder.Services.AddScoped<InterProfessors, ProfessorsController>();
builder.Services.AddScoped<FileController>();
builder.Services.AddScoped<MyJWT>();


var allowedOrigins = builder.Configuration["AllowedCorsOrigins"]?.Split(',') ?? Array.Empty<string>();
Console.WriteLine(allowedOrigins.Length);
Console.WriteLine(allowedOrigins[0]);
builder.Services.AddCors(options => { 
    
    options.AddPolicy(name:"Policy", 
        policy =>
                { policy.WithOrigins(allowedOrigins)
                    .AllowCredentials()
                    .AllowAnyHeader()
                    .AllowAnyMethod(); 
                }); 
});

builder.Services.AddAuthorization();


builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", opt =>
{
    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                                                builder.Configuration.GetSection("JWTkey").GetValue<string>("key")));
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    
   //  app.UseSwagger();
    // app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("Policy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
