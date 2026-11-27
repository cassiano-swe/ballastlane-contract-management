using BallastLane.Application.UseCases.Customers.CreateCustomer;
using BallastLane.Application.UseCases.Customers.DeleteCustomer;
using BallastLane.Application.UseCases.Customers.GetCustomerById;
using BallastLane.Application.UseCases.Customers.ListCustomers;
using BallastLane.Application.UseCases.Customers.UpdateCustomer;
using Microsoft.AspNetCore.Mvc;
using BallastLane.Infrastructure;
using BallastLane.Infrastructure.Security;
using BallastLane.WebApi.Middleware;
using BallastLane.WebApi.Extensions;
using BallastLane.Application.UseCases.Examples;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(service => JwtSettings.Load(service.GetRequiredService<IConfiguration>()));
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new CustomerTypeJsonConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    const string invalidCustomerId = "The customer identifier must be a valid UUID.";
    const string invalidCustomerVersion = "The customer version is required and must be a valid UUID.";
    var hasRouteId = context.RouteData.Values.ContainsKey("id");
    if (hasRouteId
        && context.ModelState.TryGetValue("id", out var idState) && idState.Errors.Count > 0)
    {
        idState.Errors.Clear();
        idState.Errors.Add(invalidCustomerId);
    }

    foreach (var entry in context.ModelState.Where(entry =>
        (entry.Key.Equals("version", StringComparison.OrdinalIgnoreCase) || entry.Key.Equals("$.version", StringComparison.OrdinalIgnoreCase))
        && entry.Value?.Errors.Count > 0))
    {
        entry.Value!.Errors.Clear();
        entry.Value.Errors.Add(invalidCustomerVersion);
    }

    var messages = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0).Select(entry => entry.Key.ToLowerInvariant() switch
    {
        "name" or "$.name" => "Name is required and must be a string.",
        "tradename" or "$.tradename" => "TradeName must be a string or null.",
        "customertype" or "$.customertype" => "CustomerType is required and must be either Individual or Company as a string.",
        "taxid" or "$.taxid" => "TaxId is required and must be a string.",
        "email" or "$.email" => "Email is required and must be a string.",
        "version" or "$.version" => invalidCustomerVersion,
        "id" when hasRouteId => invalidCustomerId,
        _ => "The request body is missing or has an invalid format."
    }).Distinct();
    var problem = new ValidationProblemDetails(context.ModelState) { Type = "about:blank", Title = "Validation failed", Status = 400, Detail = string.Join(" ", messages), Instance = context.HttpContext.Request.Path };
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
});
builder.Services.AddScoped<CreateCustomerValidator>();
builder.Services.AddScoped<CreateCustomerHandler>();
builder.Services.AddScoped<ListCustomersHandler>();
builder.Services.AddScoped<GetCustomerByIdValidator>();
builder.Services.AddScoped<GetCustomerByIdHandler>();
builder.Services.AddScoped<UpdateCustomerValidator>();
builder.Services.AddScoped<UpdateCustomerHandler>();
builder.Services.AddScoped<DeleteCustomerValidator>();
builder.Services.AddScoped<DeleteCustomerHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<CalculatePercentageValidator>();
builder.Services.AddScoped<CalculatePercentageHandler>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => { var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? []; if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod(); }));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtSettings>((options, jwt) =>
{
    options.TokenValidationParameters = jwt.CreateValidationParameters();
    options.MapInboundClaims = false;
    options.IncludeErrorDetails = false;
});
builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(
    new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SchemaFilter<CustomerSchemaFilter>();
    options.DocumentFilter<HealthDocumentationFilter>();
    options.OperationFilter<AuthorizationDocumentationFilter>();
    options.OperationFilter<ListCustomersDocumentationFilter>();
    options.OperationFilter<GetCustomerByIdDocumentationFilter>();
    options.OperationFilter<UpdateCustomerDocumentationFilter>();
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml"));
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BallastLane Business API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
});
var app = builder.Build();
_ = app.Services.GetRequiredService<JwtSettings>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseRouting();
app.UseCors();
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    var status = context.Response.StatusCode;
    if (status is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
        return;

    var problem = new ProblemDetails
    {
        Status = status,
        Title = status == StatusCodes.Status401Unauthorized ? "Authentication required" : "Access denied",
        Instance = context.Request.Path
    };
    problem.Extensions["traceId"] = context.TraceIdentifier;
    await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json",
        cancellationToken: context.RequestAborted);
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();
public partial class Program { }
