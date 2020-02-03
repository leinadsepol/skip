using System;
using System.Text.Json;
using System.Diagnostics;
using System.IO;

namespace windows
{
    class Program
    {
        private const int tabSize = 4;

        static void Main(string[] args)
        {
            Process wslProcess = new Process();
            wslProcess.StartInfo.FileName = "wsl";
            wslProcess.StartInfo.Arguments = "skip_lsp";
            // Set UseShellExecute to false for redirection.
            wslProcess.StartInfo.UseShellExecute = false;
            // Redirect the standard output of the sort command.  
            // This stream is read asynchronously using an event handler.
            wslProcess.StartInfo.RedirectStandardOutput = true;
            // Set our event handler to asynchronously read the sort output.
            wslProcess.OutputDataReceived += WslOutputHandler;
            // Redirect standard input as well.  This stream
            // is used synchronously.
            wslProcess.StartInfo.RedirectStandardInput = true;
            wslProcess.Start();
            // Use a stream writer to synchronously write the sort input.
            StreamWriter wslStreamWriter = wslProcess.StandardInput;
            // Start the asynchronous read of the sort output stream.
            wslProcess.BeginOutputReadLine();
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                if (line.StartsWith("{")) {
                    JsonElement elem = JsonSerializer.Deserialize<JsonElement>(line);
                    var toWsl = Convert(elem, true);
                    // Start the process.
                    wslStreamWriter.WriteLine(toWsl);
                } else {
                    wslStreamWriter.WriteLine(line);
                }
            }
        }

        private static String wslpath(String path, bool toLinux)
        {
            Process process = new Process();
            process.StartInfo.FileName = "wsl";
            process.StartInfo.Arguments = String.Format(
                "wslpath {0} {1}",
                toLinux ? "-u" : "-w",
                path
            );
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            //* Read the output (or the error)
            string output = process.StandardOutput.ReadToEnd().Trim();
            string err = process.StandardError.ReadToEnd();
            Console.WriteLine(err);
            process.WaitForExit();
            return output;
        }

        private static void WslOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            // Collect the sort command output.
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                var line = outLine.Data;
                // Add the text to the collected output.
                if (line.StartsWith("{")) {
                    JsonElement elem = JsonSerializer.Deserialize<JsonElement>(line);
                    Console.WriteLine(Convert(elem, false));
                } else {
                    Console.WriteLine(line);
                }
            }
        }

        static string Convert(JsonElement elem, bool toLinux) {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    ConvertElement(writer, elem, toLinux);
                }
                stream.Seek(0, SeekOrigin.Begin);
                string s;
                using (var readr = new StreamReader(stream))
                {
                    s = readr.ReadToEnd();
                }
                return s;
            }
        }

        static void ConvertElement(Utf8JsonWriter writer, JsonElement elem, bool toLinux) {
            switch (elem.ValueKind) {
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                    writer.WriteNullValue();
                    break;
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    JsonElement.ObjectEnumerator objEmun = elem.EnumerateObject();
                    foreach (var value in objEmun)
                    {
                        writer.WritePropertyName(value.Name);
                        ConvertElement(writer,  value.Value, toLinux);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.String:
                    var str = elem.GetString();
                    if (str.Contains("/") || str.Contains("\\")) {
                        if (File.Exists(str)) {
                            str = wslpath(str, toLinux);
                        }
                    }
                    writer.WriteStringValue(str);
                    break;
                case JsonValueKind.Number:
                    writer.WriteNumberValue(elem.GetDecimal());
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    JsonElement.ArrayEnumerator emun = elem.EnumerateArray();
                    foreach (var value in emun)
                    {
                        ConvertElement(writer,  value, toLinux);
                    }
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.Undefined:
                default : break;
            }
        }
    }


    class ResponseError {
        /**
        * A number indicating the error type that occurred.
        */
        public int id { get; set; }

        /**
        * A string providing a short description of the error.
        */
        public string message { get; set; }
    }

    class ResponseMessage
    {
        public string id { get; set; }
        public string jsonrpc { get; set; }
        public ResponseError error { get; set; }
    }
}
