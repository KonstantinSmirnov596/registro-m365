<#
  Registro Demo — шаг 4: регистрация входящего документа скриптом.
  То же, что делопроизводитель делал руками на шаге 3: файл в Entrada с реквизитами
  и видом документа → номер → строка в журнале Registro со ссылкой на файл.

  Запуск (pwsh 7):
    .\03-registrar-documento.ps1 -ClientId <ClientId> `
        -Path ..\..\..\samples\oficio-camara-regional-2026-09-05.pdf `
        -Remitente "Cámara Regional de Comercio del Este" -TipoDocumento Oficio -FechaDocumento 2026-09-05
#>
param(
    [Parameter(Mandatory)] [string]   $ClientId,
    [Parameter(Mandatory)] [string]   $Path,            # локальный PDF
    [Parameter(Mandatory)] [string]   $Remitente,
    [Parameter(Mandatory)] [ValidateSet("Carta","Oficio","Factura","Contrato")]
                           [string]   $TipoDocumento,
    [Parameter(Mandatory)] [datetime] $FechaDocumento,
    [string] $SiteUrl = "https://docmatico.sharepoint.com/sites/registro-demo"
)
$ErrorActionPreference = "Stop"
Connect-PnPOnline -Url $SiteUrl -Interactive -ClientId $ClientId

# --- 1. Файл в Entrada: вид документа + реквизиты одним вызовом ------------------------
$fileName = Split-Path $Path -Leaf
Add-PnPFile -Path $Path -Folder "Entrada" -ContentType "Documento de entrada" -Values @{
    Remitente      = $Remitente
    TipoDocumento  = $TipoDocumento
    FechaDocumento = $FechaDocumento
} | Out-Null

$web        = Get-PnPWeb
$fileRelUrl = "$($web.ServerRelativeUrl)/Entrada/$fileName"        # канонический адрес файла
$uri        = [Uri]$SiteUrl
$fileAbsUrl = "$($uri.Scheme)://$($uri.Host)$fileRelUrl"

# --- 2. Нумератор (ВРЕМЕННЫЙ) --------------------------------------------------------------
# Читаем журнал, берём максимальный номер года, +1. Не атомарно: два одновременных запуска
# получат один и тот же номер. Ровно эту дыру на блоке 4 закрывает Azure Function "Numerador" (C#).
$year   = (Get-Date).Year
$prefix = "E-$year-"
$max = Get-PnPListItem -List "Registro" -PageSize 500 |
       ForEach-Object { $_.FieldValues["Title"] } |
       Where-Object   { $_ -like "$prefix*" } |
       ForEach-Object { [int]$_.Substring($prefix.Length) } |
       Measure-Object -Maximum
$numero = "{0}{1:D6}" -f $prefix, ([int]$max.Maximum + 1)

# --- 3. Строка в журнале Registro ----------------------------------------------------------
$item = Add-PnPListItem -List "Registro" -Values @{
    Title          = $numero
    FechaRegistro  = (Get-Date)
    Remitente      = $Remitente
    TipoDocumento  = $TipoDocumento
    FechaDocumento = $FechaDocumento
    Documento      = "$fileAbsUrl, $fileName"      # формат поля URL: "адрес, описание"
}

# --- 4. Контроль ---------------------------------------------------------------------------
Write-Host "`nЗарегистрировано: $numero"
Write-Host "Файл в Entrada:"
(Get-PnPFile -Url $fileRelUrl -AsListItem).FieldValues |
    Select-Object @{n="FileLeafRef";e={$_["FileLeafRef"]}}, @{n="ContentType";e={$_["ContentTypeId"].StringValue}},
                  @{n="Remitente";e={$_["Remitente"]}}, @{n="TipoDocumento";e={$_["TipoDocumento"]}},
                  @{n="FechaDocumento";e={$_["FechaDocumento"]}} | Format-List

Write-Host "Журнал Registro:"
Get-PnPListItem -List "Registro" | ForEach-Object {
    [pscustomobject]@{
        Numero    = $_.FieldValues["Title"]
        Registro  = $_.FieldValues["FechaRegistro"]
        Remitente = $_.FieldValues["Remitente"]
        Tipo      = $_.FieldValues["TipoDocumento"]
        Documento = $_.FieldValues["Documento"].Url
    }
} | Format-Table -AutoSize

Disconnect-PnPOnline
