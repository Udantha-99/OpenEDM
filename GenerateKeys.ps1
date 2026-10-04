$rsa = [System.Security.Cryptography.RSA]::Create(2048)
$privateKey = $rsa.ToXmlString($true)
$publicKey = $rsa.ToXmlString($false)
Set-Content -Path "private_key.xml" -Value $privateKey
Set-Content -Path "public_key.xml" -Value $publicKey
Write-Output "RSA Keys generated: private_key.xml, public_key.xml"
