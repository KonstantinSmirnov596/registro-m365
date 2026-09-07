<#
  Registro Demo — шаг 2: content type "Documento de entrada" + список-журнал "Registro".
  Запуск (pwsh 7, PnP.PowerShell 3.4.1):
    .\02-contenttype-registro.ps1 -ClientId <ClientId>
  Аналогия 1С:ДО: content type = вид документа (набор реквизитов + шаблон);
  привязка content type к библиотеке = «в каком журнале/папке живут документы этого вида»;
  список Registro = регистрационный журнал (номер, дата, ссылка на карточку).
#>
param(
    [Parameter(Mandatory)] [string] $ClientId,
    [string] $SiteUrl = "https://docmatico.sharepoint.com/sites/registro-demo"
)
$ErrorActionPreference = "Stop"
Connect-PnPOnline -Url $SiteUrl -Interactive -ClientId $ClientId

# --- 1. Content type "Documento de entrada", родитель — встроенный Document (0x0101) ----
Add-PnPContentType -Name "Documento de entrada" -Group "Registro" `
    -Description "Documento recibido de un remitente externo" `
    -ParentContentType (Get-PnPContentType -Identity 0x0101) | Out-Null

Add-PnPFieldToContentType -Field "Remitente"      -ContentType "Documento de entrada" -Required
Add-PnPFieldToContentType -Field "FechaDocumento" -ContentType "Documento de entrada"
Add-PnPFieldToContentType -Field "TipoDocumento"  -ContentType "Documento de entrada" -Required

# --- 2. Привязать к библиотеке Entrada как единственный вид --------------------------
Add-PnPContentTypeToList    -List "Entrada" -ContentType "Documento de entrada" -DefaultContentType
Remove-PnPContentTypeFromList -List "Entrada" -ContentType "Document"
Set-PnPView -List "Entrada" -Identity "All Documents" `
    -Fields "DocIcon","LinkFilename","Remitente","TipoDocumento","FechaDocumento","Modified","Editor" | Out-Null

# --- 3. Список-журнал Registro ----------------------------------------------------------
New-PnPList -Title "Registro" -Template GenericList -Url "Lists/Registro" -OnQuickLaunch | Out-Null
Set-PnPField -List "Registro" -Identity "Title" -Values @{ Title = "Número de registro" }

# те же реквизиты, что в виде документа — переиспользуем site columns, не создаём копии
Add-PnPField -List "Registro" -Field "Remitente"      | Out-Null
Add-PnPField -List "Registro" -Field "TipoDocumento"  | Out-Null
Add-PnPField -List "Registro" -Field "FechaDocumento" | Out-Null
# реквизиты только журнала
Add-PnPField -List "Registro" -DisplayName "Fecha de registro" -InternalName "FechaRegistro" -Type DateTime | Out-Null
Add-PnPField -List "Registro" -DisplayName "Documento"         -InternalName "Documento"     -Type URL      | Out-Null

Set-PnPView -List "Registro" -Identity "All Items" `
    -Fields "LinkTitle","FechaRegistro","Remitente","TipoDocumento","FechaDocumento","Documento" | Out-Null

# --- 4. Контроль ------------------------------------------------------------------------
Write-Host "`nContent types библиотеки Entrada:"
Get-PnPContentType -List "Entrada" | Select-Object Name, Id | Format-Table -AutoSize

Write-Host "Поля content type 'Documento de entrada' из группы Registro:"
(Get-PnPContentType -Identity "Documento de entrada" -Includes Fields).Fields |
    Where-Object { $_.Group -eq "Registro" } | Select-Object Title, InternalName, Required | Format-Table -AutoSize

Write-Host "Колонки списка Registro:"
Get-PnPField -List "Registro" | Where-Object { $_.InternalName -in "Title","FechaRegistro","Remitente","TipoDocumento","FechaDocumento","Documento" } |
    Select-Object Title, InternalName, TypeAsString | Format-Table -AutoSize

Disconnect-PnPOnline
