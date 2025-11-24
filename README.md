# Identificación de Pagos - Cuenta Predial

API REST desarrollada en .NET 8 para identificar y consultar conceptos de solicitudes con respuestas del formulario "Cuenta Predial".

## Descripción

Este proyecto permite obtener todos los conceptos de solicitudes que se hayan registrado con respuestas del formulario con id 1 (nombre del formulario: "Cuenta Predial"). Los resultados se ordenan por el valor de "Cuenta Predial".

## Tecnologías

- .NET 8
- Entity Framework Core 8.0.17
- MySQL (Pomelo.EntityFrameworkCore.MySql 8.0.3)
- **ERP.CONTEXTPV v2.16.1-alpha** (Contexto de base de datos)
- Swagger/OpenAPI

## Configuración

### Cadena de Conexión

Las cadenas de conexión están configuradas en `appsettings.Development.json` (no incluido en el repositorio por seguridad).

Crear el archivo `appsettings.Development.json` con el siguiente formato:

```json
{
  "ConnectionStrings": {
    "PuntoVentaConnection": "Server=YOUR_SERVER;Port=YOUR_PORT;Database=YOUR_DATABASE;User ID=YOUR_USER;Password=YOUR_PASSWORD",
    "SigsaConnection": "Server=YOUR_SERVER;Port=YOUR_PORT;Database=YOUR_DATABASE;User ID=YOUR_USER;Password=YOUR_PASSWORD",
    "CatastroConnection": "Server=YOUR_SERVER;Port=YOUR_PORT;Database=YOUR_DATABASE;User ID=YOUR_USER;Password=YOUR_PASSWORD"
  }
}
```

La aplicación utiliza los contextos del paquete ERP.CONTEXTPV.

## Instalación y Ejecución

```bash
# Restaurar paquetes
dotnet restore

# Compilar el proyecto
dotnet build

# Ejecutar la aplicación
dotnet run
```

La API estará disponible en:
- HTTP: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`

## Endpoints

### GET /api/Solicitud/cuenta-predial

Obtiene todos los conceptos de solicitudes con respuestas del formulario "Cuenta Predial" (ID: 1).

**Respuesta exitosa (200 OK):**
```json
[
  {
    "nombreConcepto": "Impuesto Predial",
    "folioRecaudacion": "FO-2024-001",
    "fechaPago": "2024-01-15T10:30:00",
    "cuentaPredial": "12345678",
    "anioInicial": "2023",
    "anioFinal": "2024"
  }
]
```

**Respuesta de error (500):**
```json
{
  "mensaje": "Error al obtener las solicitudes",
  "error": "Descripción del error"
}
```

## Campos Retornados

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `nombreConcepto` | string | Nombre del concepto asociado a la solicitud |
| `folioRecaudacion` | string | Folio de recaudación de la solicitud |
| `fechaPago` | DateTime? | Fecha de pago de la solicitud |
| `cuentaPredial` | string | Valor del campo "Cuenta Predial" del formulario |
| `anioInicial` | string | Valor del campo "Año inicial" (si existe) |
| `anioFinal` | string | Valor del campo "Año Final" (si existe) |

## Estructura del Proyecto

```
IdentificacionPagos/
├── Controllers/
│   └── SolicitudController.cs       # Controlador con el endpoint principal
├── DTOs/
│   └── SolicitudConceptoDto.cs      # DTO para la respuesta
├── Services/
│   └── SolicitudService.cs          # Lógica de negocio
├── Program.cs                        # Configuración de la aplicación
├── IdentificacionPagos.csproj       # Archivo de proyecto
└── README.md                         # Este archivo
```

## Lógica de Negocio

El servicio `SolicitudService` realiza las siguientes operaciones:

1. Identifica todas las solicitudes que tienen respuestas del formulario con ID 1
2. Obtiene los datos de las solicitudes (folio, fecha de pago)
3. Recupera los conceptos asociados a cada solicitud
4. Extrae los valores de los campos "Cuenta Predial", "Año inicial" y "Año Final"
5. Ordena los resultados por el valor de "Cuenta Predial"

## Notas Técnicas

- El proyecto utiliza el paquete **ERP.CONTEXTPV** que proporciona el contexto `DbErpPuntoVentaContext` con todas las entidades de la base de datos
- Las consultas están optimizadas para minimizar las llamadas a la base de datos
- Swagger está habilitado en todos los ambientes para facilitar las pruebas


## Sincronización de Pagos

### Endpoint de Sincronización

**POST** `/api/Sincronizacion/sincronizar-pagos`

Este endpoint sincroniza los pagos identificados hacia la base de datos SIGSA (sbm_CORONANGO), registrándolos en la tabla `SIS_Pagos`.

#### Proceso de Sincronización

1. **Normalización de Cuenta Predial**: 
   - Patrón `R-123`, `U-456`, `S-789` → Se elimina el guión: `R123`, `U456`, `S789`
   - Patrón `123` (solo número) → Se agrega `U` por defecto: `U123`
   - Patrón `U123` (ya normalizado) → Se mantiene igual

2. **Procesamiento de Fechas**:
   - Si ambos campos (Año Inicial y Año Final) están vacíos → Se omite el registro
   - Si solo uno tiene valor → Se usa el mismo valor para ambas fechas
   - `fechaCreacion` = 01/01/{año inicial}
   - `fechaVencimiento` = 31/12/{año inicial}

3. **Mapeo de Campos a SIS_Pagos**:

| Campo SIS_Pagos | Origen | Valor Default |
|-----------------|--------|---------------|
| descripcion | nombre del concepto | |
| año | año final | |
| division | | 0 |
| fechaCreacion | 01/01/{año inicial} | |
| fechaVencimiento | 31/12/{año inicial} | |
| cantidad | monto en concepto_solicitud | |
| estatus | | x |
| folioPago | folio_recaudacion | |
| fechaPago | fecha_pago | |
| originPago | | M |
| folioCancelacion | | null |
| fechaCancelacion | | null |
| clave_pago | | null |
| referencia | {03}{cuenta_predial} | |
| Interlocutor | cuenta predial | |
| concepto | | 0 |

#### Respuesta

```json
{
  "mensaje": "Sincronización completada",
  "registrosInsertados": 100,
  "registrosOmitidos": 5,
  "totalProcesados": 105,
  "errores": []
}
```

### Base de Datos SIGSA

**Cadena de Conexión**: Configurada en `appsettings.Development.json` (ver sección de configuración)

**Paquete**: `ERP.CONTEXTSIGSA v2.2.1.1`

**Contexto**: `SigsaContext`

**Tabla**: `SIS_Pagos`
