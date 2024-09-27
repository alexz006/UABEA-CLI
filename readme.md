<p align="center"><img src="UABEAvalonia/Assets/logo.png" /></p>

**Quick Downloads:**

[Latest Nightly (Windows)](https://nightly.link/nesrak1/UABEA/workflows/dotnet-desktop/master/uabea-windows.zip) | [Latest Nightly (Linux)](https://nightly.link/nesrak1/UABEA/workflows/dotnet-ubuntu/master/uabea-ubuntu.zip) | [Latest Release](https://github.com/nesrak1/UABEA/releases)

[![GitHub issues](https://img.shields.io/github/issues/nesrak1/UABEA?logo=GitHub&style=flat-square)](https://github.com/nesrak1/UABEA/issues) [![discord](https://img.shields.io/discord/862035581491478558?label=discord&logo=discord&logoColor=FFFFFF&style=flat-square)](https://discord.gg/hd9VdswwZs)

## UABEAvalonia

Cross-platform Asset Bundle/Serialized File reader and writer. Originally based on (but not a fork of) [UABE](https://github.com/SeriousCache/UABE).

## Command line

```
WARNING: Command line support VERY EARLY
There is a high chance of stuff breaking
Use at your own risk

  UABEAvalonia batchexportbundle <directory>
  UABEAvalonia batchimportbundle <directory>
  UABEAvalonia applyemip <emip file> <directory>
  UABEAvalonia exportdumps <bundle file> [-<containerName1> -<containerName2> ...]
  UABEAvalonia importdumps <bundle file>

Bundle import/export arguments:
  -keepnames writes out to the exact file name in the bundle.
      Normally, file names are prepended with the bundle's name.
      Note: these names are not compatible with batchimport.
  -kd keep .decomp files. When UABEA opens compressed bundles,
      they are decompressed into .decomp files. If you want to
      decompress bundles, you can use this flag to keep them
      without deleting them.
  -fd overwrite old .decomp files.
  -md decompress into memory. Doesn't write .decomp files.
      -kd and -fd won't do anything with this flag set.
```


## Extracting assets

I develop UABEA as more of a modding/research tool than an extracting tool. Use [AssetRipper](https://github.com/AssetRipper/AssetRipper) or [AssetStudio](https://github.com/Perfare/AssetStudio/) if you only want to extract assets.

## Addressables

Many games are also now using addressables. You can tell if the bundle you're opening is part of addressables because it has the path `StreamingAssets/aa/XXX/something.bundle`. [If you want to edit these bundles, you will need to clear the CRC checks with the CRC cleaning tool here](https://github.com/nesrak1/AddressablesTools/releases). Use `Example patchcrc catalog.json`, then move or rename the old catalog.json file and rename catalog.json.patched to catalog.json.

## Libraries

- [Avalonia](https://github.com/AvaloniaUI/Avalonia) (MIT license)
  - [Dock.Avalonia](https://github.com/wieslawsoltes/Dock) (MIT license)
  - [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) (MIT license)
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET/tree/upd21-with-inst) (MIT license)
  - [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) (MIT license)
  - [Mono.Cecil](https://github.com/jbevain/cecil) (MIT license)
  - [AssetRipper.TextureDecoder](https://github.com/AssetRipper/TextureDecoder) (MIT license)
- [ISPC Texture Compressor](https://github.com/GameTechDev/ISPCTextureCompressor) (MIT license)
- [Unity crnlib](https://github.com/Unity-Technologies/crunch/tree/unity) (zlib license)
- [PVRTexLib](https://developer.imaginationtech.com/pvrtextool) (PVRTexTool license)
- [ImageSharp](https://github.com/SixLabors/ImageSharp) (Apache License 2.0)
- [Fsb5Sharp](https://github.com/SamboyCoding/Fmod5Sharp) (MIT license)
- [Font Awesome](https://fontawesome.com) (CC BY 4.0 license)
