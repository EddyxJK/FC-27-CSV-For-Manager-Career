# FC27 Manager Career CSV

Exporta la tabla de jugadores de un guardado de Carrera de mánager de EA SPORTS FC 27 a CSV. La aplicación de Windows abre un selector de archivo y crea el CSV junto al guardado. El lector procesa el archivo localmente y no envía el guardado ni el CSV a ningún servicio.

## Uso en Windows

1. Instala Node.js 24 o posterior y Python 3. FC27 debe estar instalado en el equipo.
2. Abre PowerShell en esta carpeta y ejecuta `.\build-windows.ps1`.
3. Abre `dist\FC27 Carrera CSV.exe`.
4. Selecciona el archivo de guardado `CmMgrC...`.

El CSV se crea en la misma carpeta con el sufijo `_jugadores.csv`. Si ya existe, la aplicación pregunta antes de reemplazarlo.

## Ejecutar el parser directamente

```powershell
npm install
node .\src\parse-fc27-career.mjs --save "C:\ruta\CmMgrC..." --out "C:\ruta\jugadores.csv"
```
CÓMO ENCONTRAR EL GUARDADO DE CARRERA EN WINDOWS

No necesitas buscar una carpeta llamada “Saves”: el archivo suele estar directamente dentro de settings.

Pulsa Windows + R.

Pega esta ruta y pulsa Enter:
%LOCALAPPDATA%\EA SPORTS FC 27\settings

Busca el archivo que comienza con CmMgrC. Puede aparecer sin extensión.

Abre FC27 Carrera CSV y elige ese archivo. El CSV se guardará junto a él.

Si esa carpeta no aparece, busca CmMgrC* desde el Explorador de archivos, dentro de %LOCALAPPDATA%. En algunas instalaciones también puede estar en %USERPROFILE%\Documents\FC 27\settings.

Al compilar, el script busca FC27 instalado, extrae localmente las tablas de nombres del juego y las incorpora al ejecutable. No tienes que descargar ni añadir diccionarios manualmente; el proceso no sube esos archivos a GitHub. Si el juego está en una carpeta personalizada, indícala así:

```powershell
.\build-windows.ps1 -GameDirectory "D:\SteamLibrary\steamapps\common\EA SPORTS FC 27"
```

El parser usa el esquema FC27 de [`eafc-db-kit`](https://github.com/ViniMacacari/eafc-db-kit). Para ejecutarlo directamente se puede indicar una base de datos FC27 con `--names-db`. También es posible crear diccionarios locales manualmente:

```powershell
node .\src\build-career-names.mjs --game-db "C:\ruta\fifa_ng_db" --out ".\private-data\career-names-fc27.json"
```

Los diccionarios se generan desde la instalación local del usuario. No los subas al repositorio. Si la persona no tiene FC27 instalado, puede compilar con `-SkipGameData`, pero los nombres que dependan de esas listas aparecerán como `[ID n]`.

## Datos exportados

Incluye identificador, nombre, club o selección, media, potencial, fecha de nacimiento del juego, nacionalidad como ID, posiciones, pie, filigranas, altura, peso, salario, dorsal, lesión y estadísticas de liga disponibles. Cada asociación jugador-equipo produce una fila; algunos jugadores aparecen en más de un equipo.

Los nombres y detalles de jugadores generados por la carrera pueden depender de datos locales del juego. El repositorio no debe contener guardados, CSV de carrera, bases extraídas del juego, diccionarios de nombres ni ejecutables construidos localmente.

## Compilar la aplicación

`build-windows.ps1` requiere Node.js 24 o posterior, Python 3 y una instalación local de FC27. Instala la dependencia fijada, extrae las bases CAS del juego, genera diccionarios locales, prepara el paquete y compila el lanzador de Windows. Puedes dar la ubicación con `-GameDirectory` o usar `FC27_GAME_DIR`. Para compilar sin nombres se puede usar `-SkipGameData`. Los archivos extraídos y los diccionarios quedan dentro de `.build/`, excluida de Git. No publiques el ejecutable compilado localmente. `dist/`, `.build/`, ejecutables y datos locales están excluidos de Git.

## Créditos y licencia

Este proyecto nació de la idea de exportar datos de Carrera a una hoja de cálculo. Agradecimientos y atribuciones adicionales están en [`GUIA_Y_AGRADECIMIENTOS.txt`](GUIA_Y_AGRADECIMIENTOS.txt) y [`THIRD_PARTY_NOTICES.txt`](THIRD_PARTY_NOTICES.txt). El código de este repositorio se ofrece bajo MIT; el aviso no cambia las licencias de las dependencias.

Proyecto comunitario no oficial, sin afiliación ni respaldo de Electronic Arts.

