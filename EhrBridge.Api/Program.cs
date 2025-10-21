// EhrBridge.Api/Program.cs
using EhrBridge.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
    
// Register services for dependency injection using interfaces:
// 1. Audit Service (now interface-driven)
builder.Services.AddScoped<IAuditService, AuditService>();
// 2. Control Service (new service, also interface-driven)
builder.Services.AddScoped<IControlService, ControlService>();
// 3. EXPORT Service (NEW registration for CSV exports)
builder.Services.AddScoped<IExportService, ExportService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.

// 🛑 FIX: The UseSwagger/UseSwaggerUI calls are moved outside of the IsDevelopment check,
// and the RoutePrefix is explicitly set to string.Empty. This forces the Swagger UI
// to load at the root URL of the exposed port, resolving the Codespace 404 issue.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    // Set the explicit path to the generated Swagger JSON
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EHR Bridge API v1");
    // Force the Swagger UI homepage to the root URL (e.g., https://...-8080.app.github.dev/)
    c.RoutePrefix = string.Empty;
});


// NOTE: We are intentionally NOT using HTTPS in the containerized demo environment.
// app.UseHttpsRedirection();

// Enable static files (needed if serving frontend directly later, or for general utility)
app.UseStaticFiles();

// Add Cors Policy for React Frontend
app.UseCors(policy =>
{
    // WARNING: In a production environment, replace "*" with the specific domain
    // of your frontend application to ensure HIPAA compliance and security.
    policy.AllowAnyOrigin()
          .AllowAnyHeader()
          .AllowAnyMethod();
});

app.UseAuthorization();

app.MapControllers();

app.Run();