Thanks that you want to help! Please read the sections below.

## Contributing (General)

If you want to send a bug report just open a new issue!

Otherwise just fork the project an send a pull request to discuss and merge the changes.
If you want to discuss the changes beforehand you can open a new issue.
Please send the pull request against the `develop` branch and mention an already existing issue (if present).
To build and run the tests you need the .NET 10 SDK (or Visual Studio 2026), see [DevelopReadme](doc/DevelopReadme.md).

## Contributing Documentation

You can help improving the documentation by editing the files in the `/doc` folder.
The documentation is read directly on GitHub; there is currently no generated documentation website.You can even edit a page directly on github by clicking the edit button ([for example this page](https://github.com/Matheis1992/RazorEngine/blob/develop/CONTRIBUTING.md)).
See also https://help.github.com/articles/editing-files-in-your-repository/ 
(don't forget to send a pull request back after forking and changing something).

## Branching model

The branching model in http://nvie.com/posts/a-successful-git-branching-model/ is used (which you do not need to read to send a pull request).
New work goes into `develop` via pull requests; `master` holds the released state.

## Licensing

This project is subject to the terms and conditions defined in file ['LICENSE.md'](https://github.com/Matheis1992/RazorEngine/blob/develop/LICENSE.md), which is part of this source code package.

You can find licenses of the programs this project depends on on their nuget page.


## Releasing

1. Add a heading for the new version at the top of `doc/ReleaseNotes.md` (CI fails if it does not match `<Version>`).
2. Update `<Version>` in `src/source/RazorEngine.Core/RazorEngine.Core.csproj` and increase the last number of `<FileVersion>` (for every release, previews included).
3. Previews (e.g. `4.0.0-preview.1`) are released from `develop`, stable versions from `master` (merge `develop` into `master` first).
4. Create the tag `v<version>` (e.g. `v4.0.0`) on that commit and push it: `git tag v<version>` and `git push origin v<version>`.
5. The tag starts the release workflow (`.github/workflows/release.yml`). It checks that the tag matches `<Version>`, the release notes and the branch, then builds, tests and packs, pushes the package to nuget.org and creates the GitHub release with the packages and the newest release notes section.
   The package's release notes link points to this GitHub release. To correct the notes of a published version, fix `doc/ReleaseNotes.md` and edit the text of the GitHub release (`gh release edit v<version> --notes-file <file>`); the package itself cannot be changed.

The workflow publishes with [NuGet trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing), so no API key is stored in GitHub. One-time setup:

* On nuget.org, add a trusted publishing policy for repository owner `Matheis1992`, repository `RazorEngine` and workflow file `release.yml`.
* In the GitHub repository settings, add the secret `NUGET_USER` with the nuget.org user name (profile name, not the email address).
