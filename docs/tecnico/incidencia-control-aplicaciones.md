# Incidencia de Windows: carga de DLL .NET

## Síntoma observado

En `C:\Users\herna\Desktop\Pollotikito_POS`, los proyectos .NET 10 compilaban con cero errores y advertencias, pero una compilación de `Pollotikito.Application.dll` era rechazada al ejecutar EF y xUnit:

```text
System.IO.FileLoadException:
Could not load file or assembly '...\Pollotikito.Application.dll'.
Una directiva de Control de aplicaciones bloqueó este archivo.
(0x800711C7)
```

Ocurrió en `dotnet test` y `dotnet ef migrations add`, tanto con build como con `--no-build`. El frontend compilaba normalmente. En ese intento, 54 pruebas fallaron durante carga de la biblioteca, antes de comprobar comportamiento.

Lectura del registro `Microsoft-Windows-CodeIntegrity/Operational`: eventos 3077 y 3033, proceso `dotnet.exe`, biblioteca `Pollotikito.Application.dll`, política indicada:

```text
{0283ac0f-fff1-49ae-ada1-8a933130cad6}
```

No hay evidencia de relación con PostgreSQL ni credenciales de usuarios. No se registraron secretos en este reporte.

## Estado posterior

Después de continuar los cambios de código y generar una nueva publicación, la biblioteca pudo cargarse: migraciones ejecutadas en PostgreSQL temporal, EF sin diferencias de modelo y pruebas aprobadas. Tras ampliar dos casos, la verificación final es **56 pruebas aprobadas, 0 fallidas y 0 omitidas**, y 27 capturas generadas con Edge. **No se cambió ninguna configuración de Windows**, no se desactivaron protecciones ni se agregaron exclusiones. No se determinó la causa del rechazo de la compilación anterior; el éxito posterior no demuestra que la política haya cambiado.

## Consulta de diagnóstico para soporte / Claude

Identificar qué política de control de aplicaciones corresponde al ID registrado y por qué rechazó esa compilación local. Empezar por diagnóstico de solo lectura y evidencia de los eventos; determinar el procedimiento admitido para desarrollar .NET en este equipo. No desactivar protecciones, modificar políticas o registro, agregar exclusiones ni cambiar Windows sin aprobación explícita del propietario. Si reaparece, conservar hora, ruta y hash de la DLL afectada y comparar con una compilación ejecutable.
