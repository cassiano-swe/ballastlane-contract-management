using System.Text;
using BallastLane.Infrastructure;
using BallastLane.WebApi.Middleware;
using BallastLane.WebApi.Extensions;
using BallastLane.Application.UseCases.Examples;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
string? issuer = null, audience = null, key = null;
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<CalculatePercentageValidator>();
builder.Services.AddScoped<CalculatePercentageHandler>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => { var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? []; if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod(); }));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    // With no development configuration, the default bearer options cannot validate tokens.
    if (key is null) return;
    options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ValidateLifetime = true, RequireExpirationTime = true };
});
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => { options.DocumentFilter<HealthDocumentationFilter>(); options.OperationFilter<AuthorizationDocumentationFilter>(); options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml")); options.SwaggerDoc("v1", new OpenApiInfo { Title = "BallastLane Business API", Version = "v1" }); options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" }); });
var app = builder.Build();
var jwt = app.Configuration.GetSection("Jwt");
issuer = jwt["Issuer"];
audience = jwt["Audience"];
key = jwt["SigningKey"];
var noJwtConfiguration = string.IsNullOrWhiteSpace(issuer) && string.IsNullOrWhiteSpace(audience) && string.IsNullOrWhiteSpace(key);
if (app.Environment.IsDevelopment() && noJwtConfiguration)
    key = null;
else if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
    throw new InvalidOperationException("Configure Jwt:Issuer, Jwt:Audience and a Jwt:SigningKey of at least 32 UTF-8 bytes.");
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();
public partial class Program { }
