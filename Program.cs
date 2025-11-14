using Microsoft.EntityFrameworkCore;
using IdentificacionPagos.Services;
using ERP.CONTEXTPV;

var builder = WebApplication.CreateBuilder(args);

// Configurar el contexto con DbContextOptions para MySQL
var connectionString = "Server=189.203.180.53;Port=3307;Database=db_erp_CORONANGO_CORONANGO_punto_venta;User ID=root;Password=Truenos21;";

var optionsBuilder = new DbContextOptionsBuilder<DbErpPuntoVentaContext>();
optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

builder.Services.AddScoped<DbErpPuntoVentaContext>(provider => 
    new DbErpPuntoVentaContext(optionsBuilder.Options));

builder.Services.AddScoped<SolicitudService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Habilitar Swagger en todos los ambientes
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

app.Run();
