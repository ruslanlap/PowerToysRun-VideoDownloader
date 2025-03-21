# PowerToysRun-VideoDownloader

A PowerToys Run plugin for video downloading.

## GitHub Actions Workflow

This repository includes a GitHub Actions workflow that automatically:

1. Builds the project for both x64 and ARM64 architectures
2. Creates zip files with the appropriate naming convention
3. Creates a GitHub release with these zip files

### How to Create a Release

To create a new release:

1. Tag your commit with a version number:
   ```
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. The GitHub Actions workflow will automatically:
   - Build the project for both x64 and ARM64
   - Create zip files named:
     - `VideoDownloader-v1.0.0-x64.zip`
     - `VideoDownloader-v1.0.0-arm64.zip`
   - Create a GitHub release with these zip files

The release will include:
- The compiled binaries for both architectures
- Source code in zip and tar.gz formats

## Development

To build the project locally:

```
dotnet build -c Release -p:Platform="x64"
```

Or for ARM64:

```
dotnet build -c Release -p:Platform="arm64"
```
# PowerToysRun-VideoDownloader
