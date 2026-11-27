using BallastLane.Application.Abstractions.Security;
using BallastLane.Application.UseCases.Users.LoginUser;
using BallastLane.Infrastructure;
using BallastLane.Infrastructure.Security;
using BallastLane.IdentityApi.Middleware;
using BallastLane.IdentityApi.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(service => JwtSettings.Load(service.GetRequiredService<IConfiguration>()));
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    var problem = new ProblemDetails
    {
        Title = "Validation failed",
        Detail = "One or more inputs are invalid.",
        Status = StatusCodes.Status400BadRequest,
        Instance = context.HttpContext.Request.Path
    };
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
});
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IUserPasswordHasher>(new UserPasswordHasher());
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services.AddScoped<LoginUserValidator>();
builder.Services.AddScoped<LoginUserHandler>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => { var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? []; if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod(); }));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtSettings>((options, jwt) =>
{
    options.TokenValidationParameters = jwt.CreateValidationParameters();
    options.MapInboundClaims = false;
    options.IncludeErrorDetails = false;
});
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => { options.DocumentFilter<HealthDocumentationFilter>(); options.OperationFilter<AuthorizationDocumentationFilter>(); options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml")); options.SwaggerDoc("v1", new OpenApiInfo { Title = "BallastLane Identity API", Version = "v1" }); options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" }); });
var app = builder.Build();
_ = app.Services.GetRequiredService<JwtSettings>();
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
