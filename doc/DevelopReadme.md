# RazorEngine implementation documentation 

## Building

Requirements: Windows with the .NET Framework 4.8 targeting pack and the .NET 10 SDK (or Visual Studio 2026).
The library projects also build with older SDKs, but the test projects use C# 14 (required by NUnit 4 and later).

Open `src/RazorEngine.sln` in Visual Studio, or use the command line:

```
dotnet build src/RazorEngine.sln
dotnet test src/RazorEngine.sln
dotnet pack src/RazorEngine.sln -c Release -o artifacts
```

`dotnet pack` creates the `RazorEngine.NetFramework` package (and a `.snupkg` symbol package) from `RazorEngine.Core`.
The package version is the `<Version>` property in `src/source/RazorEngine.Core/RazorEngine.Core.csproj`; it is also used for the assembly version.

Every push to `master` and `develop` and every pull request is built and tested by GitHub Actions (`.github/workflows/ci.yml`).
The test results and the packages are available as artifacts of each run.
Pushing a version tag (`v*`) runs `.github/workflows/release.yml`, which publishes the package to nuget.org and creates the GitHub release (see [Releasing](../CONTRIBUTING.md#releasing)).

## General overview:

This project aims to be a very flexible, extendable and good performing templating engine based on the Razor parser (Microsoft.AspNet.Razor -> System.Web.Razor.dll).
Everybody familiar with Razor can start building templates very quickly.

### Issues / Features / TODOs

New features are accepted via github pull requests: https://github.com/Matheis1992/RazorEngine

Issues and TODOs are tracked on github, see: https://github.com/Matheis1992/RazorEngine/issues

### Versioning: 

https://semver.org/ like versioning.

#### X.Y.Z (Major)

X version changes can contain breaking changes.

Y is the minor version. Y version changes add features and fix bugs but "should" be backwards compatible.
Breaks can happen if you use the API in an unexpected way.

Z version changes are bug-fixes and are highly backwards compatible.

#### General considerations.

* 4.x is the first version of the fork (package `RazorEngine.NetFramework`). It targets .NET Framework 4.8 and uses Razor 3 (Microsoft.AspNet.Razor 3.x), like the 3.x net45 build of the original `RazorEngine` package.
* Incrementing the X version should be compile-compatible if you do not use any obsoleted APIs.
* Incrementing the Y version should be backwards compatible, if you do not use the API in unexpected ways.
* Incrementing the Z version is fully backwards compatible.

### High level documentation ordered by project.

- `RazorEngine.Core`: The Core of the RazorEngine framework, basically all you need to get started (including Roslyn support).

- `RazorEngine.Mvc`: A template base-class implementation which uses MVC classes to get the @Html and @Url features.

- `RazorEngine.Hosts.Console`: A simple console application showing the beauty of RazorEngine.


### The Project structure:

- /.github/workflows/

	The GitHub Actions workflows: `ci.yml` builds, tests and packs the solution, `release.yml` publishes a tagged version.

- /doc/

	Project documentation files. This folder contains both development and user documentation, read directly on GitHub.
	`doc/templates` and `doc/content` are left over from the old FSharp.Formatting doc site and are currently unused.

- /src/

	The Solution directory for all projects. `src/Directory.Build.props` holds the settings shared by all projects (target framework),
	`src/Directory.Packages.props` holds the versions of all NuGet packages (Central Package Management).

	- /src/source/

		The root for all projects (not including unit test projects)

	- /src/test/

		The root for all unit test projects.

Each project should have has a corresponding project with the name `Test.${ProjectName}` in the test folder.
This test project provides unit tests for the project `${ProjectName}`.
