$version = msbuild $env:PROJECT_PATH /nologo /t:GetNuGetPackageVersion /getProperty:NuGetPackageVersion /p:Configuration=$env:CONFIGURATION
if ($LASTEXITCODE -ne 0 -or @($version).Count -ne 1 -or $version -notmatch '^\d+\.\d+\.\d+$') {
  throw "Expected a three-part release version from Nerdbank.GitVersioning. MSBuild output: $version"
}

$tag = "v$version"
$tagCommit = git rev-parse --verify --quiet "refs/tags/${tag}^{commit}"
if ($LASTEXITCODE -eq 0 -and $tagCommit -ne $env:GITHUB_SHA) {
  throw "Tag $tag already points to $tagCommit instead of $env:GITHUB_SHA."
}

$releaseJson = gh release view $tag --json isDraft,targetCommitish 2>$null
if ($LASTEXITCODE -eq 0) {
  $release = $releaseJson | ConvertFrom-Json
}
$published = $null -ne $release -and -not $release.isDraft
$draft = $null -ne $release -and $release.isDraft
if ($draft -and $release.targetCommitish -ne $env:GITHUB_SHA) {
  throw "Draft release $tag targets $($release.targetCommitish) instead of $env:GITHUB_SHA."
}
if ($published) {
  "Release $tag already exists." >> $env:GITHUB_STEP_SUMMARY
}
"published=$($published.ToString().ToLowerInvariant())" >> $env:GITHUB_OUTPUT
"draft=$($draft.ToString().ToLowerInvariant())" >> $env:GITHUB_OUTPUT
"tag=$tag" >> $env:GITHUB_OUTPUT
"archive=SinglePass.WPF.$version.zip" >> $env:GITHUB_OUTPUT
exit 0
