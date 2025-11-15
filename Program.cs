using Microsoft.EntityFrameworkCore;
using IdentificacionPagos.Services;
using ERP.CONTEXTPV;
using ERP.CONTEXTSIGSA;
using GRP.ContextCatastro;

var builder = WebApplication.CreateBuilder(args);

// Configurar el contexto de Punto de Venta con DbContextOptions para MySQL
var connectionStringPV = "Server=189.203.180.53;Port=3307;Database=db_erp_CORONANGO_CORONANGO_punto_venta;User ID=root;Password=Truenos21;";

var optionsBuilderPV = new DbContextOptionsBuilder<DbErpPuntoVentaContext>();
optionsBuilderPV.UseMySql(connectionStringPV, ServerVersion.AutoDetect(connectionStringPV));

builder.Services.AddScoped<DbErpPuntoVentaContext>(provider => 
    new DbErpPuntoVentaContext(optionsBuilderPV.Options));

// Configurar el contexto de SIGSA con DbContextOptions para MySQL
var connectionStringSigsa = builder.Configuration.GetConnectionString("SigsaConnection");

var optionsBuilderSigsa = new DbContextOptionsBuilder<SigsaContext>();
optionsBuilderSigsa.UseMySql(connectionStringSigsa, ServerVersion.AutoDetect(connectionStringSigsa));

builder.Services.AddScoped<SigsaContext>(provider => 
    new SigsaContext(optionsBuilderSigsa.Options));

// Configurar el contexto de Catastro con DbContextOptions para MySQL
var connectionStringCatastro = "Server=189.203.180.53;Port=3307;Database=db_erp_CORONANGO_CORONANGO_catastro;User ID=root;Password=Truenos21;";

var optionsBuilderCatastro = new DbContextOptionsBuilder<DbErpCatastroContext>();
optionsBuilderCatastro.UseMySql(connectionStringCatastro, ServerVersion.AutoDetect(connectionStringCatastro));

builder.Services.AddScoped<DbErpCatastroContext>(provider => 
    new DbErpCatastroContext(optionsBuilderCatastro.Options));

// Registrar servicios
builder.Services.AddScoped<SolicitudService>();
builder.Services.AddScoped<SincronizacionPagosService>();
builder.Services.AddScoped<ActualizacionPadronService>();

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
