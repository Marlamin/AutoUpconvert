# AutoUpconvert
Tool to automatically go from Noggit Red output (with modern features enabled) to modern ADTs readable by 8.2+ WoW clients. 

## Settings
### Example settings.json file
```json
{
    "MonitorDir": "G:/Noggit/winterwonderland",
    "FrenchConverterPath": "G:/Tools/SLFiledataADTConverter/CommunityTools.MapConverter.exe",
    "MTXPConverterPath": "G:/Tools/7x_TexAdt_MTXP_Adder/7x_TexAdt_MTXP_Adder.exe",
    "OutputDir": "C:/E83/_retail_/Patches/WinterWonderland",
    "ReplaceMapNameIn": "winterwonderland",
    "ReplaceMapNameOut": "azeroth",
    "EpsilonDir": "C:/E83",
    "EpsilonPatchName": "WinterWonderland"
}
```

### MonitorDir
Directory (e.g. inside Noggit project directory) with ADTs to monitor for changes.

### FrenchConverterPath 
Path to `CommunityTools.MapConverter.exe`.

### MTXPConverterPath
Path to [7x_TexAdt_MTXP_Adder.exe](https://github.com/Marlamin/7x_TexAdt_MTXP_Adder).

### OutputDir
Output directory to put converted files in.

### ReplaceMapNameIn/ReplaceMapNameOut (optional)
Optional name (`ReplaceMapNameIn`) in filenames to replace with another name (`ReplaceMapNameOut`) for map swapping. 

For example, to automatically rename `winterwonderland_32_32.adt` to `azeroth_32_32.adt` set these to `winterwonderland` and `azeroth` respectively.

### EpsilonDir (optional)
Path to Epsilon launcher directory.

### EpsilonPatchName (optional)
Name of Epsilon patch to keep updated (patch must already exist).