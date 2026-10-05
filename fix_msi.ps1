$msiScript = "installer/build-msi.ps1"
$content = Get-Content -Path $msiScript -Raw
$content = $content -replace '(?m)^Copy-Item -Path \(Join-Path \$projRoot "private_key\.xml"\) -Destination \$binDir -Force\r?\n?', ''
Set-Content -Path $msiScript -Value $content -NoNewline

$wxsScript = "installer/Product.wxs"
$wxsContent = Get-Content -Path $wxsScript -Raw
$wxsContent = $wxsContent -replace '(?ms)[ \t]*<Component Id="OpenEDMPrivateKey".*?</Component>\r?\n', ''
$wxsContent = $wxsContent -replace '(?m)[ \t]*<Permission User="Users" GenericAll="yes" />\r?\n', ''
Set-Content -Path $wxsScript -Value $wxsContent -NoNewline

Write-Host "Replaced successfully."
