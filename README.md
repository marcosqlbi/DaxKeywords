# DaxKeywords

This command line tool tests the DAX keywords for validity in various scenarios:
- Function name (partial name separated by dot)
- Table name (unquoted table reference)
- Variable name
- Parameter name

## Usage

Run DaxKeywords from the command line with the following syntax:
```
DaxKeywords <connectionString> 
```

Where `<connectionString>` is the connection string to your Analysis Services instance (e.g. "Data Source=localhost;Database=Contoso").

The tool will output the results of the tests to the console, indicating which keywords are valid or invalid in each scenario.
