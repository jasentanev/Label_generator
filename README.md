# Windows Label Generator

First implementation of the planned Windows desktop label workflow.

## What is included

- .NET 10 WPF application with MVVM-style view models.
- Configurable data source profiles for primary/detail database views or direct `SELECT` statements.
- ADO.NET data access with built-in provider registration for SQL Server, PostgreSQL, MySQL and ODBC.
- Demo provider so the app runs without a database.
- Regex column filtering in the application layer.
- Template metadata plus editable label design elements stored in configuration.
- WPF preview and direct Windows printer output with text, field, barcode, image and rectangle elements.
- Optional per-row quantity printing: if the detail view contains `LabelCount`, that row prints that many labels.

## Run

```powershell
dotnet run --project .\LabelGenerator.App\LabelGenerator.App.csproj
```

## Configure a real database

Edit `LabelGenerator.App\Config\appsettings.json` and add a `dataSources` entry:

```json
{
  "id": "sql-prod",
  "displayName": "Production SQL labels",
  "providerInvariantName": "Microsoft.Data.SqlClient",
  "connectionSecret": "Server=.;Database=Labels;Trusted_Connection=True;TrustServerCertificate=True",
  "primaryView": "dbo.vw_label_candidates",
  "detailView": "dbo.vw_label_details",
  "keyColumn": "ProductCode",
  "maxRows": 500,
  "commandTimeoutSeconds": 30,
  "visibleColumns": [ "ProductCode", "ProductName", "BatchNo", "Status" ]
}
```

Supported provider names in this implementation:

- `Microsoft.Data.SqlClient`
- `Npgsql`
- `MySqlConnector`
- `System.Data.Odbc`
- `Demo`

Use database views to hide complex SQL when possible. Configure them with `primaryView`, `detailView`, and `keyColumn`.

You can also configure direct `SELECT` statements:

```json
{
  "id": "odbc-prod",
  "displayName": "ODBC labels",
  "providerInvariantName": "System.Data.Odbc",
  "connectionSecret": "DSN=LabelsDsn;Uid=user;Pwd=password;",
  "primarySql": "select ProductCode, ProductName, Status from vw_label_candidates where IsActive = 1",
  "detailSql": "select * from vw_label_details where ProductCode in ({Keys})",
  "keyColumn": "ProductCode",
  "maxRows": 500,
  "commandTimeoutSeconds": 30,
  "visibleColumns": [ "ProductCode", "ProductName", "Status" ]
}
```

`primarySql` and `detailSql` must be single `SELECT` statements. `detailSql` must contain `{Keys}` so the app can inject selected key parameters safely. If `primarySql/detailSql` are empty, the app uses `primaryView/detailView`.

## Shared configuration

At runtime both applications use `%LOCALAPPDATA%\LabelGenerator\appsettings.json`.
The bundled `Config\appsettings.json` is copied there on first run. Set `LABEL_GENERATOR_CONFIG` to point both apps at another JSON file.

## Designer

```powershell
dotnet run --project .\LabelGenerator.Designer\LabelGenerator.Designer.csproj
```

The designer edits `labelTemplates[]` in the shared configuration file. It supports multiple templates, text, database fields, barcodes, images, rectangles, master-view regex filters, and per-template `LabelCount` settings.
The main app's `Designer` button also launches this separate designer when it can find the built executable or project.
Select an element and use `Delete selected`, the element-list `Delete` button, or the keyboard `Delete` key to remove it.

## Template master filters

Each template can store `masterFilters`. These are regex filters applied to the first/master view when that template is selected. This lets a promo label show only promo rows, a damage label show only damage rows, and so on. Manual filters in the main app are applied in addition to template filters.

## Label count

Each template has a `labelCount` setting. By default it is enabled and reads the `LabelCount` column from the second/detail view. Missing or invalid values print one label; `0` suppresses labels for that row; values are capped by `maxCountPerRow`.

## List & Label integration point

The current renderer uses the built-in WPF template design. If you later install combit List & Label, keep `templateFilePath` for external project files and replace the print service behind the existing print boundary.
