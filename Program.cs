using System;
using System.Collections.Generic;
using Microsoft.AnalysisServices.AdomdClient;

class Program
{
    static void Main(string[] args)
    {
        // Read connection string from first argument or environment variable 'AS_CONNECTION_STRING'
        string connectionString = args.Length >0 && !string.IsNullOrWhiteSpace(args[0])
         ? args[0]
         : Environment.GetEnvironmentVariable("AS_CONNECTION_STRING") ?? string.Empty;

        Console.WriteLine($"Connecting to Analysis Services... {connectionString}");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("Please provide an Analysis Services connection string as the first argument or set AS_CONNECTION_STRING environment variable.");
            return;
        }

        //1) Connection (existing code) -> moved to GetKeywords
         var keywords = GetKeywords(connectionString);

         //2) Testing: execute DEFINE FUNCTION / EVALUATE for each keyword
         var testResults = Testing(keywords, connectionString);

         //3) Output (todo)
         Output(keywords, testResults);
    }

    static List<string> GetKeywords(string connectionString)
    {
        var keywords = new List<string>();
        const string query = "SELECT Keyword FROM $System.DISCOVER_KEYWORDS";

        try
        {
            using var conn = new AdomdConnection(connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                // The Keyword column is the first column in the result set
                if (!reader.IsDBNull(0))
                    keywords.Add(reader.GetString(0));
            }

            Console.WriteLine($"Retrieved {keywords.Count} keywords.");
        }
        catch (AdomdConnectionException cex)
        {
            Console.WriteLine($"Connection error: {cex.Message}");
        }
        catch (AdomdException eex)
        {
            Console.WriteLine($"ADOMD error: {eex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }

        return keywords;
    }

    /// <summary>
    /// Result container for tests performed per keyword. Stores boolean flags that indicate the keyword is not allowed for a syntax element.
    /// If a flag is true it means the keyword cannot be used unquoted in that position (i.e., it's reserved/not allowed).
    /// </summary>
    class KeywordTestResult
    {
        public string Keyword { get; set; } = string.Empty;

        // If true, the keyword cannot be used as an unquoted function name (reserved)
        public bool FunctionNameNotAllowed { get; set; }

        // boolean results for the other tests (true = not allowed/reserved)
        public bool TableNameForUnquotedReferencesNotAllowed { get; set; }
        public bool VariableNameNotAllowed { get; set; }
        public bool ParameterNameNotAllowed { get; set; }
    }

    static List<KeywordTestResult> Testing(List<string> keywords, string connectionString)
    {
        var results = new List<KeywordTestResult>();

        // Open a single connection and reuse it for all tests
        try
        {
            using var conn = new AdomdConnection(connectionString);
            conn.Open();

            foreach (var kw in keywords)
            {
                var result = new KeywordTestResult { Keyword = kw };

                // Run individual tests using helper methods
                result.FunctionNameNotAllowed = TestFunctionName(conn, kw);
                result.VariableNameNotAllowed = TestVariableName(conn, kw);
                result.TableNameForUnquotedReferencesNotAllowed = TestTableName(conn, kw);
                result.ParameterNameNotAllowed = TestParameterName(conn, kw);

                results.Add(result);
            }
        }
        catch (AdomdConnectionException cex)
        {
            Console.WriteLine($"Connection error (testing): {cex.Message}");
        }
        catch (AdomdException eex)
        {
            Console.WriteLine($"ADOMD error (testing): {eex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error (testing): {ex.Message}");
        }

        Console.WriteLine($"Completed function-name tests for {results.Count} keywords.");
        return results;
    }

    static bool TestFunctionName(AdomdConnection conn, string keyword)
    {
        try
        {
            // Build DAX by concatenation to avoid brace escaping
            var dax = "DEFINE FUNCTION Test." + keyword + " = () => {42}\nEVALUATE Test." + keyword + "()";
            using var cmd = conn.CreateCommand();
            cmd.CommandText = dax;
            using var reader = cmd.ExecuteReader();
            // Execution succeeded -> keyword is allowed as function name
            return false; // not not-allowed
        }
        catch (AdomdException)
        {
            return true; // not allowed
        }
        catch (Exception)
        {
            return true;
        }
    }

    static bool TestVariableName(AdomdConnection conn, string keyword)
    {
        try
        {
            // DEFINE VAR {variableName} = {42} EVALUATE {variableName}
            var dax = "DEFINE VAR " + keyword + " = {42} EVALUATE {" + keyword + "}";
            using var cmd = conn.CreateCommand();
            cmd.CommandText = dax;
            using var reader = cmd.ExecuteReader();
            return false;
        }
        catch (AdomdException)
        {
            return true;
        }
        catch (Exception)
        {
            return true;
        }
    }

    static bool TestTableName(AdomdConnection conn, string keyword)
    {
        try
        {
            // DEFINE TABLE {tableName} = {42} EVALUATE {tableName}
            var dax = "DEFINE TABLE " + keyword + " = {42}\nEVALUATE {" + keyword + "}";
            using var cmd = conn.CreateCommand();
            cmd.CommandText = dax;
            using var reader = cmd.ExecuteReader();
            return false;
        }
        catch (AdomdException)
        {
            return true;
        }
        catch (Exception)
        {
            return true;
        }
    }

    static bool TestParameterName(AdomdConnection conn, string keyword)
    {
        try
        {
            // DEFINE FUNCTION x = ( parameterName ) => parameterName
            // EVALUATE { x(42) }
            var dax = "DEFINE FUNCTION x = (" + keyword + ") => " + keyword + "\nEVALUATE { x(42) }";
            using var cmd = conn.CreateCommand();
            cmd.CommandText = dax;
            using var reader = cmd.ExecuteReader();
            return false;
        }
        catch (AdomdException)
        {
            return true;
        }
        catch (Exception)
        {
            return true;
        }
    }

    static void Output(List<string> keywords, List<KeywordTestResult> testResults)
    {
        // Build lists of allowed keywords (NotAllowed == false)
        var functionAllowed = new List<string>();
        var tableAllowed = new List<string>();
        var variableAllowed = new List<string>();
        var parameterAllowed = new List<string>();

        foreach (var r in testResults)
        {
            if (!r.FunctionNameNotAllowed) functionAllowed.Add(r.Keyword);
            if (!r.TableNameForUnquotedReferencesNotAllowed) tableAllowed.Add(r.Keyword);
            if (!r.VariableNameNotAllowed) variableAllowed.Add(r.Keyword);
            if (!r.ParameterNameNotAllowed) parameterAllowed.Add(r.Keyword);
        }

        Console.WriteLine("Output: keywords allowed summary");

        // Helper local to print count and all items
        void PrintAll(string title, List<string> items)
        {
            Console.WriteLine($"{title}: {items.Count}");
            for (int i =0; i < items.Count; i++)
            {
                Console.WriteLine($"  {i +1}. {items[i]}");
            }
            Console.WriteLine();
        }

        PrintAll("Keywords allowed as function names", functionAllowed);
        PrintAll("Keywords allowed as table names (unquoted references)", tableAllowed);
        PrintAll("Keywords allowed as variable names", variableAllowed);
        PrintAll("Keywords allowed as parameter names", parameterAllowed);
    }
}
