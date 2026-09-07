<#
  Registro Demo — шаг 1: сайт, site columns, библиотека Entrada.
  Запуск (pwsh 7, PnP.PowerShell 3.4.1):
    .\01-site-entrada.ps1 -ClientId <ClientId приложения PnP.PowerShell>
  Аналогия 1С:ДО: сайт = информационная база демо; site column = реквизит;
  библиотека = хранилище файлов вида документа (пока без вида — он появится на шаге 2).
#>
param(
    [Parameter(Mandatory)] [string] $ClientId,
    [string] $Tenant  = "docmatico",
    [string] $SiteUrl = "https://docmatico.sharepoint.com/sites/registro-demo"
)

$ErrorActionPreference = "Stop"   # любая ошибка останавливает скрипт, а не идёт дальше молча
$rootUrl = "https://$Tenant.sharepoint.com"

# --- 1. Сайт -----------------------------------------------------------------
Connect-PnPOnline -Url $rootUrl -Interactive -ClientId $ClientId
Write-Host "Создаю сайт $SiteUrl ..."
New-PnPSite -Type CommunicationSite -Title "Registro Demo" -Url $SiteUrl -Lcid 1033 -Wait
Disconnect-PnPOnline

# --- 2. Site columns (реквизиты) — группа Registro ----------------------------
Connect-PnPOnline -Url $SiteUrl -Interactive -ClientId $ClientId

Add-PnPField -DisplayName "Remitente"           -InternalName "Remitente"      -Type Text     -Group "Registro" | Out-Null
Add-PnPField -DisplayName "Fecha del documento" -InternalName "FechaDocumento" -Type DateTime -Group "Registro" | Out-Null
Add-PnPField -DisplayName "Tipo documental"     -InternalName "TipoDocumento"  -Type Choice   -Group "Registro" `
             -Choices "Carta","Oficio","Factura","Contrato" | Out-Null

# --- 3. Библиотека Entrada ------------------------------------------------------
New-PnPList -Title "Entrada" -Template DocumentLibrary -Url "Entrada" -OnQuickLaunch -EnableContentTypes -EnableVersioning | Out-Null

# --- 4. Контроль: что получилось ---------------------------------------------------
Write-Host "`nSite columns группы Registro:"
Get-PnPField | Where-Object { $_.Group -eq "Registro" } | Select-Object Title, InternalName, TypeAsString | Format-Table -AutoSize

Write-Host "Библиотеки сайта:"
Get-PnPList | Where-Object { $_.BaseTemplate -eq 101 } | Select-Object Title, DefaultViewUrl, ContentTypesEnabled, EnableVersioning | Format-Table -AutoSize

Disconnect-PnPOnline
