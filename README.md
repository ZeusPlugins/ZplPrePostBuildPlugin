# ZplPrePostBuildPlugin

Ability to define Pre-Build and Post-Build actions for your project.

## Instructions
- Install the [Zeus Plugin Loader](https://github.com/ZeusPlugins/ZeusPluginLoader) if you haven't yet.
- Copy the DLL into the Custom Plugins folder.
- Restart the IDE.
- See below for usage.

## Usage

The batch (.bat) scripts are defined as Notes.

To define a Pre-build script, make a note named `PreBuildScript` (case-sensitive).

For a Post-Build script, the note must be named `PostBuildScript`.

The working directory where scripts are written to and executed is always the project directory.

To get the parameters like the configuration, target file name, etc you can use YoYo syntax macro expansion (like in Android Java extensions!):

```bat
@echo off

echo Target file name: ${targetFile}

```

will result in:

```bat
@echo off

echo Target file name: D:\Projects\_Builds\WOAH.zip
```

being written to the project directory, executed, and then deleted.

The values are taken from the .bff JSON file the IDE is passing to Igor.exe.

If you want to parse the .bff JSON file manually, the path to it (in quotes) is passed as the first argument to the batch script.

(it's really just an indented regular JSON of key - string and value - string)

You can also cancel the build process altogether by returning a non-zero exit code from the Pre-Build script:

```bat
@echo off

rem exit with non-zero status
exit 1
```

This will result the IDE in thinking that Igor has failed and it will cancel the build process.

Returning a non-zero exit code from Post-Build scripts won't result in anything useful really.

Also keep in mind that in Pre-Build scripts the `targetFile` file may or may not exist for obvious reasons.

## Sample bff file for reference

```json
{
    "targetFile": "D:\\Projects\\_Builds\\WOAH.zip",
    "assetCompiler": "",
    "debug": "False",
    "compile_output_file_name": "D:\\Progs\\GMS23\\Temp\\GMS2TEMP\\WOAH_1A2402E_VM\\WOAH.win",
    "useShaders": "True",
    "steamOptions": "D:\\Progs\\GMS23\\Asset\\GMS2CACHE\\WOAH_4ABE5481\\steam_options.yy",
    "config": "Default",
    "configParents": "",
    "outputFolder": "D:\\Progs\\GMS23\\Temp\\GMS2TEMP\\WOAH_1A2402E_VM",
    "projectName": "WOAH",
    "macros": "D:\\Progs\\GMS23\\Asset\\GMS2CACHE\\WOAH_4ABE5481\\macros.json",
    "projectDir": "D:\\Projects\\GameMaker\\23\\WOAH",
    "preferences": "D:\\Progs\\GMS23\\Asset\\GMS2CACHE\\WOAH_4ABE5481\\preferences.json",
    "projectPath": "D:\\Projects\\GameMaker\\23\\WOAH\\WOAH.yyp",
    "tempFolder": "D:\\Progs\\GMS23\\Temp\\GMS2TEMP",
    "tempFolderUnmapped": "D:\\Progs\\GMS23\\Temp\\GMS2TEMP",
    "userDir": "C:\\Users\\nik\\AppData\\Roaming/GameMakerStudio2\\felinehaxx_1534602",
    "runtimeLocation": "C:\\ProgramData\\GameMakerStudio2\\Cache\\runtimes\\runtime-2.3.2.426",
    "targetOptions": "D:\\Progs\\GMS23\\Asset\\GMS2CACHE\\WOAH_4ABE5481\\targetoptions.json",
    "targetMask": "64",
    "applicationPath": "D:\\Games\\Steam\\steamapps\\common\\GameMaker Studio 2 Desktop\\GameMakerStudio.exe",
    "verbose": "True",
    "helpPort": "51290",
    "debuggerPort": "6509"
}
```

An example of a .bff file passed to Igor, with personal info edited, as you can see by the `userDir` property, paths are not always in Windows-compliant form.

But both the keys and values are always strings no matter what. Even booleans. Extremely easy to parse.

