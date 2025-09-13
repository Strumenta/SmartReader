# Development

This page contain information useful to develop or modify the project for your own use.

## Demo & Console Projects

The [GitHub repository](https://github.com/strumenta/SmartReader) contains both a console and web demo projects. The web demo is a simple ASP.NET Core webpage that allows you to input an address and see what the library returns. You can also use the Docker project to see the web demo in action.

The console project is a Console program that allows you to see the results of the library on a random test page.

## Creating The Nuget Package

In case you want to build the Nuget package yourself you can use the following command.

```
dotnet pack .\SmartReader.csproj --configuration Release --output ..\nupkgs\
```

The command must be issued inside the `src/SmartReader` folder.

## Documentation

The project contains a `docfx_project` folder that set up the static documentation website. It uses [DocFx](https://dotnet.github.io/docfx/index.html) to generated documentation that contains automatically generated API reference with comments from the source code.

If you need to build the documentation, you must first ensure that you have installed docfx. You can install it using any recent .NET SDK.

```
dotnet tool update -g docfx
```

Them you just run the following command inside the `docfx_project` folder.

```
docfx build
```

This will generate a static website inside the `docfx_project/_site` folder.

You can also use the option --serve to launch a demo of the site on localhost:8080.

```
docfx --serve
```

## Contributors

- [Gabriele Tomassetti](https://github.com/gabriele-tomassetti)
- [Jason Nelson](https://github.com/iamcarbon)
- [Dan Rigby](https://github.com/DanRigby)
- [Yasindn](https://github.com/yasindn)
- [Jamie Lord](https://github.com/jamie-lord)
- [Gábor Gergely](https://github.com/kodfodrasz)
- [Andy Schmitt](https://github.com/AndySchmitt)
- [Andrew Lombard](https://github.com/alombard)
- [Latis Vlad](https://github.com/latisvlad)
- [Rohit Patil](https://github.com/RohitPatilRRP)
- [theolivenbaum](https://github.com/theolivenbaum)
- [Daniel Egbers](https://github.com/DanielEgbers)
- [Sander Schutten](https://github.com/sschutten)
- [sucrose](https://github.com/sucrose0413 )
- [Ian Smirlis](https://github.com/iansmirlis)
- [Peter Hagen](https://github.com/PeterHagen)
- [Andrea Bondanini](https://github.com/AndreBonda)
- [Clay Lenhart](https://github.com/xclayl)
- [yandrapragada](https://github.com/yandrapragada)

Thanks to all the people involved.