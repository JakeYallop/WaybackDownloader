## Creating a release

When creating a new release on GitHub, it will prompt you to create a tag for the release. It should follow semver.

### Pre-publish checks

* Enusre version set in `DefaultCommand.Version` is up to date
* Ensure all unit tests pass

### Publishing

```
dotnet publish -c Release -r win-x64
```

Attach the binary when creating a new release through GitHub. Make sure the release version matches the version specified in `DefaultCommand.Version`.

This version value is printed out at the top of the tool when run, and also used in its user agent string.

### Testing
```
archive.org ./downloads -m exact --limitPages 1 -p SomeString1 -r 1 -v --clearHistory -f statuscode:200 -f mimetype:text/html --from 20230101 --to 2024
```
