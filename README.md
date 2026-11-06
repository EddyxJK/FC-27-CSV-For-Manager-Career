# FC27 Manager Career CSV

Herramienta comunitaria para exportar jugadores de una partida de Carrera de mánager de EA SPORTS FC 27 a CSV. El procesamiento ocurre localmente; la partida y el CSV no se envían a ningún servicio.

## Descargar y usar en Windows

1. Descarga el ZIP portable desde **Releases**.
2. Extrae el ZIP completo en una carpeta normal (por ejemplo, el Escritorio).
3. Abre `FC27 Carrera CSV.exe` dentro de la carpeta extraída.
4. Selecciona tu guardado `CmMgrC...`.

La primera vez, la aplicación busca FC27 instalado y prepara localmente las listas de nombres. Este paso puede tardar un poco; después reutiliza los datos locales. Si no encuentra el juego automáticamente, te pedirá seleccionar su carpeta. Necesitas FC27 instalado, pero no Node.js ni Python.

El CSV se crea junto al guardado con el sufijo `_jugadores.csv`. Si ya existe, la aplicación pregunta antes de reemplazarlo. Extrae y conserva todos los archivos del ZIP juntos; el lanzador necesita la carpeta `parser` y los runtimes incluidos.

## Compilar desde el código fuente

Para compilar en Windows, instala Node.js 24 o posterior y Python 3. Después ejecuta en PowerShell:

```powershell
.\build-windows.ps1
```

El proceso prepara runtimes portátiles y crea `dist\FC27-Carrera-CSV-portable-v1.0.0.zip`. No extrae ni incorpora datos de FC27 durante la compilación. La aplicación los obtiene del juego local la primera vez que se usa.

## Ejecutar el parser directamente

```powershell
npm install
node .\src\parse-fc27-career.mjs --save "C:\ruta\CmMgrC..." --out "C:\ruta\jugadores.csv" --names-db "C:\ruta\fifa_ng_db"
```

El parser usa el esquema FC27 de [`eafc-db-kit`](https://github.com/ViniMacacari/eafc-db-kit). Para generar diccionarios locales desde una base de datos extraída:

```powershell
node .\src\build-career-names.mjs --game-db "C:\ruta\fifa_ng_db" --out ".\private-data\career-names-fc27.json" --names-out ".\private-data\playernames-fc27.json"
```

## Archivos locales y privacidad

El programa no lleva tablas de nombres de EA dentro del ZIP. Extrae esas tablas desde FC27 en el primer uso y las conserva en la carpeta portable local. No subas guardados, CSV, bases `.fifa_ng_db`, diccionarios generados ni la carpeta extraída de la aplicación al repositorio. Para GitHub Releases adjunta solo el ZIP portable completo; no adjuntes el antiguo ejecutable autocontenido.

## Datos exportados

Incluye identificador, nombre, club o selección, media, potencial, fecha de nacimiento del juego, nacionalidad como ID, posiciones, pie, filigranas, altura, peso, salario, dorsal, lesión y estadísticas de liga disponibles. Cada asociación jugador-equipo produce una fila; algunos jugadores aparecen en más de un equipo. Si falta un nombre en las listas locales, el CSV muestra `[ID n]`.

## Créditos y licencia

Este proyecto se ofrece bajo MIT. Consulta [`GUIA_Y_AGRADECIMIENTOS.txt`](GUIA_Y_AGRADECIMIENTOS.txt) y [`THIRD_PARTY_NOTICES.txt`](THIRD_PARTY_NOTICES.txt) para agradecimientos y avisos de dependencias.

Proyecto comunitario no oficial, sin afiliación ni respaldo de Electronic Arts.
