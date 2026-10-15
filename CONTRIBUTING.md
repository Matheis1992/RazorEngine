Thanks that you want to help! Please read the sections below.

## Contributing (General)

If you want to send a bug report just open a new issue!

Otherwise just fork the project an send a pull request to discuss and merge the changes.
If you want to discuss the changes beforehand you can open a new issue.
Please send the pull request against the `develop` branch and mention an already existing issue (if present).

## Contributing Documentation

You can help improving the documentation by editing the files in the `/doc` folder.
You can even edit a page directly on github by clicking the edit button ([for example this page](https://github.com/Matheis1992/RazorEngine/blob/develop/CONTRIBUTING.md)).
See also https://help.github.com/articles/editing-files-in-your-repository/ 
(don't forget to send a pull request back after forking and changing something).

## Branching model

The branching model in http://nvie.com/posts/a-successful-git-branching-model/ is used (which you do not need to read to send a pull request).
New work goes into `develop` via pull requests; `master` holds the released state.

## Licensing

This project is subject to the terms and conditions defined in file ['LICENSE.md'](https://github.com/Matheis1992/RazorEngine/blob/develop/LICENSE.md), which is part of this source code package.

You can find licenses of the programs this project depends on on their nuget page.


## Releasing

1. Update `doc/ReleaseNotes.md`.
2. Update `<Version>` in `src/source/RazorEngine.Core/RazorEngine.Core.csproj`.
3. Run `dotnet pack src/RazorEngine.sln -c Release -o artifacts` (or take the `packages` artifact of the CI run).
4. Push the package with `dotnet nuget push artifacts/RazorEngine.NetFramework.<version>.nupkg --api-key <key> --source https://api.nuget.org/v3/index.json`.
