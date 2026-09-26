# Plataforma de Incidencias

Examen Parcial - ASP.NET Core MVC

## Tecnologías

- ASP.NET Core MVC
- ASP.NET Core Identity
- Entity Framework Core
- SQLite
- Algolia
- Redis
- PieHost / PieSocket
- Render
- Git / GitHub

## Usuario supervisor

**Usuario:** `supervisor@incidencias.com`  
**Contraseña:** `Supervisor123`

## Funcionalidades

### Algolia

Permite buscar incidencias por estación o descripción.

Los resultados obtenidos desde Algolia son comprobados contra SQLite para mostrar únicamente incidencias que continúan abiertas.

### Redis

El listado general de incidencias abiertas utiliza Redis.

- Tiempo de caché: 60 segundos.
- Se registra en logs si la consulta proviene de Redis o SQLite.
- Al cerrar una incidencia se invalida la caché.
- Las búsquedas mediante Algolia no utilizan esta caché.

### PieHost / PieSocket

Al cerrar una incidencia:

1. Se actualiza primero el estado en SQLite.
2. Se invalida la caché Redis.
3. Se publica el evento `IncidenciaActualizada`.
4. El evento contiene `Id` y `Estado`.
5. Las sesiones conectadas reciben la actualización mediante WebSocket.

La pantalla intenta reconectarse automáticamente si se pierde la conexión.

## Flujo de cierre

SQLite -> Invalidación Redis -> PieHost -> Actualización WebSocket

## Despliegue

Aplicación desplegada mediante Render Web Service.

URL pública:

https://plataformaincidencias.onrender.com/

## Ramas principales

- feature/busqueda-algolia
- feature/cache-redis
- feature/websocket-piehost

Las ramas parten del commit base:

`780d824 - Base MVC Identity SQLite e incidencias`

## Commit desplegado

`add2446 - Integrar Redis Algolia y PieSocket`

## Pruebas

### Algolia
Buscar una estación o descripción y comprobar que solo se muestran incidencias abiertas.

### Redis
Abrir el listado general dos veces. Los logs permiten comprobar MISS/consulta a SQLite y posteriormente HIT de Redis.

### PieSocket
Abrir dos sesiones de incidencias. Al cerrar una incidencia en una sesión, la segunda recibe el evento y actualiza la lista sin recargar manualmente.

### Autenticación
Ingresar mediante el usuario supervisor y acceder a `/Operaciones/Incidencias`.

## Seguridad

Las credenciales de los servicios externos se configuran mediante variables de entorno / User Secrets y no deben almacenarse directamente en el repositorio.