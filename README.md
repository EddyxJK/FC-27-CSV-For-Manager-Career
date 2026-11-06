# FC27 Manager Career CSV

Herramienta comunitaria para exportar jugadores de una partida de Carrera de mánager de EA SPORTS FC 27 a CSV. El procesamiento ocurre localmente; la partida y el CSV no se envían a ningún servicio.

## Descargar y usar en Windows

1. Descarga `FC27 Carrera CSV.exe` desde la sección **Releases** y ábrelo.
2. Selecciona el archivo de guardado `CmMgrC...`.
3. La primera vez, la aplicación busca FC27 instalado y prepara localmente las listas de nombres. Este paso puede tardar un poco; después reutiliza los datos locales.

El CSV se crea junto al guardado con el sufijo `_jugadores.csv`. Si ya existe, la aplicación pregunta antes de reemplazarlo. El ejecutable incluye Node y Python portátiles, por lo que no tienes que instalarlos. Sí necesitas FC27 instalado para que la aplicación lea las listas de nombres. Si no encuentra el juego automáticamente, te pedirá seleccionar su carpeta.

## Compilar desde el código fuente

Para compilar en Windows, instala Node.js 24 o posterior y Python 3. Después ejecuta en PowerShell:

```powershell
.\build-windows.ps1
```

El proceso instala la dependencia fijada, prepara los runtimes portátiles y crea `dist\FC27 Carrera CSV.exe`. No extrae ni incorpora datos de FC27 durante la compilación. La aplicación los obtiene del juego local la primera vez que se usa.

## Ejecutar el parser directamente

```powershell
npm install
node .\src\parse-fc27-career.mjs --save "C:\ruta\CmMgrC..." --out "C:\ruta\jugadores.csv" --names-db "C:\ruta\fifa_ng_db"
```

El parser usa el esquema FC27 de [`eafc-db-kit`](https://github.com/ViniMacacari/eafc-db-kit). Para generar un diccionario de nombres local desde una base extraída:

```powershell
node .\src\build-career-names.mjs --game-db "C:\ruta\fifa_ng_db" --out ".\private-data\career-names-fc27.json" --names-out ".\private-data\playernames-fc27.json"
```

## Archivos locales y privacidad

El ejecutable extrae los archivos del juego y genera diccionarios solo dentro de `%LOCALAPPDATA%\FC27 Carrera CSV`. No incorpora esos datos al ejecutable ni los sube a GitHub. No publiques guardados, CSV, bases `.fifa_ng_db`, diccionarios generados ni ejecutables compilados localmente. `.gitignore` excluye los datos y salidas locales.

## Datos exportados

Incluye identificador, nombre, club o selección, media, potencial, fecha de nacimiento del juego, nacionalidad como ID, posiciones, pie, filigranas, altura, peso, salario, dorsal, lesión y estadísticas de liga disponibles. Cada asociación jugador-equipo produce una fila; algunos jugadores aparecen en más de un equipo. Si alguna tabla local no contiene un nombre, el CSV muestra `[ID n]`.

## Créditos y licencia

Este proyecto se ofrece bajo MIT. Consulta [`GUIA_Y_AGRADECIMIENTOS.txt`](GUIA_Y_AGRADECIMIENTOS.txt) y [`THIRD_PARTY_NOTICES.txt`](THIRD_PARTY_NOTICES.txt) para agradecimientos y avisos de dependencias.

Proyecto comunitario no oficial, sin afiliación ni respaldo de Electronic Arts.
