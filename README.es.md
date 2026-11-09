# Registro — registro de documentos en Microsoft 365

[![build](https://github.com/KonstantinSmirnov596/registro-m365/actions/workflows/build.yml/badge.svg)](https://github.com/KonstantinSmirnov596/registro-m365/actions/workflows/build.yml)
&nbsp;·&nbsp; [English version](README.md)

**Registro** es un proyecto de aprendizaje y portafolio: el núcleo de una mesa de entradas —
registro de documentos entrantes, salientes e internos, aprobación y retención — construido sobre
**SharePoint Online, Power Automate, Power Apps, Microsoft Purview y Azure Functions (C#)**.

La regla del proyecto es *primero configuración, código solo donde la plataforma no tiene respuesta*.
Todo lo que Microsoft 365 trae de fábrica se configura en el tenant; el código se escribe para lo único
que no resuelve de forma confiable: la emisión de números de registro.

> Estado: en desarrollo (noviembre de 2026). Lo que figura como *construido* existe y funciona en un
> tenant de prueba de Microsoft 365; las definiciones de flujos y del sitio todavía no están
> exportadas a este repositorio.

## Qué está construido

| Área | Implementación | Concepto ECM |
|---|---|---|
| Arquitectura de información | Sitio de comunicación *Gestión Documental*; grupo `Registro` en el term store con *Tipo documental*, *Serie documental*, *Dependencia*; 10 columnas de sitio; jerarquía de tipos de contenido `Documento registrado` → `Documento de entrada` / `de salida` / `interno`; bibliotecas `Entrada`, `Salida`, `Interno` | tipos documentales, series documentales (cuadro de clasificación), dependencias, metadatos |
| Libro de registro | Lista `Registro`: número, tipo de flujo, fecha, contraparte, copias de texto de los metadatos administrados, vínculo e ID del archivo, estado | libro de registro |
| Registro | Flujo automatizado **Registrar entrada**: cuando el archivo en `Entrada` tiene la ficha completa → asiento en el libro → número → propiedades `Número de registro`, `Fecha de registro`, `Estado = Registrado` | registro de correspondencia entrante |
| Aprobación | Flujo instantáneo **Solicitar aprobación** (desde el menú del archivo) con Approvals: `En aprobación` → `Aprobado` / `Rechazado` | circuito de aprobación |
| Registro móvil | Canvas app **Registro móvil** generada desde la lista `Registro`, con filtro por estado (`Filter` delegable sobre columna Choice) | consulta del libro desde el celular |
| Retención | Etiqueta de retención de Purview *Correspondencia — 5 años*, publicada en el sitio | plazo de conservación por serie |
| Numeración | **Azure Function `Numerador`** (este repositorio) | números de registro atómicos |
| Aprovisionamiento (demo) | Scripts PnP PowerShell que crean un sitio demo, tipo de contenido, biblioteca y registran un documento — `scripts/powershell/demo` | — |

## Numerador — por qué hay código

El número de registro tiene la forma `E-2026-000001`: prefijo del flujo (E entrada, S salida,
I interno), año y secuencia de seis dígitos. Tiene que ser único, no reutilizarse nunca y reiniciarse
cada año en cada libro.

La primera versión del flujo armaba el número con el ID del elemento de la lista: atómico, pero
compartido por los tres libros, sin reinicio en enero y con huecos al borrar. Calcular "máximo + 1"
dentro del flujo tampoco sirve: dos ejecuciones simultáneas emiten el mismo número.

`Numerador` guarda un contador por libro y año en **Azure Table Storage** y lo incrementa con
**concurrencia optimista**: lee el contador con su ETag → escribe `valor + 1` solo si el ETag no
cambió (si cambió, Table Storage responde 412) → ante conflicto vuelve a leer y reintenta con espera
aleatoria. Sin bloqueos: un llamador que falla nunca deja trabado el libro. Detalle y alternativas en
[ADR 0002](docs/decisions/0002-numerador-optimistic-concurrency.md) (en inglés).

```
POST /api/numerador/{flujo}[?anio=2026]      flujo = E | S | I | entrada | salida | interno

200 { "numero": "E-2026-000001", "flujo": "E", "anio": 2026, "secuencia": 1 }
400 flujo desconocido o año inválido     503 contador ocupado después de todos los reintentos
```

Si no se pasa el año, se toma el calendario de la organización (`Numerador:TimeZone`, por defecto
`America/Montevideo`): un documento registrado el 31 de diciembre a las 22:00 hora local recibe el
número de ese año.

| Código | Qué hace |
|---|---|
| `src/Registro.Core/Numeracion` | algoritmo (`Numerador`), formato del número (`NumeroRegistro`), contrato de almacenamiento (`ICounterStore`) — sin dependencia de Azure |
| `src/Registro.Functions/Numeracion` | función HTTP y `TableCounterStore` sobre Azure Table Storage |
| `tests/Registro.Core.Tests` | formato y parseo, contadores por libro y por año, **300 llamadas en paralelo → números únicos y consecutivos**, abandono tras agotar reintentos |

### Ejecutarlo en local

Requisitos: .NET SDK 10, Azure Functions Core Tools 4, Azurite (Visual Studio lo levanta solo para
proyectos de Functions; si no, `npm install -g azurite` y `azurite-table`).

```powershell
dotnet test --solution Registro.slnx          # pruebas unitarias
cd src/Registro.Functions
func start --port 7104                         # http://localhost:7104/api/numerador/{flujo}
Invoke-RestMethod -Method Post http://localhost:7104/api/numerador/E
```

### Llamarlo desde Power Automate

En `Registrar entrada`, el paso *Compose* que formateaba el ID se reemplaza por una acción **HTTP**
(`POST https://<function-app>.azurewebsites.net/api/numerador/E`, encabezado `x-functions-key`), y
`body('HTTP')?['numero']` va al libro y al archivo. El conector HTTP es premium.
Esa conexión es el próximo paso; la función todavía no está desplegada en Azure.

## Estructura del repositorio

```
src/Registro.Core            lógica de dominio (.NET 10)
src/Registro.Functions       Azure Functions, worker aislado (.NET 10, Azure.Functions.Sdk)
tests/Registro.Core.Tests    xUnit v3 sobre Microsoft.Testing.Platform
scripts/powershell/demo      PnP PowerShell: sitio demo, tipo de contenido, registro de un documento
docs/decisions               registros de decisiones de arquitectura (ADR)
sharepoint/ power-automate/ purview/   exportaciones de la configuración del tenant (pendientes)
```

## Próximos pasos

- Desplegar `Numerador` (Flex Consumption) y llamarlo desde `Registrar entrada`.
- Exportar el esquema del sitio (plantilla de aprovisionamiento PnP) y los flujos (solución) al repositorio.
- Endurecer la condición de disparo de `Registrar entrada` (una ficha con metadatos vacíos no debe pasar).
- Sincronizar el estado del libro con el del archivo; avisar al solicitante después de la aprobación.
- Aplicación automática de etiquetas de retención; flujos de registro de `Salida` e `Interno`.
- Agente de Copilot Studio que responda consultas sobre el procedimiento de registro.

## Autor

Constantino Smirnov — desarrollador ECM / gestión documental, de 1C:Document Management a
Microsoft 365. Montevideo, Uruguay.
