# Release Checklist: Pub.dev Package Intelligence MCP

Use this guide when preparing the MCP for public release and registry submission.

## 1. Promote to Public Repository (T053)
- [ ] Create a new GitHub repository under the desired organization/user.
- [ ] Push the latest `001-build-an-mcp` branch history.
- [ ] Set `main` as the default branch and fast-forward it to the latest commit.
- [ ] Protect `main` with required PR reviews and status checks.

## 2. Prepare Documentation (T054)
- [x] Ensure `README.md` accurately describes features, setup, and transports.
- [x] Link to `specs/001-build-an-mcp/quickstart.md` for detailed instructions.
- [ ] Add a `LICENSE` file (MIT recommended for registry submission).

## 3. Build & Test Artifacts (T056)
- [ ] Clean working tree: `git status` should show no tracked modifications.
- [ ] Restore packages: `dotnet restore PubDevMcp.sln`.
- [ ] Build Release (trim/AOT ready):
  ```powershell
  dotnet build PubDevMcp.sln --configuration Release --nologo
  ```
- [ ] Run test suites (Release):
  ```powershell
  dotnet test tests/contract/PubDevMcp.Tests.Contract.csproj --configuration Release
  dotnet test tests/integration/PubDevMcp.Tests.Integration.csproj --configuration Release
  dotnet test tests/compliance/PubDevMcp.Tests.Compliance.csproj --configuration Release
  ```
- [ ] Publish NativeAOT binaries for each target runtime:
  ```powershell
  dotnet publish src/PubDevMcp.Server/PubDevMcp.Server.csproj `
    -c Release `
    -r win-x64 `
    /p:PublishAot=true /p:SelfContained=true /p:PublishTrimmed=true /p:InvariantGlobalization=true `
    --no-restore
  ```
  Repeat for `linux-x64` (from Linux host or container) and `osx-arm64` if desired.
- [ ] Archive publish outputs under `artifacts/` for release attachments.
- [ ] Capture build/test logs for inclusion in the GitHub release.

## 4. Registry Metadata (T055)
- [x] Populate `specs/001-build-an-mcp/registry-metadata.yaml`.
- [ ] Update `repository_url`, `homepage_url`, maintainer name/email, and artifact locations to match the public repo.
- [ ] Ensure transports list includes only `stdio` and `http` (no Docker requirement).

## 5. Tag & Release
- [ ] Bump version and tag (e.g., `v1.0.0`).
- [ ] Draft GitHub release with summary, changelog, and links to quickstart + README.
- [ ] Attach NativeAOT artifact bundles and build/test logs.
- [ ] Publish release after validation.

## 6. Submit to GitHub MCP Registry
- [ ] Complete the submission form or PR with metadata from `registry-metadata.yaml`.
- [ ] Provide repository link, transports, instructions, and contact info.
- [ ] Confirm submission states Docker image is not provided (NativeAOT binaries only).

## 7. Post-Submission Maintenance
- [ ] Monitor registry feedback/issues.
- [ ] Schedule dependency updates and security scans.
- [ ] Keep README and quickstart in sync with future changes.
