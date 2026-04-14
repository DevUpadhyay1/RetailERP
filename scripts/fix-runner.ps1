# Fix runner service - Run AS ADMINISTRATOR
$svc = "actions.runner.DevUpadhyay1-RetailERP.retailerp-local"
Stop-Service $svc -Force -ErrorAction SilentlyContinue
Start-Sleep 2
sc.exe config $svc obj= "LocalSystem" password= ""
net localgroup docker-users "NT AUTHORITY\SYSTEM" /add 2>$null
Start-Service $svc
Start-Sleep 5
Get-Service $svc | Format-Table Name, Status -AutoSize
